using System.Text.RegularExpressions;
using AIDevelopmentEasy.Core.Analysis;
using AIDevelopmentEasy.Core.Models;
using Microsoft.Extensions.Logging;

namespace AIDevelopmentEasy.Core.Analysis.Go;

/// <summary>
/// Analyzes Go codebases (go.mod, *.go) and produces a <see cref="CodebaseAnalysis"/> with
/// <see cref="ProjectInfo.LanguageId"/> = "go".
/// </summary>
public class GoCodebaseAnalyzer : ICodebaseAnalyzer
{
    public string LanguageId => "go";

    private readonly ILogger<GoCodebaseAnalyzer>? _logger;

    private static readonly Regex ModuleRegex = new(@"^\s*module\s+(.+)$", RegexOptions.Multiline);
    private static readonly Regex GoVersionRegex = new(@"^\s*go\s+([\d.]+)", RegexOptions.Multiline);
    private static readonly Regex RequireLineRegex = new(@"^\s*require\s+(\S+)\s+(\S+)", RegexOptions.Multiline);
    private static readonly Regex PackageRegex = new(@"^\s*package\s+(\w+)", RegexOptions.Multiline);
    private static readonly Regex TypeStructRegex = new(@"type\s+(\w+)\s+struct\s*(?:\{[^}]*\})?", RegexOptions.Compiled);
    private static readonly Regex TypeInterfaceRegex = new(@"type\s+(\w+)\s+interface\s*(?:\{[^}]*\})?", RegexOptions.Compiled);
    private static readonly Regex FuncRegex = new(@"func\s+(?:\([^)]+\)\s+)?(\w+)\s*\([^)]*\)", RegexOptions.Compiled);

    public GoCodebaseAnalyzer(ILogger<GoCodebaseAnalyzer>? logger = null)
    {
        _logger = logger;
    }

    public bool CanAnalyze(string codebasePath)
    {
        if (!Directory.Exists(codebasePath))
            return false;
        var goModFiles = Directory.GetFiles(codebasePath, "go.mod", SearchOption.AllDirectories);
        return goModFiles.Length > 0;
    }

    public async Task<CodebaseAnalysis> AnalyzeAsync(string codebasePath, string codebaseName, CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("[GoCodebaseAnalyzer] Starting analysis of: {Path}", codebasePath);

        if (!Directory.Exists(codebasePath))
            throw new DirectoryNotFoundException($"Codebase path not found: {codebasePath}");

        var analysis = new CodebaseAnalysis
        {
            CodebaseName = codebaseName,
            CodebasePath = codebasePath,
            AnalyzedAt = DateTime.UtcNow
        };

        var goModFiles = Directory.GetFiles(codebasePath, "go.mod", SearchOption.AllDirectories)
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + "vendor" + Path.DirectorySeparatorChar))
            .ToList();

        foreach (var goModPath in goModFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var projectInfo = await ParseGoModAsync(goModPath, codebasePath, cancellationToken);
            if (projectInfo != null)
            {
                projectInfo.LanguageId = LanguageId;
                projectInfo.RootPath = GetRelativePath(Path.GetDirectoryName(goModPath)!, codebasePath);
                projectInfo.Role = InferRole(projectInfo.RootPath);
                analysis.Projects.Add(projectInfo);
            }
        }

        _logger?.LogInformation("[GoCodebaseAnalyzer] Found {Count} module(s)", analysis.Projects.Count);

        analysis.Summary = BuildSummary(analysis);
        analysis.Summary.Languages = new List<string> { LanguageId };
        analysis.Conventions = new CodeConventions { NamingStyle = "MixedCaps", PrivateFieldPrefix = "" };
        analysis.RequirementContext = BuildRequirementContext(analysis);
        analysis.PipelineContext = BuildPipelineContext(analysis);

        return analysis;
    }

    private string InferRole(string rootPath)
    {
        var lower = rootPath.Replace('\\', '/').ToLowerInvariant();
        if (lower.Contains("frontend") || lower.Contains("web") || lower.Contains("ui")) return "Frontend";
        if (lower.Contains("backend") || lower.Contains("api") || lower.Contains("cmd")) return "Backend";
        return "";
    }

    private static string GetRelativePath(string fullPath, string basePath)
    {
        if (fullPath.StartsWith(basePath, StringComparison.OrdinalIgnoreCase))
        {
            var relative = fullPath.Substring(basePath.Length);
            return relative.TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        return fullPath;
    }

    private static void ParseGoModRequires(string goModContent, ProjectInfo projectInfo)
    {
        foreach (Match m in RequireLineRegex.Matches(goModContent))
        {
            var module = m.Groups[1].Value.Trim();
            var version = m.Groups[2].Value.Trim();
            if (string.IsNullOrEmpty(module) || module.StartsWith("("))
                continue;
            if (!projectInfo.PackageReferences.Any(p => p.Name == module))
                projectInfo.PackageReferences.Add(new PackageReference { Name = module, Version = version });
        }
        var inRequire = false;
        foreach (var line in goModContent.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("require ", StringComparison.Ordinal))
            {
                var rest = trimmed.Substring(8).Trim();
                if (rest.StartsWith("("))
                {
                    inRequire = true;
                    continue;
                }
                var parts = rest.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2 && !string.IsNullOrEmpty(parts[0]) && !projectInfo.PackageReferences.Any(p => p.Name == parts[0]))
                    projectInfo.PackageReferences.Add(new PackageReference { Name = parts[0], Version = parts[1] });
                continue;
            }
            if (inRequire)
            {
                if (trimmed == ")")
                {
                    inRequire = false;
                    continue;
                }
                var parts = trimmed.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2 && !string.IsNullOrEmpty(parts[0]) && !projectInfo.PackageReferences.Any(p => p.Name == parts[0]))
                    projectInfo.PackageReferences.Add(new PackageReference { Name = parts[0], Version = parts[1] });
            }
        }
    }

    private async Task<ProjectInfo?> ParseGoModAsync(string goModPath, string basePath, CancellationToken cancellationToken)
    {
        var moduleDir = Path.GetDirectoryName(goModPath)!;
        var content = await File.ReadAllTextAsync(goModPath, cancellationToken);

        var moduleMatch = ModuleRegex.Match(content);
        var moduleName = moduleMatch.Success ? moduleMatch.Groups[1].Value.Trim() : Path.GetFileName(moduleDir);
        var goVersionMatch = GoVersionRegex.Match(content);
        var goVersion = goVersionMatch.Success ? goVersionMatch.Groups[1].Value : "";

        var projectInfo = new ProjectInfo
        {
            Name = moduleName,
            Path = goModPath,
            RelativePath = GetRelativePath(goModPath, basePath),
            ProjectDirectory = GetRelativePath(moduleDir, basePath),
            TargetFramework = string.IsNullOrEmpty(goVersion) ? "go" : $"go {goVersion}",
            OutputType = "Exe",
            RootNamespace = moduleName
        };

        ParseGoModRequires(content, projectInfo);

        var goFiles = Directory.GetFiles(moduleDir, "*.go", SearchOption.AllDirectories)
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + "vendor" + Path.DirectorySeparatorChar))
            .ToList();

        foreach (var goFile in goFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var fileContent = await File.ReadAllTextAsync(goFile, cancellationToken);
                var relativePath = GetRelativePath(goFile, basePath);

                var pkgMatch = PackageRegex.Match(fileContent);
                var pkgName = pkgMatch.Success ? pkgMatch.Groups[1].Value : "main";
                if (!projectInfo.Namespaces.Contains(pkgName))
                    projectInfo.Namespaces.Add(pkgName);

                foreach (Match m in TypeStructRegex.Matches(fileContent))
                {
                    var name = m.Groups[1].Value;
                    projectInfo.Classes.Add(new TypeInfo
                    {
                        Name = name,
                        Namespace = pkgName,
                        FilePath = relativePath,
                        DetectedPattern = InferGoPattern(name, fileContent)
                    });
                }
                foreach (Match m in TypeInterfaceRegex.Matches(fileContent))
                {
                    projectInfo.Interfaces.Add(new TypeInfo
                    {
                        Name = m.Groups[1].Value,
                        Namespace = pkgName,
                        FilePath = relativePath
                    });
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error parsing Go file: {Path}", goFile);
            }
        }

        projectInfo.DetectedPatterns = projectInfo.Classes.Select(c => c.DetectedPattern).Where(p => !string.IsNullOrEmpty(p)).Cast<string>().Distinct().ToList();
        return projectInfo;
    }

    private static string? InferGoPattern(string typeName, string content)
    {
        if (typeName.Contains("Handler") || typeName.Contains("Controller")) return "Controller";
        if (typeName.Contains("Service") || typeName.Contains("Manager")) return "Service";
        if (typeName.Contains("Repository") || typeName.Contains("Store")) return "Repository";
        if (typeName.EndsWith("Test") && content.Contains("testing.T")) return "UnitTest";
        return null;
    }

    private CodebaseSummary BuildSummary(CodebaseAnalysis analysis)
    {
        return new CodebaseSummary
        {
            TotalSolutions = 0,
            TotalProjects = analysis.Projects.Count,
            TotalClasses = analysis.Projects.Sum(p => p.Classes.Count),
            TotalInterfaces = analysis.Projects.Sum(p => p.Interfaces.Count),
            TotalFiles = analysis.Projects.Sum(p => p.Classes.Select(c => c.FilePath).Concat(p.Interfaces.Select(i => i.FilePath)).Distinct().Count()),
            PrimaryFramework = analysis.Projects.FirstOrDefault()?.TargetFramework ?? "go",
            DetectedPatterns = analysis.Projects.SelectMany(p => p.DetectedPatterns).Distinct().ToList(),
            KeyNamespaces = analysis.Projects.SelectMany(p => p.Namespaces).Distinct().Take(10).ToList()
        };
    }

    private RequirementContext BuildRequirementContext(CodebaseAnalysis analysis)
    {
        var context = new RequirementContext();
        foreach (var project in analysis.Projects)
        {
            var projectType = DetectGoProjectType(project);
            var purpose = InferGoPurpose(project, projectType);
            context.Projects.Add(new ProjectBrief
            {
                Name = project.Name,
                Type = projectType,
                Purpose = purpose,
                KeyNamespaces = project.Namespaces.Take(5).ToList()
            });
        }
        context.Architecture = DetectGoArchitecture(analysis);
        context.Technologies = DetectGoTechnologies(analysis);
        context.ExtensionPoints = FindGoExtensionPoints(analysis);
        context.SummaryText = GenerateGoRequirementSummaryText(context, analysis);
        context.TokenEstimate = EstimateTokens(context.SummaryText);
        return context;
    }

    private static string DetectGoProjectType(ProjectInfo project)
    {
        var path = project.RootPath.Replace('\\', '/').ToLowerInvariant();
        if (path.Contains("cmd")) return "CLI";
        if (path.Contains("api") || path.Contains("server") || path.Contains("main")) return "API";
        if (path.Contains("internal")) return "Library";
        if (project.Classes.Any(c => c.DetectedPattern == "UnitTest")) return "Tests";
        return "Library";
    }

    private static string InferGoPurpose(ProjectInfo project, string projectType)
    {
        return projectType switch
        {
            "CLI" => "Command-line entry point",
            "API" => "HTTP API (handlers, routes)",
            "Library" => "Shared packages and business logic",
            "Tests" => "Unit and integration tests",
            _ => "Go module"
        };
    }

    private static List<string> DetectGoArchitecture(CodebaseAnalysis analysis)
    {
        var layers = new List<string>();
        var patterns = analysis.Projects.SelectMany(p => p.DetectedPatterns).Distinct().ToList();
        if (patterns.Any(p => p == "Controller" || p == "Handler")) layers.Add("Handler/API Layer");
        if (patterns.Any(p => p == "Service")) layers.Add("Service Layer");
        if (patterns.Any(p => p == "Repository")) layers.Add("Repository/Data Layer");
        if (layers.Count == 0) layers.Add("Go modules (packages)");
        return layers;
    }

    private static List<string> DetectGoTechnologies(CodebaseAnalysis analysis)
    {
        var tech = new HashSet<string> { "Go" };
        var first = analysis.Projects.FirstOrDefault();
        if (!string.IsNullOrEmpty(first?.TargetFramework) && first.TargetFramework.StartsWith("go "))
            tech.Add(first.TargetFramework);
        return tech.ToList();
    }

    private static List<ExtensionPoint> FindGoExtensionPoints(CodebaseAnalysis analysis)
    {
        var points = new List<ExtensionPoint>();
        foreach (var project in analysis.Projects)
        {
            var handlers = project.Classes.Where(c => c.DetectedPattern == "Controller" || c.DetectedPattern == "Handler").ToList();
            if (handlers.Any())
            {
                var pkg = handlers.First().Namespace;
                points.Add(new ExtensionPoint { Layer = "Handlers", Project = project.Name, Namespace = pkg, Pattern = "Handler" });
            }
            var services = project.Classes.Where(c => c.DetectedPattern == "Service").ToList();
            if (services.Any())
            {
                var pkg = services.First().Namespace;
                points.Add(new ExtensionPoint { Layer = "Services", Project = project.Name, Namespace = pkg, Pattern = "Service" });
            }
            var repos = project.Classes.Where(c => c.DetectedPattern == "Repository").ToList();
            if (repos.Any())
            {
                var pkg = repos.First().Namespace;
                points.Add(new ExtensionPoint { Layer = "Repositories", Project = project.Name, Namespace = pkg, Pattern = "Repository" });
            }
        }
        return points.Take(10).ToList();
    }

    private static string GenerateGoRequirementSummaryText(RequirementContext context, CodebaseAnalysis analysis)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"# Codebase: {analysis.CodebaseName}");
        sb.AppendLine();
        sb.AppendLine("## Architecture (Go best practices)");
        foreach (var layer in context.Architecture) sb.AppendLine($"- {layer}");
        sb.AppendLine();
        sb.AppendLine("## Projects (modules)");
        foreach (var project in context.Projects)
        {
            sb.AppendLine($"- **{project.Name}** ({project.Type}): {project.Purpose}");
            if (project.KeyNamespaces.Any())
                sb.AppendLine($"  Packages: {string.Join(", ", project.KeyNamespaces)}");
        }
        sb.AppendLine();
        sb.AppendLine("## Technologies");
        sb.AppendLine(string.Join(", ", context.Technologies));
        if (context.ExtensionPoints.Any())
        {
            sb.AppendLine();
            sb.AppendLine("## Extension Points (where to add new code)");
            foreach (var ep in context.ExtensionPoints)
                sb.AppendLine($"- {ep.Layer}: {ep.Project} → package {ep.Namespace}");
        }
        return sb.ToString();
    }

    private PipelineContext BuildPipelineContext(CodebaseAnalysis analysis)
    {
        var context = new PipelineContext();
        foreach (var project in analysis.Projects)
        {
            var detail = new ProjectDetail
            {
                Name = project.Name,
                Path = project.RelativePath,
                RootNamespace = project.RootNamespace
            };
            foreach (var cls in project.Classes.Take(80))
                detail.Classes.Add(new ClassBrief { Name = cls.Name, Namespace = cls.Namespace, Pattern = cls.DetectedPattern });
            foreach (var iface in project.Interfaces.Take(50))
                detail.Interfaces.Add(new InterfaceBrief { Name = iface.Name, Namespace = iface.Namespace });
            context.ProjectDetails.Add(detail);
        }
        context.FullContextText = GenerateGoPipelineContextText(context, analysis);
        context.TokenEstimate = EstimateTokens(context.FullContextText);
        return context;
    }

    private static string GenerateGoPipelineContextText(PipelineContext context, CodebaseAnalysis analysis)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"# Codebase: {analysis.CodebaseName}");
        sb.AppendLine($"Language: Go | Conventions: MixedCaps (exported), camelCase (unexported)");
        var packages = analysis.Projects.SelectMany(p => p.PackageReferences.Select(r => (r.Name, (string?)r.Version)));
        var integrationsText = IntegrationCategorizer.BuildIntegrationsSection(packages);
        if (!string.IsNullOrEmpty(integrationsText))
            sb.AppendLine(integrationsText);
        sb.AppendLine();
        foreach (var project in context.ProjectDetails)
        {
            var proj = analysis.Projects.FirstOrDefault(p => p.Name == project.Name);
            sb.AppendLine($"## Module: {project.Name}");
            sb.AppendLine($"Path: {project.Path}");
            if (proj?.Namespaces.Any() == true)
                sb.AppendLine($"Packages: {string.Join(", ", proj.Namespaces.Take(15))}");
            sb.AppendLine();
            if (project.Interfaces.Any())
            {
                sb.AppendLine("### Interfaces");
                foreach (var iface in project.Interfaces.Take(25))
                    sb.AppendLine($"- {iface.Namespace}.{iface.Name}");
                sb.AppendLine();
            }
            if (project.Classes.Any())
            {
                sb.AppendLine("### Structs / Types");
                foreach (var cls in project.Classes.Take(40))
                {
                    var patternInfo = !string.IsNullOrEmpty(cls.Pattern) ? $" [{cls.Pattern}]" : "";
                    sb.AppendLine($"- {cls.Namespace}.{cls.Name}{patternInfo}");
                }
            }
            sb.AppendLine();
        }
        return sb.ToString();
    }

    private static int EstimateTokens(string text) => text.Length / 4;
}
