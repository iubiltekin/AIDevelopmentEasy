using System.Text.RegularExpressions;
using AIDevelopmentEasy.Core.Models;
using Microsoft.Extensions.Logging;

namespace AIDevelopmentEasy.Core.Analysis.Python;

/// <summary>
/// Analyzes Python codebases (pyproject.toml, setup.py, requirements.txt, *.py) and produces a
/// <see cref="CodebaseAnalysis"/> with <see cref="ProjectInfo.LanguageId"/> = "python".
/// </summary>
public class PythonCodebaseAnalyzer : ICodebaseAnalyzer
{
    public string LanguageId => "python";

    private readonly ILogger<PythonCodebaseAnalyzer>? _logger;

    private static readonly Regex ProjectNameRegex = new(@"^\s*name\s*=\s*[""]([^""]+)[""]", RegexOptions.Multiline);
    private static readonly Regex ClassRegex = new(@"^\s*class\s+(\w+)(?:\(([^)]*)\))?\s*:", RegexOptions.Multiline);
    private static readonly Regex DefRegex = new(@"^\s*def\s+(\w+)\s*\(", RegexOptions.Multiline);

    public PythonCodebaseAnalyzer(ILogger<PythonCodebaseAnalyzer>? logger = null)
    {
        _logger = logger;
    }

    public bool CanAnalyze(string codebasePath)
    {
        if (!Directory.Exists(codebasePath))
            return false;
        if (Directory.GetFiles(codebasePath, "pyproject.toml", SearchOption.AllDirectories).Length > 0) return true;
        if (Directory.GetFiles(codebasePath, "setup.py", SearchOption.AllDirectories).Length > 0) return true;
        if (Directory.GetFiles(codebasePath, "requirements.txt", SearchOption.AllDirectories).Length > 0) return true;
        return Directory.GetFiles(codebasePath, "*.py", SearchOption.AllDirectories).Length > 0;
    }

    public async Task<CodebaseAnalysis> AnalyzeAsync(string codebasePath, string codebaseName, CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("[PythonCodebaseAnalyzer] Starting analysis of: {Path}", codebasePath);

        if (!Directory.Exists(codebasePath))
            throw new DirectoryNotFoundException($"Codebase path not found: {codebasePath}");

        var analysis = new CodebaseAnalysis
        {
            CodebaseName = codebaseName,
            CodebasePath = codebasePath,
            AnalyzedAt = DateTime.UtcNow
        };

        var pyprojectFiles = Directory.GetFiles(codebasePath, "pyproject.toml", SearchOption.AllDirectories)
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + ".venv" + Path.DirectorySeparatorChar))
            .ToList();
        var setupFiles = Directory.GetFiles(codebasePath, "setup.py", SearchOption.AllDirectories).ToList();

        if (pyprojectFiles.Count > 0)
        {
            foreach (var pyprojectPath in pyprojectFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var projectDir = Path.GetDirectoryName(pyprojectPath)!;
                var projectInfo = await ParsePythonProjectAsync(projectDir, codebasePath, codebaseName, cancellationToken);
                if (projectInfo != null)
                {
                    projectInfo.LanguageId = LanguageId;
                    projectInfo.RootPath = GetRelativePath(projectDir, codebasePath);
                    analysis.Projects.Add(projectInfo);
                }
            }
        }
        else if (setupFiles.Count > 0)
        {
            foreach (var setupPath in setupFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var projectDir = Path.GetDirectoryName(setupPath)!;
                var projectInfo = await ParsePythonProjectAsync(projectDir, codebasePath, codebaseName, cancellationToken);
                if (projectInfo != null)
                {
                    projectInfo.LanguageId = LanguageId;
                    projectInfo.RootPath = GetRelativePath(projectDir, codebasePath);
                    analysis.Projects.Add(projectInfo);
                }
            }
        }
        else
        {
            var projectInfo = await ParsePythonProjectAsync(codebasePath, codebasePath, codebaseName, cancellationToken);
            if (projectInfo != null)
            {
                projectInfo.LanguageId = LanguageId;
                projectInfo.RootPath = "";
                analysis.Projects.Add(projectInfo);
            }
        }

        _logger?.LogInformation("[PythonCodebaseAnalyzer] Found {Count} project(s)", analysis.Projects.Count);

        analysis.Summary = BuildSummary(analysis);
        analysis.Summary.Languages = new List<string> { LanguageId };
        analysis.Conventions = new CodeConventions { NamingStyle = "snake_case", PrivateFieldPrefix = "_" };
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

    private async Task<ProjectInfo?> ParsePythonProjectAsync(string projectDir, string basePath, string defaultName, CancellationToken cancellationToken)
    {
        var pyprojectPath = Path.Combine(projectDir, "pyproject.toml");
        var setupPath = Path.Combine(projectDir, "setup.py");
        string projectName = defaultName;
        if (File.Exists(pyprojectPath))
        {
            var content = await File.ReadAllTextAsync(pyprojectPath, cancellationToken);
            var m = ProjectNameRegex.Match(content);
            if (m.Success) projectName = m.Groups[1].Value.Trim();
        }

        var projectInfo = new ProjectInfo
        {
            Name = projectName,
            Path = projectDir,
            RelativePath = GetRelativePath(projectDir, basePath),
            ProjectDirectory = GetRelativePath(projectDir, basePath),
            TargetFramework = "python",
            OutputType = "Library",
            RootNamespace = projectName
        };

        var pyFiles = Directory.GetFiles(projectDir, "*.py", SearchOption.AllDirectories)
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + "__pycache__" + Path.DirectorySeparatorChar)
                && !f.Contains(Path.DirectorySeparatorChar + ".venv" + Path.DirectorySeparatorChar))
            .ToList();

        foreach (var pyFile in pyFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var fileContent = await File.ReadAllTextAsync(pyFile, cancellationToken);
                var relativePath = GetRelativePath(pyFile, basePath);
                var moduleName = Path.GetFileNameWithoutExtension(pyFile);
                if (moduleName != "__init__" && !projectInfo.Namespaces.Contains(moduleName))
                    projectInfo.Namespaces.Add(moduleName);

                foreach (Match m in ClassRegex.Matches(fileContent))
                {
                    var baseTypes = m.Groups[2].Success ? m.Groups[2].Value.Split(',').Select(t => t.Trim()).ToList() : new List<string>();
                    projectInfo.Classes.Add(new TypeInfo
                    {
                        Name = m.Groups[1].Value,
                        Namespace = moduleName,
                        FilePath = relativePath,
                        BaseTypes = baseTypes,
                        DetectedPattern = InferPythonPattern(m.Groups[1].Value, fileContent)
                    });
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error parsing Python file: {Path}", pyFile);
            }
        }

        if (projectInfo.Namespaces.Count == 0) projectInfo.Namespaces.Add("main");
        projectInfo.DetectedPatterns = projectInfo.Classes.Select(c => c.DetectedPattern).Where(p => !string.IsNullOrEmpty(p)).Cast<string>().Distinct().ToList();
        return projectInfo;
    }

    private static string? InferPythonPattern(string className, string content)
    {
        if (className.Contains("View") || className.Contains("Controller")) return "Controller";
        if (className.Contains("Service") || className.Contains("Manager")) return "Service";
        if (className.Contains("Repository") || className.Contains("DAO")) return "Repository";
        if (className.EndsWith("Test") || content.Contains("unittest") || content.Contains("pytest")) return "UnitTest";
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
            PrimaryFramework = "python",
            DetectedPatterns = analysis.Projects.SelectMany(p => p.DetectedPatterns).Distinct().ToList(),
            KeyNamespaces = analysis.Projects.SelectMany(p => p.Namespaces).Distinct().Take(10).ToList()
        };
    }

    private RequirementContext BuildRequirementContext(CodebaseAnalysis analysis)
    {
        var context = new RequirementContext();
        foreach (var project in analysis.Projects)
        {
            var projectType = DetectPythonProjectType(project);
            var purpose = InferPythonPurpose(project, projectType);
            context.Projects.Add(new ProjectBrief
            {
                Name = project.Name,
                Type = projectType,
                Purpose = purpose,
                KeyNamespaces = project.Namespaces.Take(5).ToList()
            });
        }
        context.Architecture = DetectPythonArchitecture(analysis);
        context.Technologies = new List<string> { "Python" };
        context.ExtensionPoints = FindPythonExtensionPoints(analysis);
        context.SummaryText = GeneratePythonRequirementSummaryText(context, analysis);
        context.TokenEstimate = EstimateTokens(context.SummaryText);
        return context;
    }

    private static string DetectPythonProjectType(ProjectInfo project)
    {
        if (project.DetectedPatterns.Contains("UnitTest")) return "Tests";
        if (project.Classes.Any(c => c.DetectedPattern == "Controller" || c.DetectedPattern == "View")) return "API/Web";
        if (project.Classes.Any(c => c.DetectedPattern == "Service")) return "Application";
        return "Package";
    }

    private static string InferPythonPurpose(ProjectInfo project, string projectType)
    {
        return projectType switch
        {
            "API/Web" => "Web API or MVC (views, controllers)",
            "Application" => "Business logic and services",
            "Tests" => "Unit and integration tests",
            "Package" => "Python package (modules and classes)",
            _ => "Python project"
        };
    }

    private static List<string> DetectPythonArchitecture(CodebaseAnalysis analysis)
    {
        var layers = new List<string>();
        var patterns = analysis.Projects.SelectMany(p => p.DetectedPatterns).Distinct().ToList();
        if (patterns.Any(p => p == "Controller" || p == "View")) layers.Add("View/Controller Layer");
        if (patterns.Any(p => p == "Service")) layers.Add("Service Layer");
        if (patterns.Any(p => p == "Repository")) layers.Add("Repository/Data Layer");
        if (layers.Count == 0) layers.Add("Module-based package layout");
        return layers;
    }

    private static List<ExtensionPoint> FindPythonExtensionPoints(CodebaseAnalysis analysis)
    {
        var points = new List<ExtensionPoint>();
        foreach (var project in analysis.Projects)
        {
            var controllers = project.Classes.Where(c => c.DetectedPattern == "Controller" || c.DetectedPattern == "View").ToList();
            if (controllers.Any())
            {
                var mod = controllers.First().Namespace;
                points.Add(new ExtensionPoint { Layer = "Views/Controllers", Project = project.Name, Namespace = mod, Pattern = "Controller" });
            }
            var services = project.Classes.Where(c => c.DetectedPattern == "Service").ToList();
            if (services.Any())
            {
                var mod = services.First().Namespace;
                points.Add(new ExtensionPoint { Layer = "Services", Project = project.Name, Namespace = mod, Pattern = "Service" });
            }
            var repos = project.Classes.Where(c => c.DetectedPattern == "Repository").ToList();
            if (repos.Any())
            {
                var mod = repos.First().Namespace;
                points.Add(new ExtensionPoint { Layer = "Repositories", Project = project.Name, Namespace = mod, Pattern = "Repository" });
            }
        }
        return points.Take(10).ToList();
    }

    private static string GeneratePythonRequirementSummaryText(RequirementContext context, CodebaseAnalysis analysis)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"# Codebase: {analysis.CodebaseName}");
        sb.AppendLine();
        sb.AppendLine("## Architecture (Python)");
        foreach (var layer in context.Architecture) sb.AppendLine($"- {layer}");
        sb.AppendLine();
        sb.AppendLine("## Projects / Packages");
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
            sb.AppendLine("## Extension Points (where to add new code)");
            foreach (var ep in context.ExtensionPoints)
                sb.AppendLine($"- {ep.Layer}: {ep.Project} → module {ep.Namespace}");
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
                detail.Classes.Add(new ClassBrief { Name = cls.Name, Namespace = cls.Namespace, Pattern = cls.DetectedPattern, BaseTypes = cls.BaseTypes });
            context.ProjectDetails.Add(detail);
        }
        context.FullContextText = GeneratePythonPipelineContextText(context, analysis);
        context.TokenEstimate = EstimateTokens(context.FullContextText);
        return context;
    }

    private static string GeneratePythonPipelineContextText(PipelineContext context, CodebaseAnalysis analysis)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"# Codebase: {analysis.CodebaseName}");
        sb.AppendLine("Language: Python | Conventions: snake_case (functions, modules), PascalCase not required for classes");
        sb.AppendLine();
        foreach (var project in context.ProjectDetails)
        {
            sb.AppendLine($"## Package: {project.Name}");
            sb.AppendLine($"Path: {project.Path}");
            sb.AppendLine();
            if (project.Classes.Any())
            {
                sb.AppendLine("### Classes");
                foreach (var cls in project.Classes.Take(40))
                {
                    var baseInfo = cls.BaseTypes != null && cls.BaseTypes.Count > 0 ? $"({string.Join(", ", cls.BaseTypes)})" : "";
                    var patternInfo = !string.IsNullOrEmpty(cls.Pattern) ? $" [{cls.Pattern}]" : "";
                    sb.AppendLine($"- {cls.Namespace}.{cls.Name} {baseInfo}{patternInfo}");
                }
            }
            sb.AppendLine();
        }
        return sb.ToString();
    }

    private static int EstimateTokens(string text) => text.Length / 4;
}
