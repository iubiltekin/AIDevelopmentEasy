using System.Text.RegularExpressions;
using AIDevelopmentEasy.Core.Analysis;
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
    // [dependencies] crate = "1.0" or crate = { version = "1.0", ... }
    private static readonly Regex CargoDepInlineRegex = new(@"^\s*([a-zA-Z0-9_-]+)\s*=\s*[""]([^""]+)[""]", RegexOptions.Multiline);
    private static readonly Regex CargoDepTableNameRegex = new(@"^\s*([a-zA-Z0-9_-]+)\s*=\s*\{", RegexOptions.Multiline);
    private static readonly Regex CargoDepVersionInLineRegex = new(@"version\s*=\s*[""]([^""]+)[""]", RegexOptions.Compiled);
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

        ParseCargoDependencies(content, projectInfo);

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

    private static void ParseCargoDependencies(string cargoContent, ProjectInfo projectInfo)
    {
        string? currentSection = null;
        foreach (var line in cargoContent.Split(new[] { '\r', '\n' }))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("["))
            {
                var section = trimmed.TrimStart('[').TrimEnd(']').Trim();
                if (section.Equals("dependencies", StringComparison.OrdinalIgnoreCase) ||
                    section.Equals("dev-dependencies", StringComparison.OrdinalIgnoreCase))
                    currentSection = section;
                else
                    currentSection = null;
                continue;
            }
            if (currentSection == null || string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("#")) continue;

            // name = "1.0" or name = { version = "1.0", ... }
            var inline = CargoDepInlineRegex.Match(trimmed);
            if (inline.Success)
            {
                var name = inline.Groups[1].Value;
                var version = inline.Groups[2].Value;
                if (!projectInfo.PackageReferences.Any(p => p.Name == name))
                    projectInfo.PackageReferences.Add(new PackageReference { Name = name, Version = version });
                continue;
            }
            var table = CargoDepTableNameRegex.Match(trimmed);
            if (table.Success)
            {
                var name = table.Groups[1].Value;
                var versionMatch = CargoDepVersionInLineRegex.Match(trimmed);
                var version = versionMatch.Success ? versionMatch.Groups[1].Value : "";
                if (!projectInfo.PackageReferences.Any(p => p.Name == name))
                    projectInfo.PackageReferences.Add(new PackageReference { Name = name, Version = version });
            }
        }
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
        {
            var purpose = "Rust crate (structs, enums, traits)";
            if (project.Classes.Count > 0 || project.Interfaces.Count > 0)
                purpose = $"Crate: {project.Classes.Count} types, {project.Interfaces.Count} traits";
            context.Projects.Add(new ProjectBrief
            {
                Name = project.Name,
                Type = "Crate",
                Purpose = purpose,
                KeyNamespaces = project.Namespaces.Take(5).ToList()
            });
        }
        context.Architecture = DetectRustArchitecture(analysis);
        context.Technologies = new List<string> { "Rust" };
        context.ExtensionPoints = FindRustExtensionPoints(analysis);
        context.SummaryText = GenerateRustRequirementSummaryText(context, analysis);
        context.TokenEstimate = EstimateTokens(context.SummaryText);
        return context;
    }

    private static List<string> DetectRustArchitecture(CodebaseAnalysis analysis)
    {
        var layers = new List<string>();
        var hasStructs = analysis.Projects.Any(p => p.Classes.Any());
        var hasTraits = analysis.Projects.Any(p => p.Interfaces.Any());
        if (hasStructs) layers.Add("Structs (data types)");
        if (hasTraits) layers.Add("Traits (abstractions)");
        layers.Add("Module-based crate layout");
        return layers;
    }

    private static List<ExtensionPoint> FindRustExtensionPoints(CodebaseAnalysis analysis)
    {
        var points = new List<ExtensionPoint>();
        foreach (var project in analysis.Projects)
        {
            if (project.Interfaces.Any())
            {
                var trait = project.Interfaces.First();
                points.Add(new ExtensionPoint { Layer = "Traits", Project = project.Name, Namespace = trait.Namespace, Pattern = "Trait" });
            }
            if (project.Classes.Any())
            {
                var first = project.Classes.First();
                points.Add(new ExtensionPoint { Layer = "Structs/Enums", Project = project.Name, Namespace = first.Namespace, Pattern = "Type" });
            }
        }
        return points.Take(10).ToList();
    }

    private static string GenerateRustRequirementSummaryText(RequirementContext context, CodebaseAnalysis analysis)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"# Codebase: {analysis.CodebaseName}");
        sb.AppendLine();
        sb.AppendLine("## Architecture (Rust)");
        foreach (var layer in context.Architecture) sb.AppendLine($"- {layer}");
        sb.AppendLine();
        sb.AppendLine("## Crates");
        foreach (var project in context.Projects)
        {
            sb.AppendLine($"- **{project.Name}** ({project.Type}): {project.Purpose}");
            if (project.KeyNamespaces.Any())
                sb.AppendLine($"  Modules: {string.Join(", ", project.KeyNamespaces)}");
        }
        sb.AppendLine();
        sb.AppendLine("## Technologies");
        sb.AppendLine(string.Join(", ", context.Technologies));
        if (context.ExtensionPoints.Any())
        {
            sb.AppendLine();
            sb.AppendLine("## Extension Points");
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
            foreach (var cls in project.Classes.Take(80))
                detail.Classes.Add(new ClassBrief { Name = cls.Name, Namespace = cls.Namespace });
            foreach (var iface in project.Interfaces.Take(50))
                detail.Interfaces.Add(new InterfaceBrief { Name = iface.Name, Namespace = iface.Namespace });
            context.ProjectDetails.Add(detail);
        }
        context.FullContextText = GenerateRustPipelineContextText(context, analysis);
        context.TokenEstimate = EstimateTokens(context.FullContextText);
        return context;
    }

    private static string GenerateRustPipelineContextText(PipelineContext context, CodebaseAnalysis analysis)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"# Codebase: {analysis.CodebaseName}");
        sb.AppendLine("Language: Rust | Conventions: snake_case (functions, modules), PascalCase (types, traits)");
        var packages = analysis.Projects.SelectMany(p => p.PackageReferences.Select(r => (r.Name, (string?)r.Version)));
        var integrationsText = IntegrationCategorizer.BuildIntegrationsSection(packages);
        if (!string.IsNullOrEmpty(integrationsText))
            sb.AppendLine(integrationsText);
        sb.AppendLine();
        foreach (var project in context.ProjectDetails)
        {
            sb.AppendLine($"## Crate: {project.Name}");
            sb.AppendLine($"Path: {project.Path}");
            sb.AppendLine();
            if (project.Interfaces.Any())
            {
                sb.AppendLine("### Traits");
                foreach (var t in project.Interfaces.Take(25))
                    sb.AppendLine($"- {t.Namespace}.{t.Name}");
                sb.AppendLine();
            }
            if (project.Classes.Any())
            {
                sb.AppendLine("### Structs / Enums");
                foreach (var c in project.Classes.Take(40))
                    sb.AppendLine($"- {c.Namespace}.{c.Name}");
            }
            sb.AppendLine();
        }
        return sb.ToString();
    }

    private static int EstimateTokens(string text) => text.Length / 4;
}
