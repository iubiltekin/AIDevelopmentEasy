using System.Text.Json;
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

        try
        {
            var json = await File.ReadAllTextAsync(packageJsonPath, cancellationToken);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("name", out var nameEl))
                projectName = nameEl.GetString() ?? projectName;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error reading package.json: {Path}", packageJsonPath);
        }

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

        var srcDir = Path.Combine(projectDir, "src");
        if (Directory.Exists(srcDir))
        {
            var tsxFiles = Directory.GetFiles(srcDir, "*.tsx", SearchOption.AllDirectories);
            var tsFiles = Directory.GetFiles(srcDir, "*.ts", SearchOption.AllDirectories);
            foreach (var f in tsxFiles.Concat(tsFiles))
            {
                var rel = GetRelativePath(f, basePath);
                var fileName = Path.GetFileNameWithoutExtension(f);
                projectInfo.Namespaces.Add(fileName);
                projectInfo.Classes.Add(new TypeInfo { Name = fileName, Namespace = "src", FilePath = rel });
            }
        }

        if (projectInfo.Namespaces.Count == 0) projectInfo.Namespaces.Add("app");
        projectInfo.DetectedPatterns = new List<string> { "Frontend" };
        return projectInfo;
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
            context.Projects.Add(new ProjectBrief { Name = project.Name, Type = "Frontend", Purpose = "React/TypeScript app", KeyNamespaces = project.Namespaces.Take(3).ToList() });
        context.Architecture = new List<string> { "Frontend" };
        context.Technologies = new List<string> { "TypeScript", "React" };
        context.SummaryText = $"# Codebase: {analysis.CodebaseName}\n\n## Frontend\n" + string.Join("\n", analysis.Projects.Select(p => $"- **{p.Name}** ({p.RootPath})"));
        context.TokenEstimate = context.SummaryText.Length / 4;
        return context;
    }

    private PipelineContext BuildPipelineContext(CodebaseAnalysis analysis)
    {
        var context = new PipelineContext();
        foreach (var project in analysis.Projects)
        {
            var detail = new ProjectDetail { Name = project.Name, Path = project.RelativePath, RootNamespace = project.RootNamespace };
            foreach (var cls in project.Classes.Take(50))
                detail.Classes.Add(new ClassBrief { Name = cls.Name, Namespace = cls.Namespace });
            context.ProjectDetails.Add(detail);
        }
        context.FullContextText = $"# Codebase: {analysis.CodebaseName}\n\nFrontend: " + string.Join(", ", analysis.Projects.Select(p => p.Name));
        context.TokenEstimate = context.FullContextText.Length / 4;
        return context;
    }
}
