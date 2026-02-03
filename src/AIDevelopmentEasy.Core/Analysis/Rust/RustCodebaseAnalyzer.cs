using System.Text.RegularExpressions;
using AIDevelopmentEasy.Core.Models;
using Microsoft.Extensions.Logging;

namespace AIDevelopmentEasy.Core.Analysis.Rust;

/// <summary>
/// Analyzes Rust codebases (Cargo.toml, *.rs) and produces a <see cref="CodebaseAnalysis"/> with
/// <see cref="ProjectInfo.LanguageId"/> = "rust".
/// </summary>
public class RustCodebaseAnalyzer : ICodebaseAnalyzer
{
    public string LanguageId => "rust";

    private readonly ILogger<RustCodebaseAnalyzer>? _logger;

    private static readonly Regex PackageNameRegex = new(@"^\s*name\s*=\s*[""](\w+)[""]", RegexOptions.Multiline);
    private static readonly Regex ModRegex = new(@"^\s*mod\s+(\w+)", RegexOptions.Multiline);
    private static readonly Regex StructRegex = new(@"struct\s+(\w+)(?:\s*<[^>]+>)?(?:\s*\{[^}]*\})?", RegexOptions.Compiled);
    private static readonly Regex EnumRegex = new(@"enum\s+(\w+)(?:\s*\{[^}]*\})?", RegexOptions.Compiled);
    private static readonly Regex TraitRegex = new(@"trait\s+(\w+)(?:\s*<[^>]+>)?(?:\s*\{[^}]*\})?", RegexOptions.Compiled);
    private static readonly Regex ImplRegex = new(@"impl(?:\s*<[^>]+>)?\s+(?:(\w+)\s+for\s+)?(\w+)", RegexOptions.Compiled);

    public RustCodebaseAnalyzer(ILogger<RustCodebaseAnalyzer>? logger = null)
    {
        _logger = logger;
    }

    public bool CanAnalyze(string codebasePath)
    {
        if (!Directory.Exists(codebasePath))
            return false;
        var cargoFiles = Directory.GetFiles(codebasePath, "Cargo.toml", SearchOption.AllDirectories);
        return cargoFiles.Length > 0;
    }

    public async Task<CodebaseAnalysis> AnalyzeAsync(string codebasePath, string codebaseName, CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("[RustCodebaseAnalyzer] Starting analysis of: {Path}", codebasePath);

        if (!Directory.Exists(codebasePath))
            throw new DirectoryNotFoundException($"Codebase path not found: {codebasePath}");

        var analysis = new CodebaseAnalysis
        {
            CodebaseName = codebaseName,
            CodebasePath = codebasePath,
            AnalyzedAt = DateTime.UtcNow
        };

        var cargoFiles = Directory.GetFiles(codebasePath, "Cargo.toml", SearchOption.AllDirectories)
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + "target" + Path.DirectorySeparatorChar))
            .ToList();

        foreach (var cargoPath in cargoFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var projectInfo = await ParseCargoAsync(cargoPath, codebasePath, cancellationToken);
            if (projectInfo != null)
            {
                projectInfo.LanguageId = LanguageId;
                projectInfo.RootPath = GetRelativePath(Path.GetDirectoryName(cargoPath)!, codebasePath);
                analysis.Projects.Add(projectInfo);
            }
        }

        _logger?.LogInformation("[RustCodebaseAnalyzer] Found {Count} package(s)", analysis.Projects.Count);

        analysis.Summary = BuildSummary(analysis);
        analysis.Summary.Languages = new List<string> { LanguageId };
        analysis.Conventions = new CodeConventions { NamingStyle = "snake_case", PrivateFieldPrefix = "" };
        analysis.RequirementContext = BuildRequirementContext(analysis);
        analysis.PipelineContext = BuildPipelineContext(analysis);

        return analysis;
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

    private async Task<ProjectInfo?> ParseCargoAsync(string cargoPath, string basePath, CancellationToken cancellationToken)
    {
        var projectDir = Path.GetDirectoryName(cargoPath)!;
        var content = await File.ReadAllTextAsync(cargoPath, cancellationToken);

        var nameMatch = PackageNameRegex.Match(content);
        var packageName = nameMatch.Success ? nameMatch.Groups[1].Value : Path.GetFileName(projectDir);

        var projectInfo = new ProjectInfo
        {
            Name = packageName,
            Path = cargoPath,
            RelativePath = GetRelativePath(cargoPath, basePath),
            ProjectDirectory = GetRelativePath(projectDir, basePath),
            TargetFramework = "rust",
            OutputType = "Library",
            RootNamespace = packageName
        };

        var rsFiles = Directory.GetFiles(projectDir, "*.rs", SearchOption.AllDirectories)
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + "target" + Path.DirectorySeparatorChar))
            .ToList();

        foreach (var rsFile in rsFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var fileContent = await File.ReadAllTextAsync(rsFile, cancellationToken);
                var relativePath = GetRelativePath(rsFile, basePath);

                foreach (Match m in StructRegex.Matches(fileContent))
                    projectInfo.Classes.Add(new TypeInfo { Name = m.Groups[1].Value, Namespace = packageName, FilePath = relativePath });
                foreach (Match m in EnumRegex.Matches(fileContent))
                    projectInfo.Classes.Add(new TypeInfo { Name = m.Groups[1].Value, Namespace = packageName, FilePath = relativePath });
                foreach (Match m in TraitRegex.Matches(fileContent))
                    projectInfo.Interfaces.Add(new TypeInfo { Name = m.Groups[1].Value, Namespace = packageName, FilePath = relativePath });
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error parsing Rust file: {Path}", rsFile);
            }
        }

        projectInfo.Namespaces.Add(packageName);
        return projectInfo;
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
            PrimaryFramework = "rust",
            DetectedPatterns = new List<string>(),
            KeyNamespaces = analysis.Projects.SelectMany(p => p.Namespaces).Distinct().Take(10).ToList()
        };
    }

    private RequirementContext BuildRequirementContext(CodebaseAnalysis analysis)
    {
        var context = new RequirementContext();
        foreach (var project in analysis.Projects)
            context.Projects.Add(new ProjectBrief { Name = project.Name, Type = "Crate", Purpose = "Rust package", KeyNamespaces = project.Namespaces.Take(3).ToList() });
        context.Architecture = new List<string> { "Rust crates" };
        context.Technologies = new List<string> { "Rust" };
        context.SummaryText = $"# Codebase: {analysis.CodebaseName}\n\n## Rust crates\n" + string.Join("\n", analysis.Projects.Select(p => $"- **{p.Name}** ({p.RootPath})"));
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
            foreach (var iface in project.Interfaces.Take(30))
                detail.Interfaces.Add(new InterfaceBrief { Name = iface.Name, Namespace = iface.Namespace });
            context.ProjectDetails.Add(detail);
        }
        context.FullContextText = $"# Codebase: {analysis.CodebaseName}\n\nRust crates: " + string.Join(", ", analysis.Projects.Select(p => p.Name));
        context.TokenEstimate = context.FullContextText.Length / 4;
        return context;
    }
}
