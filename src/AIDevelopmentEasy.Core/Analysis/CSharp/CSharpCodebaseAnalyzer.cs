using System.Text.RegularExpressions;
using System.Xml.Linq;
using AIDevelopmentEasy.Core.Analysis;
using AIDevelopmentEasy.Core.Models;
using Microsoft.Extensions.Logging;

namespace AIDevelopmentEasy.Core.Analysis.CSharp;

/// <summary>
/// Analyzes C# codebases (.sln, .csproj, .cs) and produces a <see cref="CodebaseAnalysis"/> with
/// <see cref="ProjectInfo.LanguageId"/> = "csharp".
/// </summary>
public class CSharpCodebaseAnalyzer : ICodebaseAnalyzer
{
    public string LanguageId => "csharp";

    private readonly ILogger<CSharpCodebaseAnalyzer>? _logger;

    private static readonly Regex NamespaceRegex = new(@"namespace\s+([\w.]+)", RegexOptions.Compiled);
    private static readonly Regex ClassRegex = new(@"(public|internal|private|protected)?\s*(abstract|static|sealed|partial)?\s*class\s+(\w+)(?:\s*<[^>]+>)?(?:\s*:\s*([^{]+))?", RegexOptions.Compiled);
    private static readonly Regex InterfaceRegex = new(@"(public|internal|private|protected)?\s*interface\s+(I\w+)(?:\s*<[^>]+>)?(?:\s*:\s*([^{]+))?", RegexOptions.Compiled);
    private static readonly Regex MethodRegex = new(@"(public|private|protected|internal)?\s*(static|virtual|override|abstract|async)?\s*([\w<>\[\],\s\?]+)\s+(\w+)\s*\(([^)]*)\)", RegexOptions.Compiled);
    private static readonly Regex PropertyRegex = new(@"(public|private|protected|internal)?\s*(static|virtual|override|abstract)?\s*([\w<>\[\],\s\?]+)\s+(\w+)\s*{\s*(get|set)", RegexOptions.Compiled);
    private static readonly Regex FieldRegex = new(@"(private|protected|internal)?\s*(readonly|static|const)?\s*([\w<>\[\],\s\?]+)\s+(_\w+|\w+)\s*[;=]", RegexOptions.Compiled);

    private static readonly Dictionary<string, string[]> PatternKeywords = new()
    {
        ["Repository"] = new[] { "Repository", "IRepository", "DbContext", "DbSet", "Entity" },
        ["Service"] = new[] { "Service", "IService", "Manager", "Handler" },
        ["Helper"] = new[] { "Helper", "Helpers", "Utils", "Utility" },
        ["Extension"] = new[] { "Extensions", "this " },
        ["Factory"] = new[] { "Factory", "IFactory", "Create" },
        ["Controller"] = new[] { "Controller", "ApiController", "[HttpGet]", "[HttpPost]" },
        ["UnitTest"] = new[] { "[Test]", "[TestMethod]", "[Fact]", "[TestFixture]", "[TestClass]" },
        ["CQRS"] = new[] { "Command", "Query", "Handler", "IRequest", "IRequestHandler" },
        ["DependencyInjection"] = new[] { "AddScoped", "AddSingleton", "AddTransient", "IServiceCollection" }
    };

    public CSharpCodebaseAnalyzer(ILogger<CSharpCodebaseAnalyzer>? logger = null)
    {
        _logger = logger;
    }

    public bool CanAnalyze(string codebasePath)
    {
        if (!Directory.Exists(codebasePath))
            return false;
        var hasSln = Directory.GetFiles(codebasePath, "*.sln", SearchOption.AllDirectories).Length > 0;
        var hasCsproj = Directory.GetFiles(codebasePath, "*.csproj", SearchOption.AllDirectories).Length > 0;
        return hasSln || hasCsproj;
    }

    public async Task<CodebaseAnalysis> AnalyzeAsync(string codebasePath, string codebaseName, CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("[CSharpCodebaseAnalyzer] Starting analysis of: {Path}", codebasePath);

        if (!Directory.Exists(codebasePath))
            throw new DirectoryNotFoundException($"Codebase path not found: {codebasePath}");

        var analysis = new CodebaseAnalysis
        {
            CodebaseName = codebaseName,
            CodebasePath = codebasePath,
            AnalyzedAt = DateTime.UtcNow
        };

        var solutionFiles = Directory.GetFiles(codebasePath, "*.sln", SearchOption.AllDirectories);
        foreach (var slnFile in solutionFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var solutionInfo = await ParseSolutionFileAsync(slnFile, codebasePath);
            analysis.Solutions.Add(solutionInfo);
        }

        _logger?.LogInformation("[CSharpCodebaseAnalyzer] Found {Count} solution(s)", analysis.Solutions.Count);

        var projectFiles = Directory.GetFiles(codebasePath, "*.csproj", SearchOption.AllDirectories);
        foreach (var projFile in projectFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var projectInfo = await ParseProjectFileAsync(projFile, codebasePath, cancellationToken);
            projectInfo.LanguageId = LanguageId;
            projectInfo.RootPath = projectInfo.ProjectDirectory;
            analysis.Projects.Add(projectInfo);
        }

        _logger?.LogInformation("[CSharpCodebaseAnalyzer] Found {Count} project(s)", analysis.Projects.Count);

        analysis.Summary = BuildSummary(analysis);
        analysis.Summary.Languages = new List<string> { LanguageId };
        analysis.Conventions = DetectConventions(analysis);
        analysis.RequirementContext = BuildRequirementContext(analysis);
        analysis.PipelineContext = BuildPipelineContext(analysis);

        _logger?.LogInformation("[CSharpCodebaseAnalyzer] Analysis complete: {Classes} classes, {Interfaces} interfaces",
            analysis.Summary.TotalClasses, analysis.Summary.TotalInterfaces);

        return analysis;
    }

    private RequirementContext BuildRequirementContext(CodebaseAnalysis analysis)
    {
        var context = new RequirementContext();
        foreach (var project in analysis.Projects)
        {
            var projectType = DetectProjectType(project);
            var purpose = InferProjectPurpose(project, projectType);
            context.Projects.Add(new ProjectBrief
            {
                Name = project.Name,
                Type = projectType,
                Purpose = purpose,
                KeyNamespaces = project.Namespaces.Take(3).ToList()
            });
        }
        context.Architecture = DetectArchitectureLayers(analysis);
        context.Technologies = DetectTechnologies(analysis);
        context.ExtensionPoints = FindExtensionPoints(analysis);
        context.SummaryText = GenerateRequirementSummaryText(context, analysis);
        context.TokenEstimate = EstimateTokens(context.SummaryText);
        return context;
    }

    private PipelineContext BuildPipelineContext(CodebaseAnalysis analysis)
    {
        var context = new PipelineContext();
        foreach (var project in analysis.Projects.Where(p => !p.IsTestProject))
        {
            var detail = new ProjectDetail
            {
                Name = project.Name,
                Path = project.RelativePath,
                RootNamespace = project.RootNamespace
            };
            foreach (var iface in project.Interfaces.Take(50))
            {
                var methods = iface.Members
                    .Where(m => m.Kind == "Method")
                    .Select(m => $"{m.ReturnType} {m.Name}({string.Join(", ", m.Parameters)})")
                    .Take(10)
                    .ToList();
                detail.Interfaces.Add(new InterfaceBrief
                {
                    Name = iface.Name,
                    Namespace = iface.Namespace,
                    Methods = methods
                });
            }
            foreach (var cls in project.Classes.Take(100))
            {
                var publicMethods = cls.Members
                    .Where(m => m.Kind == "Method" && m.Modifiers.Contains("public"))
                    .Select(m => $"{m.ReturnType} {m.Name}({string.Join(", ", m.Parameters)})")
                    .Take(10)
                    .ToList();
                detail.Classes.Add(new ClassBrief
                {
                    Name = cls.Name,
                    Namespace = cls.Namespace,
                    BaseTypes = cls.BaseTypes,
                    Pattern = cls.DetectedPattern,
                    PublicMethods = publicMethods
                });
            }
            context.ProjectDetails.Add(detail);
        }
        context.FullContextText = GeneratePipelineContextText(context, analysis);
        context.TokenEstimate = EstimateTokens(context.FullContextText);
        return context;
    }

    private string DetectProjectType(ProjectInfo project)
    {
        if (project.IsTestProject) return "Tests";
        if (project.Name.EndsWith(".Api") || project.Name.EndsWith(".Web") || project.DetectedPatterns.Contains("Controller")) return "API";
        if (project.Name.EndsWith(".Core") || project.Name.EndsWith(".Domain")) return "Core";
        if (project.Name.EndsWith(".Infrastructure") || project.Name.EndsWith(".Data")) return "Infrastructure";
        if (project.OutputType == "Exe") return "Console";
        return "Library";
    }

    private string InferProjectPurpose(ProjectInfo project, string projectType)
    {
        return projectType switch
        {
            "API" => "REST API endpoints and web interface",
            "Core" => "Core business logic and domain models",
            "Infrastructure" => "Data access and external integrations",
            "Tests" => "Unit and integration tests",
            "Console" => "Console application entry point",
            "Library" => $"Shared library ({string.Join(", ", project.DetectedPatterns.Take(2))})",
            _ => "General purpose"
        };
    }

    private List<string> DetectArchitectureLayers(CodebaseAnalysis analysis)
    {
        var layers = new List<string>();
        var patterns = analysis.Projects.SelectMany(p => p.DetectedPatterns).Distinct().ToList();
        if (patterns.Any(p => p.Contains("Controller"))) layers.Add("Web/API Layer");
        if (patterns.Any(p => p.Contains("Service"))) layers.Add("Service Layer");
        if (patterns.Any(p => p.Contains("Repository"))) layers.Add("Repository/Data Layer");
        if (patterns.Any(p => p.Contains("CQRS"))) layers.Add("CQRS Pattern");
        if (patterns.Any(p => p.Contains("DependencyInjection"))) layers.Add("Dependency Injection");
        if (layers.Count == 0) layers.Add("Monolithic Structure");
        return layers;
    }

    private List<string> DetectTechnologies(CodebaseAnalysis analysis)
    {
        var tech = new HashSet<string>();
        foreach (var project in analysis.Projects)
        {
            if (!string.IsNullOrEmpty(project.TargetFramework))
            {
                if (project.TargetFramework.Contains("net8") || project.TargetFramework.Contains("net7") || project.TargetFramework.Contains("net6"))
                    tech.Add(".NET " + project.TargetFramework.Replace("net", ""));
                else if (project.TargetFramework.Contains("netstandard"))
                    tech.Add(".NET Standard");
                else if (project.TargetFramework.Contains("v4"))
                    tech.Add(".NET Framework");
            }
            foreach (var pkg in project.PackageReferences)
            {
                if (pkg.Name.Contains("EntityFramework")) tech.Add("Entity Framework");
                if (pkg.Name.Contains("Dapper")) tech.Add("Dapper");
                if (pkg.Name.Contains("SignalR")) tech.Add("SignalR");
                if (pkg.Name.Contains("Serilog")) tech.Add("Serilog");
                if (pkg.Name.Contains("AutoMapper")) tech.Add("AutoMapper");
                if (pkg.Name.Contains("FluentValidation")) tech.Add("FluentValidation");
                if (pkg.Name.Contains("MediatR")) tech.Add("MediatR");
                if (pkg.Name.Contains("Swagger") || pkg.Name.Contains("OpenApi")) tech.Add("OpenAPI/Swagger");
            }
        }
        if (!string.IsNullOrEmpty(analysis.Conventions.TestFramework))
            tech.Add(analysis.Conventions.TestFramework);
        return tech.Take(10).ToList();
    }

    private List<ExtensionPoint> FindExtensionPoints(CodebaseAnalysis analysis)
    {
        var points = new List<ExtensionPoint>();
        foreach (var project in analysis.Projects.Where(p => !p.IsTestProject))
        {
            if (project.Classes.Any(c => c.DetectedPattern == "Controller"))
            {
                var controllerNs = project.Classes.Where(c => c.DetectedPattern == "Controller").Select(c => c.Namespace).FirstOrDefault();
                if (!string.IsNullOrEmpty(controllerNs))
                    points.Add(new ExtensionPoint { Layer = "Controllers", Project = project.Name, Namespace = controllerNs, Pattern = "Controller" });
            }
            if (project.Classes.Any(c => c.DetectedPattern == "Service") || project.Interfaces.Any(i => i.Name.StartsWith("I") && i.Name.Contains("Service")))
            {
                var serviceNs = project.Classes.Where(c => c.DetectedPattern == "Service").Select(c => c.Namespace).FirstOrDefault()
                    ?? project.Interfaces.Where(i => i.Name.Contains("Service")).Select(i => i.Namespace).FirstOrDefault();
                if (!string.IsNullOrEmpty(serviceNs))
                    points.Add(new ExtensionPoint { Layer = "Services", Project = project.Name, Namespace = serviceNs, Pattern = "Service" });
            }
            if (project.Classes.Any(c => c.DetectedPattern == "Repository") || project.Interfaces.Any(i => i.Name.Contains("Repository")))
            {
                var repoNs = project.Classes.Where(c => c.DetectedPattern == "Repository").Select(c => c.Namespace).FirstOrDefault()
                    ?? project.Interfaces.Where(i => i.Name.Contains("Repository")).Select(i => i.Namespace).FirstOrDefault();
                if (!string.IsNullOrEmpty(repoNs))
                    points.Add(new ExtensionPoint { Layer = "Repositories", Project = project.Name, Namespace = repoNs, Pattern = "Repository" });
            }
        }
        return points.Take(10).ToList();
    }

    private string GenerateRequirementSummaryText(RequirementContext context, CodebaseAnalysis analysis)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"# Codebase: {analysis.CodebaseName}");
        sb.AppendLine();
        sb.AppendLine("## Architecture");
        foreach (var layer in context.Architecture) sb.AppendLine($"- {layer}");
        sb.AppendLine();
        sb.AppendLine("## Projects");
        foreach (var project in context.Projects)
        {
            sb.AppendLine($"- **{project.Name}** ({project.Type}): {project.Purpose}");
            if (project.KeyNamespaces.Any())
                sb.AppendLine($"  Namespaces: {string.Join(", ", project.KeyNamespaces)}");
        }
        sb.AppendLine();
        sb.AppendLine("## Technologies");
        sb.AppendLine(string.Join(", ", context.Technologies));
        sb.AppendLine();
        if (context.ExtensionPoints.Any())
        {
            sb.AppendLine("## Extension Points (where to add new code)");
            foreach (var ep in context.ExtensionPoints)
                sb.AppendLine($"- {ep.Layer}: {ep.Project} → {ep.Namespace}");
        }
        return sb.ToString();
    }

    private string GeneratePipelineContextText(PipelineContext context, CodebaseAnalysis analysis)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"# Codebase: {analysis.CodebaseName}");
        sb.AppendLine($"Framework: {analysis.Summary.PrimaryFramework}");
        sb.AppendLine($"Patterns: {string.Join(", ", analysis.Summary.DetectedPatterns)}");
        sb.AppendLine();

        var packages = analysis.Projects.SelectMany(p => p.PackageReferences.Select(r => (r.Name, (string?)r.Version)));
        var integrationsText = IntegrationCategorizer.BuildIntegrationsSection(packages);
        if (!string.IsNullOrEmpty(integrationsText))
            sb.AppendLine(integrationsText);

        sb.AppendLine("## Conventions");
        sb.AppendLine($"- Naming: {analysis.Conventions.NamingStyle}");
        sb.AppendLine($"- Private field prefix: {analysis.Conventions.PrivateFieldPrefix}");
        sb.AppendLine($"- Async suffix: {(analysis.Conventions.UsesAsyncSuffix ? "Yes" : "No")}");
        sb.AppendLine();
        foreach (var project in context.ProjectDetails)
        {
            sb.AppendLine($"## Project: {project.Name}");
            sb.AppendLine($"Namespace: {project.RootNamespace}");
            sb.AppendLine();
            if (project.Interfaces.Any())
            {
                sb.AppendLine("### Interfaces");
                foreach (var iface in project.Interfaces.Take(20))
                {
                    sb.AppendLine($"- {iface.Namespace}.{iface.Name}");
                    foreach (var method in iface.Methods.Take(5)) sb.AppendLine($"    {method}");
                }
                sb.AppendLine();
            }
            if (project.Classes.Any())
            {
                sb.AppendLine("### Classes");
                foreach (var cls in project.Classes.Take(30))
                {
                    var baseInfo = cls.BaseTypes.Any() ? $" : {string.Join(", ", cls.BaseTypes)}" : "";
                    var patternInfo = !string.IsNullOrEmpty(cls.Pattern) ? $" [{cls.Pattern}]" : "";
                    sb.AppendLine($"- {cls.Namespace}.{cls.Name}{baseInfo}{patternInfo}");
                }
                sb.AppendLine();
            }
        }
        return sb.ToString();
    }

    private static int EstimateTokens(string text) => text.Length / 4;

    private async Task<SolutionInfo> ParseSolutionFileAsync(string slnPath, string basePath)
    {
        var content = await File.ReadAllTextAsync(slnPath);
        var solutionInfo = new SolutionInfo
        {
            Name = Path.GetFileNameWithoutExtension(slnPath),
            Path = GetRelativePath(slnPath, basePath)
        };
        var projectRegex = new Regex(@"Project\(""\{[^}]+\}""\)\s*=\s*""([^""]+)"",\s*""([^""]+)""");
        foreach (Match match in projectRegex.Matches(content))
        {
            var projectPath = match.Groups[2].Value;
            if (projectPath.EndsWith(".csproj"))
                solutionInfo.ProjectReferences.Add(match.Groups[1].Value);
        }
        return solutionInfo;
    }

    private async Task<ProjectInfo> ParseProjectFileAsync(string projPath, string basePath, CancellationToken cancellationToken)
    {
        var projectDir = Path.GetDirectoryName(projPath)!;
        var projectInfo = new ProjectInfo
        {
            Name = Path.GetFileNameWithoutExtension(projPath),
            Path = projPath,
            RelativePath = GetRelativePath(projPath, basePath),
            ProjectDirectory = GetRelativePath(projectDir, basePath)
        };

        try
        {
            var doc = XDocument.Load(projPath);
            var ns = doc.Root?.Name.Namespace ?? XNamespace.None;
            projectInfo.TargetFramework = doc.Descendants(ns + "TargetFramework").FirstOrDefault()?.Value
                ?? doc.Descendants(ns + "TargetFrameworkVersion").FirstOrDefault()?.Value ?? "Unknown";
            projectInfo.OutputType = doc.Descendants(ns + "OutputType").FirstOrDefault()?.Value ?? "Library";
            projectInfo.RootNamespace = doc.Descendants(ns + "RootNamespace").FirstOrDefault()?.Value ?? "";

            foreach (var projRef in doc.Descendants(ns + "ProjectReference"))
            {
                var include = projRef.Attribute("Include")?.Value;
                if (!string.IsNullOrEmpty(include))
                    projectInfo.ProjectReferences.Add(Path.GetFileNameWithoutExtension(include));
            }
            foreach (var pkgRef in doc.Descendants(ns + "PackageReference"))
            {
                var name = pkgRef.Attribute("Include")?.Value;
                var version = pkgRef.Attribute("Version")?.Value ?? pkgRef.Element(ns + "Version")?.Value ?? "";
                if (!string.IsNullOrEmpty(name))
                    projectInfo.PackageReferences.Add(new PackageReference { Name = name, Version = version });
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error parsing project file: {Path}", projPath);
        }

        var namespacePathPairs = new List<(string Namespace, string RelativeFolderPath)>();
        var csFiles = Directory.GetFiles(projectDir, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains("\\obj\\") && !f.Contains("\\bin\\") && !f.Contains("/obj/") && !f.Contains("/bin/"))
            .ToList();

        foreach (var csFile in csFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var nsPathPair = await ParseCSharpFileWithPathAsync(csFile, projectDir, basePath, projectInfo);
            if (nsPathPair.HasValue)
                namespacePathPairs.Add(nsPathPair.Value);
        }

        CalculateNamespaceMappings(projectInfo, namespacePathPairs);
        projectInfo.DetectedPatterns = DetectProjectPatterns(projectInfo);
        return projectInfo;
    }

    private async Task<(string Namespace, string RelativeFolderPath)?> ParseCSharpFileWithPathAsync(
        string csPath, string projectDir, string basePath, ProjectInfo projectInfo)
    {
        try
        {
            var content = await File.ReadAllTextAsync(csPath);
            var relativePath = GetRelativePath(csPath, basePath);
            string? fileNamespace = null;
            var nsMatch = NamespaceRegex.Match(content);
            if (nsMatch.Success)
            {
                fileNamespace = nsMatch.Groups[1].Value;
                if (!projectInfo.Namespaces.Contains(fileNamespace))
                    projectInfo.Namespaces.Add(fileNamespace);
            }

            foreach (Match match in ClassRegex.Matches(content))
            {
                var className = match.Groups[3].Value;
                var baseTypes = match.Groups[4].Success ? match.Groups[4].Value.Split(',').Select(t => t.Trim()).ToList() : new List<string>();
                var modifiers = new List<string>();
                if (!string.IsNullOrEmpty(match.Groups[1].Value)) modifiers.Add(match.Groups[1].Value);
                if (!string.IsNullOrEmpty(match.Groups[2].Value)) modifiers.Add(match.Groups[2].Value);
                var (startLine, endLine) = GetTypeLineRange(content, match.Index);
                var typeInfo = new TypeInfo
                {
                    Name = className,
                    Namespace = fileNamespace ?? "",
                    FilePath = relativePath,
                    StartLine = startLine,
                    EndLine = endLine,
                    BaseTypes = baseTypes,
                    Modifiers = modifiers,
                    DetectedPattern = DetectTypePattern(className, baseTypes, content)
                };
                ExtractMembers(content, typeInfo);
                projectInfo.Classes.Add(typeInfo);
            }

            foreach (Match match in InterfaceRegex.Matches(content))
            {
                var interfaceName = match.Groups[2].Value;
                var baseTypes = match.Groups[3].Success ? match.Groups[3].Value.Split(',').Select(t => t.Trim()).ToList() : new List<string>();
                var modifiers = new List<string>();
                if (!string.IsNullOrEmpty(match.Groups[1].Value)) modifiers.Add(match.Groups[1].Value);
                var (startLine, endLine) = GetTypeLineRange(content, match.Index);
                projectInfo.Interfaces.Add(new TypeInfo
                {
                    Name = interfaceName,
                    Namespace = fileNamespace ?? "",
                    FilePath = relativePath,
                    StartLine = startLine,
                    EndLine = endLine,
                    BaseTypes = baseTypes,
                    Modifiers = modifiers,
                    DetectedPattern = DetectTypePattern(interfaceName, baseTypes, content)
                });
            }

            if (!string.IsNullOrEmpty(fileNamespace))
            {
                var fileDir = Path.GetDirectoryName(csPath) ?? "";
                var relativeFolderPath = GetRelativePath(fileDir, projectDir);
                if (relativeFolderPath == "." || string.IsNullOrEmpty(relativeFolderPath)) relativeFolderPath = "";
                return (fileNamespace, relativeFolderPath);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error parsing C# file: {Path}", csPath);
        }
        return null;
    }

    private static (int startLine, int endLine) GetTypeLineRange(string content, int declarationIndex) =>
        LineRangeHelper.GetBraceTypeLineRange(content, declarationIndex);

    private void CalculateNamespaceMappings(ProjectInfo projectInfo, List<(string Namespace, string RelativeFolderPath)> pairs)
    {
        if (pairs.Count == 0) return;
        var rootFolderNamespaces = pairs.Where(p => string.IsNullOrEmpty(p.RelativeFolderPath)).Select(p => p.Namespace).ToList();
        if (rootFolderNamespaces.Any())
            projectInfo.RootNamespace = rootFolderNamespaces.GroupBy(n => n).OrderByDescending(g => g.Count()).First().Key;
        else if (string.IsNullOrEmpty(projectInfo.RootNamespace))
            projectInfo.RootNamespace = FindCommonNamespacePrefix(pairs.Select(p => p.Namespace).Distinct().OrderBy(n => n.Length).ToList());

        var rootNs = projectInfo.RootNamespace;
        foreach (var (ns, folderPath) in pairs.Distinct())
        {
            string nsSuffix = ns.Equals(rootNs, StringComparison.OrdinalIgnoreCase) ? ""
                : ns.StartsWith(rootNs + ".", StringComparison.OrdinalIgnoreCase) ? ns.Substring(rootNs.Length + 1) : ns;
            var normalizedFolder = folderPath.Replace('\\', '/');
            if (!projectInfo.NamespaceFolderMap.ContainsKey(nsSuffix))
                projectInfo.NamespaceFolderMap[nsSuffix] = normalizedFolder;
        }
        if (!projectInfo.NamespaceFolderMap.ContainsKey(""))
            projectInfo.NamespaceFolderMap[""] = "";
    }

    private static string FindCommonNamespacePrefix(List<string> namespaces)
    {
        if (namespaces.Count == 0) return "";
        if (namespaces.Count == 1) return namespaces[0];
        var first = namespaces[0].Split('.');
        var prefixLength = first.Length;
        foreach (var ns in namespaces.Skip(1))
        {
            var parts = ns.Split('.');
            var commonLength = 0;
            for (int i = 0; i < Math.Min(prefixLength, parts.Length); i++)
            {
                if (first[i].Equals(parts[i], StringComparison.OrdinalIgnoreCase)) commonLength++;
                else break;
            }
            prefixLength = commonLength;
            if (prefixLength == 0) break;
        }
        return string.Join(".", first.Take(prefixLength));
    }

    private void ExtractMembers(string content, TypeInfo typeInfo)
    {
        foreach (Match match in MethodRegex.Matches(content))
        {
            var methodName = match.Groups[4].Value;
            if (methodName == "class" || methodName == "interface" || methodName == "new" || methodName == "if") continue;
            var modifiers = new List<string>();
            if (!string.IsNullOrEmpty(match.Groups[1].Value)) modifiers.Add(match.Groups[1].Value);
            if (!string.IsNullOrEmpty(match.Groups[2].Value)) modifiers.Add(match.Groups[2].Value);
            typeInfo.Members.Add(new MemberInfo
            {
                Name = methodName,
                Kind = "Method",
                ReturnType = match.Groups[3].Value.Trim(),
                Parameters = ParseParameters(match.Groups[5].Value),
                Modifiers = modifiers
            });
        }
        foreach (Match match in PropertyRegex.Matches(content))
        {
            var modifiers = new List<string>();
            if (!string.IsNullOrEmpty(match.Groups[1].Value)) modifiers.Add(match.Groups[1].Value);
            if (!string.IsNullOrEmpty(match.Groups[2].Value)) modifiers.Add(match.Groups[2].Value);
            typeInfo.Members.Add(new MemberInfo
            {
                Name = match.Groups[4].Value,
                Kind = "Property",
                ReturnType = match.Groups[3].Value.Trim(),
                Modifiers = modifiers
            });
        }
    }

    private static List<string> ParseParameters(string paramString)
    {
        if (string.IsNullOrWhiteSpace(paramString)) return new List<string>();
        return paramString.Split(',').Select(p => p.Trim()).Where(p => !string.IsNullOrEmpty(p)).ToList();
    }

    private static string? DetectTypePattern(string typeName, List<string> baseTypes, string content)
    {
        foreach (var pattern in PatternKeywords)
        {
            foreach (var keyword in pattern.Value)
            {
                if (typeName.Contains(keyword) || baseTypes.Any(b => b.Contains(keyword))) return pattern.Key;
                if (keyword.StartsWith("[") && content.Contains(keyword)) return pattern.Key;
            }
        }
        return null;
    }

    private List<string> DetectProjectPatterns(ProjectInfo projectInfo)
    {
        var patterns = new HashSet<string>();
        foreach (var cls in projectInfo.Classes)
            if (!string.IsNullOrEmpty(cls.DetectedPattern)) patterns.Add(cls.DetectedPattern);
        foreach (var iface in projectInfo.Interfaces)
            if (!string.IsNullOrEmpty(iface.DetectedPattern)) patterns.Add(iface.DetectedPattern);
        foreach (var pkg in projectInfo.PackageReferences)
        {
            if (pkg.Name.Contains("NUnit")) patterns.Add("NUnit");
            if (pkg.Name.Contains("xUnit")) patterns.Add("xUnit");
            if (pkg.Name.Contains("MSTest")) patterns.Add("MSTest");
            if (pkg.Name.Contains("FluentAssertions")) patterns.Add("FluentAssertions");
            if (pkg.Name.Contains("Moq")) patterns.Add("Moq");
            if (pkg.Name.Contains("EntityFramework")) patterns.Add("EntityFramework");
            if (pkg.Name.Contains("Dapper")) patterns.Add("Dapper");
            if (pkg.Name.Contains("MediatR")) patterns.Add("MediatR");
            if (pkg.Name.Contains("AutoMapper")) patterns.Add("AutoMapper");
            if (pkg.Name.Contains("Serilog")) patterns.Add("Serilog");
            if (pkg.Name.Contains("Newtonsoft")) patterns.Add("Newtonsoft.Json");
        }
        return patterns.ToList();
    }

    private CodebaseSummary BuildSummary(CodebaseAnalysis analysis)
    {
        var allPatterns = analysis.Projects.SelectMany(p => p.DetectedPatterns).Distinct().ToList();
        var allNamespaces = analysis.Projects.SelectMany(p => p.Namespaces).Distinct().OrderBy(n => n).ToList();
        var frameworks = analysis.Projects.GroupBy(p => p.TargetFramework).OrderByDescending(g => g.Count()).Select(g => g.Key).ToList();
        return new CodebaseSummary
        {
            TotalSolutions = analysis.Solutions.Count,
            TotalProjects = analysis.Projects.Count,
            TotalClasses = analysis.Projects.Sum(p => p.Classes.Count),
            TotalInterfaces = analysis.Projects.Sum(p => p.Interfaces.Count),
            TotalFiles = analysis.Projects.Sum(p => p.Classes.Select(c => c.FilePath).Distinct().Count()),
            PrimaryFramework = frameworks.FirstOrDefault() ?? "Unknown",
            DetectedPatterns = allPatterns,
            KeyNamespaces = allNamespaces.Take(10).ToList()
        };
    }

    private CodeConventions DetectConventions(CodebaseAnalysis analysis)
    {
        var conventions = new CodeConventions();
        var allFields = analysis.Projects.SelectMany(p => p.Classes).SelectMany(c => c.Members).Where(m => m.Kind == "Field").Select(m => m.Name).ToList();
        if (allFields.Any())
        {
            var underscoreCount = allFields.Count(f => f.StartsWith("_"));
            var mPrefixCount = allFields.Count(f => f.StartsWith("m_"));
            conventions.PrivateFieldPrefix = mPrefixCount > underscoreCount ? "m_" : "_";
        }
        var testPatterns = analysis.Summary.DetectedPatterns;
        if (testPatterns.Contains("NUnit")) conventions.TestFramework = "NUnit";
        else if (testPatterns.Contains("xUnit")) conventions.TestFramework = "xUnit";
        else if (testPatterns.Contains("MSTest")) conventions.TestFramework = "MSTest";

        var allPackages = analysis.Projects.SelectMany(p => p.PackageReferences).Select(r => r.Name).ToList();
        if (allPackages.Any(p => p.Contains("Microsoft.Extensions.DependencyInjection")))
            conventions.DIFramework = "Microsoft.Extensions.DependencyInjection";
        else if (allPackages.Any(p => p.Contains("Autofac"))) conventions.DIFramework = "Autofac";
        else if (allPackages.Any(p => p.Contains("Ninject"))) conventions.DIFramework = "Ninject";

        var asyncMethods = analysis.Projects.SelectMany(p => p.Classes).SelectMany(c => c.Members)
            .Where(m => m.Kind == "Method" && m.Modifiers.Contains("async")).ToList();
        if (asyncMethods.Any())
            conventions.UsesAsyncSuffix = asyncMethods.Count(m => m.Name.EndsWith("Async")) > asyncMethods.Count / 2;

        return conventions;
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
}
