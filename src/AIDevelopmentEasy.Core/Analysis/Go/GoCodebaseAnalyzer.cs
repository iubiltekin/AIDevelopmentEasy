using System.Text.RegularExpressions;
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
            context.Projects.Add(new ProjectBrief
            {
                Name = project.Name,
                Type = project.RootPath.Contains("cmd") ? "CLI" : "Library",
                Purpose = "Go module",
                KeyNamespaces = project.Namespaces.Take(3).ToList()
            });
        }
        context.Architecture = new List<string> { "Go modules" };
        context.Technologies = new List<string> { "Go" };
        context.SummaryText = $"# Codebase: {analysis.CodebaseName}\n\n## Go modules\n" + string.Join("\n", analysis.Projects.Select(p => $"- **{p.Name}** ({p.RootPath})"));
        context.TokenEstimate = context.SummaryText.Length / 4;
        return context;
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
            foreach (var cls in project.Classes.Take(50))
                detail.Classes.Add(new ClassBrief { Name = cls.Name, Namespace = cls.Namespace, Pattern = cls.DetectedPattern });
            foreach (var iface in project.Interfaces.Take(30))
                detail.Interfaces.Add(new InterfaceBrief { Name = iface.Name, Namespace = iface.Namespace });
            context.ProjectDetails.Add(detail);
        }
        context.FullContextText = $"# Codebase: {analysis.CodebaseName}\n\n" + string.Join("\n", context.ProjectDetails.Select(d => $"## {d.Name}\nPackages: {string.Join(", ", analysis.Projects.FirstOrDefault(p => p.Name == d.Name)?.Namespaces.Take(5) ?? Array.Empty<string>())}"));
        context.TokenEstimate = context.FullContextText.Length / 4;
        return context;
    }
}
