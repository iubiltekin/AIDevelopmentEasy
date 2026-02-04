using System.Text.Json;
using System.Text.RegularExpressions;
using AIDevelopmentEasy.Core.Analysis;
using AIDevelopmentEasy.Core.Models;
using Microsoft.Extensions.Logging;

namespace AIDevelopmentEasy.Core.Analysis.Frontend;

/// <summary>
/// Analyzes frontend codebases (package.json with React/Vite/Next etc.) and produces a
/// <see cref="CodebaseAnalysis"/> with <see cref="ProjectInfo.LanguageId"/> = "typescript" and Role = "Frontend".
/// </summary>
public class FrontendCodebaseAnalyzer : ICodebaseAnalyzer
{
    public string LanguageId => "typescript";

    private readonly ILogger<FrontendCodebaseAnalyzer>? _logger;

    private static readonly string[] FrontendIndicators = { "react", "vue", "next", "nuxt", "vite", "angular", "@angular" };
    private static readonly Regex ExportDefaultRegex = new(@"export\s+default\s+function\s+(\w+)|export\s+default\s+(\w+)", RegexOptions.Compiled);
    private static readonly Regex ExportConstComponentRegex = new(@"export\s+const\s+(\w+)\s*=\s*\([^)]*\)\s*=>|export\s+function\s+(\w+)\s*\(", RegexOptions.Compiled);

    public FrontendCodebaseAnalyzer(ILogger<FrontendCodebaseAnalyzer>? logger = null)
    {
        _logger = logger;
    }

    public bool CanAnalyze(string codebasePath)
    {
        if (!Directory.Exists(codebasePath))
            return false;
        var packageJsonFiles = Directory.GetFiles(codebasePath, "package.json", SearchOption.AllDirectories);
        foreach (var path in packageJsonFiles)
        {
            if (IsFrontendPackageJson(path))
                return true;
        }
        return false;
    }

    public async Task<CodebaseAnalysis> AnalyzeAsync(string codebasePath, string codebaseName, CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("[FrontendCodebaseAnalyzer] Starting analysis of: {Path}", codebasePath);

        if (!Directory.Exists(codebasePath))
            throw new DirectoryNotFoundException($"Codebase path not found: {codebasePath}");

        var analysis = new CodebaseAnalysis
        {
            CodebaseName = codebaseName,
            CodebasePath = codebasePath,
            AnalyzedAt = DateTime.UtcNow
        };

        var packageJsonFiles = Directory.GetFiles(codebasePath, "package.json", SearchOption.AllDirectories)
            .Where(IsFrontendPackageJson)
            .ToList();

        foreach (var pkgPath in packageJsonFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var projectInfo = await ParsePackageJsonAsync(pkgPath, codebasePath, cancellationToken);
            if (projectInfo != null)
            {
                projectInfo.LanguageId = LanguageId;
                projectInfo.RootPath = GetRelativePath(Path.GetDirectoryName(pkgPath)!, codebasePath);
                projectInfo.Role = "Frontend";
                analysis.Projects.Add(projectInfo);
            }
        }

        _logger?.LogInformation("[FrontendCodebaseAnalyzer] Found {Count} frontend project(s)", analysis.Projects.Count);

        analysis.Summary = BuildSummary(analysis);
        analysis.Summary.Languages = new List<string> { LanguageId };
        analysis.Conventions = new CodeConventions { NamingStyle = "camelCase", PrivateFieldPrefix = "" };
        analysis.RequirementContext = BuildRequirementContext(analysis);
        analysis.PipelineContext = BuildPipelineContext(analysis);

        return analysis;
    }

    private static bool IsFrontendPackageJson(string path)
    {
        try
        {
            var json = File.ReadAllText(path);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("dependencies", out var deps))
            {
                var depStr = deps.GetRawText().ToLowerInvariant();
                if (FrontendIndicators.Any(ind => depStr.Contains($"\"{ind}\""))) return true;
            }
            if (root.TryGetProperty("devDependencies", out var devDeps))
            {
                var devStr = devDeps.GetRawText().ToLowerInvariant();
                if (FrontendIndicators.Any(ind => devStr.Contains($"\"{ind}\""))) return true;
            }
        }
        catch { /* ignore */ }
        return false;
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

    private async Task<ProjectInfo?> ParsePackageJsonAsync(string packageJsonPath, string basePath, CancellationToken cancellationToken)
    {
        var projectDir = Path.GetDirectoryName(packageJsonPath)!;
        string projectName = Path.GetFileName(projectDir);

        var projectInfo = new ProjectInfo
        {
            Name = projectName,
            Path = packageJsonPath,
            RelativePath = GetRelativePath(packageJsonPath, basePath),
            ProjectDirectory = GetRelativePath(projectDir, basePath),
            TargetFramework = "Node/TypeScript",
            OutputType = "Library",
            RootNamespace = projectName
        };

        try
        {
            var json = await File.ReadAllTextAsync(packageJsonPath, cancellationToken);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("name", out var nameEl))
                projectInfo.Name = nameEl.GetString() ?? projectName;
            AddPackageJsonDependencies(root, projectInfo);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error reading package.json: {Path}", packageJsonPath);
        }

        var srcDir = Path.Combine(projectDir, "src");
        var searchDir = Directory.Exists(srcDir) ? srcDir : projectDir;
        var tsxFiles = Directory.GetFiles(searchDir, "*.tsx", SearchOption.AllDirectories);
        var tsFiles = Directory.GetFiles(searchDir, "*.ts", SearchOption.AllDirectories);
        foreach (var f in tsxFiles.Concat(tsFiles))
        {
            var rel = GetRelativePath(f, basePath);
            var fileName = Path.GetFileNameWithoutExtension(f);
            var dirRel = Path.GetDirectoryName(rel)?.Replace('\\', '/') ?? "";
            var namespaceFolder = string.IsNullOrEmpty(dirRel) ? "root" : dirRel;
            if (!projectInfo.Namespaces.Contains(namespaceFolder))
                projectInfo.Namespaces.Add(namespaceFolder);
            var componentName = TryGetComponentName(f);
            projectInfo.Classes.Add(new TypeInfo
            {
                Name = componentName ?? fileName,
                Namespace = namespaceFolder,
                FilePath = rel,
                DetectedPattern = InferFrontendPattern(namespaceFolder, fileName)
            });
        }

        if (projectInfo.Namespaces.Count == 0) projectInfo.Namespaces.Add("app");
        projectInfo.DetectedPatterns = new List<string> { "Frontend" };
        return projectInfo;
    }

    private static void AddPackageJsonDependencies(JsonElement root, ProjectInfo projectInfo)
    {
        foreach (var key in new[] { "dependencies", "devDependencies" })
        {
            if (!root.TryGetProperty(key, out var deps) || deps.ValueKind != JsonValueKind.Object)
                continue;
            foreach (var prop in deps.EnumerateObject())
            {
                var version = prop.Value.GetString() ?? "";
                if (!projectInfo.PackageReferences.Any(p => p.Name == prop.Name))
                    projectInfo.PackageReferences.Add(new PackageReference { Name = prop.Name, Version = version });
            }
        }
    }

    private static string? TryGetComponentName(string filePath)
    {
        try
        {
            var content = File.ReadAllText(filePath);
            var m = ExportDefaultRegex.Match(content);
            if (m.Success) return m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value;
            m = ExportConstComponentRegex.Match(content);
            if (m.Success) return m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value;
        }
        catch { /* ignore */ }
        return null;
    }

    private static string? InferFrontendPattern(string namespaceFolder, string fileName)
    {
        var lower = (namespaceFolder + "/" + fileName).ToLowerInvariant();
        if (lower.Contains("page") || lower.Contains("pages")) return "Page";
        if (lower.Contains("component")) return "Component";
        if (lower.Contains("hook")) return "Hook";
        if (lower.Contains("layout")) return "Layout";
        return null;
    }

    private CodebaseSummary BuildSummary(CodebaseAnalysis analysis)
    {
        return new CodebaseSummary
        {
            TotalSolutions = 0,
            TotalProjects = analysis.Projects.Count,
            TotalClasses = analysis.Projects.Sum(p => p.Classes.Count),
            TotalInterfaces = 0,
            TotalFiles = analysis.Projects.Sum(p => p.Classes.Select(c => c.FilePath).Distinct().Count()),
            PrimaryFramework = "Node/TypeScript",
            DetectedPatterns = new List<string> { "Frontend" },
            KeyNamespaces = analysis.Projects.SelectMany(p => p.Namespaces).Distinct().Take(10).ToList()
        };
    }

    private RequirementContext BuildRequirementContext(CodebaseAnalysis analysis)
    {
        var context = new RequirementContext();
        foreach (var project in analysis.Projects)
        {
            var purpose = "React/TypeScript frontend application";
            if (project.Classes.Any())
                purpose = $"{project.Classes.Count} components/pages, folders: {string.Join(", ", project.Namespaces.Take(5))}";
            context.Projects.Add(new ProjectBrief
            {
                Name = project.Name,
                Type = "Frontend",
                Purpose = purpose,
                KeyNamespaces = project.Namespaces.Take(5).ToList()
            });
        }
        context.Architecture = DetectFrontendArchitecture(analysis);
        context.Technologies = DetectFrontendTechnologies(analysis);
        context.ExtensionPoints = FindFrontendExtensionPoints(analysis);
        context.SummaryText = GenerateFrontendRequirementSummaryText(context, analysis);
        context.TokenEstimate = EstimateTokens(context.SummaryText);
        return context;
    }

    private static List<string> DetectFrontendArchitecture(CodebaseAnalysis analysis)
    {
        var layers = new List<string> { "Component-based UI" };
        var hasPages = analysis.Projects.Any(p => p.Classes.Any(c => c.DetectedPattern == "Page"));
        var hasHooks = analysis.Projects.Any(p => p.Classes.Any(c => c.DetectedPattern == "Hook"));
        if (hasPages) layers.Add("Pages/Routes");
        if (hasHooks) layers.Add("Custom Hooks");
        return layers;
    }

    private static List<string> DetectFrontendTechnologies(CodebaseAnalysis analysis)
    {
        var tech = new HashSet<string> { "TypeScript" };
        var firstProject = analysis.Projects.FirstOrDefault();
        if (firstProject?.Path != null && File.Exists(firstProject.Path))
        {
            try
            {
                var json = File.ReadAllText(firstProject.Path);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                foreach (var key in new[] { "dependencies", "devDependencies" })
                {
                    if (!root.TryGetProperty(key, out var deps)) continue;
                    foreach (var prop in deps.EnumerateObject())
                    {
                        var name = prop.Name.ToLowerInvariant();
                        if (name.Contains("react") && !name.Contains("dom")) tech.Add("React");
                        else if (name == "vue" || name.StartsWith("vue-")) tech.Add("Vue");
                        else if (name == "next") tech.Add("Next.js");
                        else if (name == "vite") tech.Add("Vite");
                        else if (name == "angular" || name.StartsWith("@angular")) tech.Add("Angular");
                    }
                }
            }
            catch { /* ignore */ }
        }
        if (!tech.Contains("React") && !tech.Contains("Vue") && !tech.Contains("Angular")) tech.Add("React");
        return tech.ToList();
    }

    private static List<ExtensionPoint> FindFrontendExtensionPoints(CodebaseAnalysis analysis)
    {
        var points = new List<ExtensionPoint>();
        foreach (var project in analysis.Projects)
        {
            var pages = project.Classes.Where(c => c.DetectedPattern == "Page").ToList();
            if (pages.Any())
            {
                var ns = pages.First().Namespace;
                points.Add(new ExtensionPoint { Layer = "Pages", Project = project.Name, Namespace = ns, Pattern = "Page" });
            }
            var components = project.Classes.Where(c => c.DetectedPattern == "Component" || (c.DetectedPattern == null && c.Namespace.Contains("component"))).ToList();
            if (components.Any())
            {
                var ns = components.First().Namespace;
                points.Add(new ExtensionPoint { Layer = "Components", Project = project.Name, Namespace = ns, Pattern = "Component" });
            }
            if (!points.Any(p => p.Project == project.Name))
                points.Add(new ExtensionPoint { Layer = "UI", Project = project.Name, Namespace = project.Namespaces.FirstOrDefault() ?? "src", Pattern = "Frontend" });
        }
        return points.Take(10).ToList();
    }

    private static string GenerateFrontendRequirementSummaryText(RequirementContext context, CodebaseAnalysis analysis)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"# Codebase: {analysis.CodebaseName}");
        sb.AppendLine();
        sb.AppendLine("## Architecture (Frontend)");
        foreach (var layer in context.Architecture) sb.AppendLine($"- {layer}");
        sb.AppendLine();
        sb.AppendLine("## Projects");
        foreach (var project in context.Projects)
        {
            sb.AppendLine($"- **{project.Name}** ({project.Type}): {project.Purpose}");
            if (project.KeyNamespaces.Any())
                sb.AppendLine($"  Folders: {string.Join(", ", project.KeyNamespaces)}");
        }
        sb.AppendLine();
        sb.AppendLine("## Technologies");
        sb.AppendLine(string.Join(", ", context.Technologies));
        if (context.ExtensionPoints.Any())
        {
            sb.AppendLine();
            sb.AppendLine("## Extension Points (where to add new code)");
            foreach (var ep in context.ExtensionPoints)
                sb.AppendLine($"- {ep.Layer}: {ep.Project} → {ep.Namespace}");
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
            foreach (var cls in project.Classes.Take(100))
                detail.Classes.Add(new ClassBrief { Name = cls.Name, Namespace = cls.Namespace, Pattern = cls.DetectedPattern });
            context.ProjectDetails.Add(detail);
        }
        context.FullContextText = GenerateFrontendPipelineContextText(context, analysis);
        context.TokenEstimate = EstimateTokens(context.FullContextText);
        return context;
    }

    private static string GenerateFrontendPipelineContextText(PipelineContext context, CodebaseAnalysis analysis)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"# Codebase: {analysis.CodebaseName}");
        sb.AppendLine("Language: TypeScript/React | Conventions: camelCase (functions, variables), PascalCase (components)");
        var packages = analysis.Projects.SelectMany(p => p.PackageReferences.Select(r => (r.Name, (string?)r.Version)));
        var integrationsText = IntegrationCategorizer.BuildIntegrationsSection(packages);
        if (!string.IsNullOrEmpty(integrationsText))
            sb.AppendLine(integrationsText);
        sb.AppendLine();
        foreach (var project in context.ProjectDetails)
        {
            var proj = analysis.Projects.FirstOrDefault(p => p.Name == project.Name);
            sb.AppendLine($"## Project: {project.Name}");
            sb.AppendLine($"Path: {project.Path}");
            if (proj?.Namespaces.Any() == true)
                sb.AppendLine($"Folders: {string.Join(", ", proj.Namespaces.Take(15))}");
            sb.AppendLine();
            var byFolder = project.Classes.GroupBy(c => c.Namespace).OrderBy(g => g.Key);
            foreach (var group in byFolder)
            {
                sb.AppendLine($"### {group.Key}");
                foreach (var cls in group.Take(30))
                {
                    var patternInfo = !string.IsNullOrEmpty(cls.Pattern) ? $" [{cls.Pattern}]" : "";
                    sb.AppendLine($"- {cls.Name}{patternInfo}");
                }
                sb.AppendLine();
            }
        }
        return sb.ToString();
    }

    private static int EstimateTokens(string text) => text.Length / 4;
}
