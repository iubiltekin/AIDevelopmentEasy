using System.Text.RegularExpressions;
using System.Xml.Linq;
using AIDevelopmentEasy.Core.Models;
using Microsoft.Extensions.Logging;

namespace AIDevelopmentEasy.Core.Agents;

/// <summary>
/// Code Analysis Agent - Analyzes existing codebases to extract structure, patterns, and conventions.
/// This agent does NOT use LLM - it performs static analysis of the codebase.
/// </summary>
public class CodeAnalysisAgent
{
    private readonly ILogger<CodeAnalysisAgent>? _logger;

    // Regex patterns for C# parsing
    private static readonly Regex NamespaceRegex = new(@"namespace\s+([\w.]+)", RegexOptions.Compiled);
    private static readonly Regex ClassRegex = new(@"(public|internal|private|protected)?\s*(abstract|static|sealed|partial)?\s*class\s+(\w+)(?:\s*<[^>]+>)?(?:\s*:\s*([^{]+))?", RegexOptions.Compiled);
    private static readonly Regex InterfaceRegex = new(@"(public|internal|private|protected)?\s*interface\s+(I\w+)(?:\s*<[^>]+>)?(?:\s*:\s*([^{]+))?", RegexOptions.Compiled);
    private static readonly Regex MethodRegex = new(@"(public|private|protected|internal)?\s*(static|virtual|override|abstract|async)?\s*([\w<>\[\],\s\?]+)\s+(\w+)\s*\(([^)]*)\)", RegexOptions.Compiled);
    private static readonly Regex PropertyRegex = new(@"(public|private|protected|internal)?\s*(static|virtual|override|abstract)?\s*([\w<>\[\],\s\?]+)\s+(\w+)\s*{\s*(get|set)", RegexOptions.Compiled);
    private static readonly Regex FieldRegex = new(@"(private|protected|internal)?\s*(readonly|static|const)?\s*([\w<>\[\],\s\?]+)\s+(_\w+|\w+)\s*[;=]", RegexOptions.Compiled);

    // Pattern detection keywords
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

    public CodeAnalysisAgent(ILogger<CodeAnalysisAgent>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Analyze a codebase at the given path
    /// </summary>
    public async Task<CodebaseAnalysis> AnalyzeAsync(string codebasePath, string codebaseName, CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("[CodeAnalysis] Starting analysis of: {Path}", codebasePath);

        if (!Directory.Exists(codebasePath))
        {
            throw new DirectoryNotFoundException($"Codebase path not found: {codebasePath}");
        }

        var analysis = new CodebaseAnalysis
        {
            CodebaseName = codebaseName,
            CodebasePath = codebasePath,
            AnalyzedAt = DateTime.UtcNow
        };

        // Find and parse solutions
        var solutionFiles = Directory.GetFiles(codebasePath, "*.sln", SearchOption.AllDirectories);
        foreach (var slnFile in solutionFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var solutionInfo = await ParseSolutionFileAsync(slnFile, codebasePath);
            analysis.Solutions.Add(solutionInfo);
        }

        _logger?.LogInformation("[CodeAnalysis] Found {Count} solution(s)", analysis.Solutions.Count);

        // Find and parse projects
        var projectFiles = Directory.GetFiles(codebasePath, "*.csproj", SearchOption.AllDirectories);
        foreach (var projFile in projectFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var projectInfo = await ParseProjectFileAsync(projFile, codebasePath, cancellationToken);
            analysis.Projects.Add(projectInfo);
        }

        _logger?.LogInformation("[CodeAnalysis] Found {Count} project(s)", analysis.Projects.Count);

        // Build summary
        analysis.Summary = BuildSummary(analysis);

        // Detect conventions
        analysis.Conventions = DetectConventions(analysis);

        _logger?.LogInformation("[CodeAnalysis] Analysis complete: {Classes} classes, {Interfaces} interfaces",
            analysis.Summary.TotalClasses, analysis.Summary.TotalInterfaces);

        return analysis;
    }

    private async Task<SolutionInfo> ParseSolutionFileAsync(string slnPath, string basePath)
    {
        var content = await File.ReadAllTextAsync(slnPath);
        var solutionInfo = new SolutionInfo
        {
            Name = Path.GetFileNameWithoutExtension(slnPath),
            Path = GetRelativePath(slnPath, basePath)
        };

        // Extract project references from solution file
        var projectRegex = new Regex(@"Project\(""\{[^}]+\}""\)\s*=\s*""([^""]+)"",\s*""([^""]+)""");
        var matches = projectRegex.Matches(content);

        foreach (Match match in matches)
        {
            var projectPath = match.Groups[2].Value;
            if (projectPath.EndsWith(".csproj"))
            {
                solutionInfo.ProjectReferences.Add(match.Groups[1].Value);
            }
        }

        return solutionInfo;
    }

    private async Task<ProjectInfo> ParseProjectFileAsync(string projPath, string basePath, CancellationToken cancellationToken)
    {
        var projectInfo = new ProjectInfo
        {
            Name = Path.GetFileNameWithoutExtension(projPath),
            Path = projPath,
            RelativePath = GetRelativePath(projPath, basePath)
        };

        try
        {
            var doc = XDocument.Load(projPath);
            var ns = doc.Root?.Name.Namespace ?? XNamespace.None;

            // Get target framework
            var targetFramework = doc.Descendants(ns + "TargetFramework").FirstOrDefault()?.Value
                ?? doc.Descendants(ns + "TargetFrameworkVersion").FirstOrDefault()?.Value
                ?? "Unknown";
            projectInfo.TargetFramework = targetFramework;

            // Get output type
            projectInfo.OutputType = doc.Descendants(ns + "OutputType").FirstOrDefault()?.Value ?? "Library";

            // Get project references
            foreach (var projRef in doc.Descendants(ns + "ProjectReference"))
            {
                var include = projRef.Attribute("Include")?.Value;
                if (!string.IsNullOrEmpty(include))
                {
                    projectInfo.ProjectReferences.Add(Path.GetFileNameWithoutExtension(include));
                }
            }

            // Get package references
            foreach (var pkgRef in doc.Descendants(ns + "PackageReference"))
            {
                var name = pkgRef.Attribute("Include")?.Value;
                var version = pkgRef.Attribute("Version")?.Value ?? pkgRef.Element(ns + "Version")?.Value ?? "";
                if (!string.IsNullOrEmpty(name))
                {
                    projectInfo.PackageReferences.Add(new PackageReference { Name = name, Version = version });
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error parsing project file: {Path}", projPath);
        }

        // Parse C# files in project directory
        var projectDir = Path.GetDirectoryName(projPath)!;
        var csFiles = Directory.GetFiles(projectDir, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains("\\obj\\") && !f.Contains("\\bin\\"))
            .ToList();

        foreach (var csFile in csFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ParseCSharpFileAsync(csFile, basePath, projectInfo);
        }

        // Detect patterns at project level
        projectInfo.DetectedPatterns = DetectProjectPatterns(projectInfo);

        return projectInfo;
    }

    private async Task ParseCSharpFileAsync(string csPath, string basePath, ProjectInfo projectInfo)
    {
        try
        {
            var content = await File.ReadAllTextAsync(csPath);
            var relativePath = GetRelativePath(csPath, basePath);

            // Extract namespace
            var nsMatch = NamespaceRegex.Match(content);
            if (nsMatch.Success)
            {
                var ns = nsMatch.Groups[1].Value;
                if (!projectInfo.Namespaces.Contains(ns))
                {
                    projectInfo.Namespaces.Add(ns);
                }
            }

            // Extract classes
            foreach (Match match in ClassRegex.Matches(content))
            {
                var className = match.Groups[3].Value;
                var baseTypes = match.Groups[4].Success
                    ? match.Groups[4].Value.Split(',').Select(t => t.Trim()).ToList()
                    : new List<string>();

                var modifiers = new List<string>();
                if (!string.IsNullOrEmpty(match.Groups[1].Value)) modifiers.Add(match.Groups[1].Value);
                if (!string.IsNullOrEmpty(match.Groups[2].Value)) modifiers.Add(match.Groups[2].Value);

                var typeInfo = new TypeInfo
                {
                    Name = className,
                    Namespace = nsMatch.Success ? nsMatch.Groups[1].Value : "",
                    FilePath = relativePath,
                    BaseTypes = baseTypes,
                    Modifiers = modifiers,
                    DetectedPattern = DetectTypePattern(className, baseTypes, content)
                };

                // Extract members (simplified)
                ExtractMembers(content, typeInfo);

                projectInfo.Classes.Add(typeInfo);
            }

            // Extract interfaces
            foreach (Match match in InterfaceRegex.Matches(content))
            {
                var interfaceName = match.Groups[2].Value;
                var baseTypes = match.Groups[3].Success
                    ? match.Groups[3].Value.Split(',').Select(t => t.Trim()).ToList()
                    : new List<string>();

                var modifiers = new List<string>();
                if (!string.IsNullOrEmpty(match.Groups[1].Value)) modifiers.Add(match.Groups[1].Value);

                var typeInfo = new TypeInfo
                {
                    Name = interfaceName,
                    Namespace = nsMatch.Success ? nsMatch.Groups[1].Value : "",
                    FilePath = relativePath,
                    BaseTypes = baseTypes,
                    Modifiers = modifiers,
                    DetectedPattern = DetectTypePattern(interfaceName, baseTypes, content)
                };

                projectInfo.Interfaces.Add(typeInfo);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error parsing C# file: {Path}", csPath);
        }
    }

    private void ExtractMembers(string content, TypeInfo typeInfo)
    {
        // Extract methods
        foreach (Match match in MethodRegex.Matches(content))
        {
            var methodName = match.Groups[4].Value;
            // Skip if it looks like a class declaration or common keywords
            if (methodName == "class" || methodName == "interface" || methodName == "new" || methodName == "if")
                continue;

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

        // Extract properties
        foreach (Match match in PropertyRegex.Matches(content))
        {
            var propName = match.Groups[4].Value;
            var modifiers = new List<string>();
            if (!string.IsNullOrEmpty(match.Groups[1].Value)) modifiers.Add(match.Groups[1].Value);
            if (!string.IsNullOrEmpty(match.Groups[2].Value)) modifiers.Add(match.Groups[2].Value);

            typeInfo.Members.Add(new MemberInfo
            {
                Name = propName,
                Kind = "Property",
                ReturnType = match.Groups[3].Value.Trim(),
                Modifiers = modifiers
            });
        }
    }

    private List<string> ParseParameters(string paramString)
    {
        if (string.IsNullOrWhiteSpace(paramString))
            return new List<string>();

        return paramString.Split(',')
            .Select(p => p.Trim())
            .Where(p => !string.IsNullOrEmpty(p))
            .ToList();
    }

    private string? DetectTypePattern(string typeName, List<string> baseTypes, string content)
    {
        foreach (var pattern in PatternKeywords)
        {
            foreach (var keyword in pattern.Value)
            {
                if (typeName.Contains(keyword) || baseTypes.Any(b => b.Contains(keyword)))
                {
                    return pattern.Key;
                }

                // Check content for attributes (like [Test])
                if (keyword.StartsWith("[") && content.Contains(keyword))
                {
                    return pattern.Key;
                }
            }
        }

        return null;
    }

    private List<string> DetectProjectPatterns(ProjectInfo projectInfo)
    {
        var patterns = new HashSet<string>();

        // From classes and interfaces
        foreach (var cls in projectInfo.Classes)
        {
            if (!string.IsNullOrEmpty(cls.DetectedPattern))
                patterns.Add(cls.DetectedPattern);
        }

        foreach (var iface in projectInfo.Interfaces)
        {
            if (!string.IsNullOrEmpty(iface.DetectedPattern))
                patterns.Add(iface.DetectedPattern);
        }

        // From package references
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
        var allPatterns = analysis.Projects
            .SelectMany(p => p.DetectedPatterns)
            .Distinct()
            .ToList();

        var allNamespaces = analysis.Projects
            .SelectMany(p => p.Namespaces)
            .Distinct()
            .OrderBy(n => n)
            .ToList();

        // Get primary framework (most common)
        var frameworks = analysis.Projects
            .GroupBy(p => p.TargetFramework)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .ToList();

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

        // Detect private field prefix
        var allFields = analysis.Projects
            .SelectMany(p => p.Classes)
            .SelectMany(c => c.Members)
            .Where(m => m.Kind == "Field")
            .Select(m => m.Name)
            .ToList();

        if (allFields.Any())
        {
            var underscoreCount = allFields.Count(f => f.StartsWith("_"));
            var mPrefixCount = allFields.Count(f => f.StartsWith("m_"));
            conventions.PrivateFieldPrefix = mPrefixCount > underscoreCount ? "m_" : "_";
        }

        // Detect test framework
        var testPatterns = analysis.Summary.DetectedPatterns;
        if (testPatterns.Contains("NUnit")) conventions.TestFramework = "NUnit";
        else if (testPatterns.Contains("xUnit")) conventions.TestFramework = "xUnit";
        else if (testPatterns.Contains("MSTest")) conventions.TestFramework = "MSTest";

        // Detect DI framework from packages
        var allPackages = analysis.Projects.SelectMany(p => p.PackageReferences).Select(r => r.Name).ToList();
        if (allPackages.Any(p => p.Contains("Microsoft.Extensions.DependencyInjection")))
            conventions.DIFramework = "Microsoft.Extensions.DependencyInjection";
        else if (allPackages.Any(p => p.Contains("Autofac")))
            conventions.DIFramework = "Autofac";
        else if (allPackages.Any(p => p.Contains("Ninject")))
            conventions.DIFramework = "Ninject";

        // Check for async suffix usage
        var asyncMethods = analysis.Projects
            .SelectMany(p => p.Classes)
            .SelectMany(c => c.Members)
            .Where(m => m.Kind == "Method" && m.Modifiers.Contains("async"))
            .ToList();

        if (asyncMethods.Any())
        {
            var asyncSuffixCount = asyncMethods.Count(m => m.Name.EndsWith("Async"));
            conventions.UsesAsyncSuffix = asyncSuffixCount > asyncMethods.Count / 2;
        }

        return conventions;
    }

    private string GetRelativePath(string fullPath, string basePath)
    {
        if (fullPath.StartsWith(basePath, StringComparison.OrdinalIgnoreCase))
        {
            var relative = fullPath.Substring(basePath.Length);
            return relative.TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        return fullPath;
    }

    /// <summary>
    /// Find a class by name in the analyzed codebase
    /// </summary>
    public async Task<ClassSearchResult> FindClassAsync(CodebaseAnalysis analysis, string className, bool includeContent = true)
    {
        _logger?.LogInformation("[CodeAnalysis] Searching for class: {ClassName}", className);

        var result = new ClassSearchResult { ClassName = className };

        // Search in all projects
        foreach (var project in analysis.Projects)
        {
            var classInfo = project.Classes.FirstOrDefault(c =>
                c.Name.Equals(className, StringComparison.OrdinalIgnoreCase));

            if (classInfo != null)
            {
                result.Found = true;
                result.FilePath = classInfo.FilePath;
                result.FullPath = Path.Combine(analysis.CodebasePath, classInfo.FilePath);
                result.Namespace = classInfo.Namespace;
                result.ProjectName = project.Name;
                result.BaseTypes = classInfo.BaseTypes;
                result.Members = classInfo.Members;

                if (includeContent && File.Exists(result.FullPath))
                {
                    result.FileContent = await File.ReadAllTextAsync(result.FullPath);
                }

                _logger?.LogInformation("[CodeAnalysis] Found class {ClassName} in {FilePath}", className, result.FilePath);
                return result;
            }
        }

        _logger?.LogWarning("[CodeAnalysis] Class {ClassName} not found in codebase", className);
        return result;
    }

    /// <summary>
    /// Find all references to a class in the codebase
    /// </summary>
    public async Task<ReferenceSearchResult> FindReferencesAsync(CodebaseAnalysis analysis, string className, CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("[CodeAnalysis] Finding references to class: {ClassName}", className);

        var result = new ReferenceSearchResult { ClassName = className };

        // First find the class itself
        var classSearch = await FindClassAsync(analysis, className, false);
        if (classSearch.Found)
        {
            result.ClassFilePath = classSearch.FilePath;
        }

        // Search in all C# files for references
        var csFiles = Directory.GetFiles(analysis.CodebasePath, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains("\\obj\\") && !f.Contains("\\bin\\") && !f.Contains("/obj/") && !f.Contains("/bin/"))
            .ToList();

        foreach (var csFile in csFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var relativePath = GetRelativePath(csFile, analysis.CodebasePath);

            // Skip the class's own file for most reference types
            var isOwnFile = classSearch.Found && relativePath.Equals(classSearch.FilePath, StringComparison.OrdinalIgnoreCase);

            try
            {
                var lines = await File.ReadAllLinesAsync(csFile, cancellationToken);
                var projectName = FindProjectForFile(analysis, relativePath);
                string? currentClass = null;
                string? currentMethod = null;

                for (int i = 0; i < lines.Length; i++)
                {
                    var line = lines[i];
                    var lineNumber = i + 1;

                    // Track current class/method context
                    var classMatch = ClassRegex.Match(line);
                    if (classMatch.Success)
                    {
                        currentClass = classMatch.Groups[3].Value;
                        currentMethod = null;
                    }

                    var methodMatch = MethodRegex.Match(line);
                    if (methodMatch.Success)
                    {
                        currentMethod = methodMatch.Groups[4].Value;
                    }

                    // Skip if this line doesn't contain the class name
                    if (!line.Contains(className))
                        continue;

                    // Determine reference type
                    var refType = DetermineReferenceType(line, className, isOwnFile);
                    if (refType == null)
                        continue;

                    result.References.Add(new ClassReference
                    {
                        FilePath = relativePath,
                        FullPath = csFile,
                        ProjectName = projectName,
                        LineNumber = lineNumber,
                        LineContent = line.Trim(),
                        ReferenceType = refType.Value,
                        ContainingClass = currentClass,
                        ContainingMethod = currentMethod
                    });
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error reading file: {File}", csFile);
            }
        }

        _logger?.LogInformation("[CodeAnalysis] Found {Count} references to {ClassName} in {FileCount} files",
            result.References.Count, className, result.AffectedFiles.Count);

        return result;
    }

    private ReferenceType? DetermineReferenceType(string line, string className, bool isOwnFile)
    {
        var trimmedLine = line.Trim();

        // Skip comments
        if (trimmedLine.StartsWith("//") || trimmedLine.StartsWith("/*") || trimmedLine.StartsWith("*"))
            return null;

        // Using statement
        if (trimmedLine.StartsWith("using ") && trimmedLine.Contains(className))
            return ReferenceType.Using;

        // Skip class/interface declaration lines (we want usages, not definitions)
        if (Regex.IsMatch(trimmedLine, $@"\bclass\s+{className}\b") ||
            Regex.IsMatch(trimmedLine, $@"\binterface\s+{className}\b"))
        {
            return isOwnFile ? null : ReferenceType.Unknown;
        }

        // Inheritance - class Foo : ClassName or : IClassName
        if (Regex.IsMatch(trimmedLine, $@":\s*[^{{]*\b{className}\b"))
            return ReferenceType.Inheritance;

        // Object instantiation - new ClassName(
        if (Regex.IsMatch(trimmedLine, $@"\bnew\s+{className}\s*[\(<]"))
            return ReferenceType.Instantiation;

        // Static method call - ClassName.Method(
        if (Regex.IsMatch(trimmedLine, $@"\b{className}\s*\.\s*\w+\s*\("))
            return ReferenceType.StaticCall;

        // Generic type argument - List<ClassName>, Task<ClassName>
        if (Regex.IsMatch(trimmedLine, $@"<[^>]*\b{className}\b[^>]*>"))
            return ReferenceType.GenericArgument;

        // Field/property type - private ClassName _field; or public ClassName Property
        if (Regex.IsMatch(trimmedLine, $@"\b(private|protected|public|internal)\s+.*\b{className}\b\s+\w+"))
            return ReferenceType.Field;

        // Method parameter - (ClassName param) or , ClassName param
        if (Regex.IsMatch(trimmedLine, $@"[\(,]\s*{className}\s+\w+"))
            return ReferenceType.Parameter;

        // Return type - public ClassName MethodName(
        if (Regex.IsMatch(trimmedLine, $@"\b{className}\s+\w+\s*\("))
            return ReferenceType.ReturnType;

        // Local variable - var x = ... as ClassName or ClassName x = 
        if (Regex.IsMatch(trimmedLine, $@"\b{className}\s+\w+\s*="))
            return ReferenceType.LocalVariable;

        // If the line contains the class name but doesn't match specific patterns
        if (Regex.IsMatch(trimmedLine, $@"\b{className}\b"))
            return ReferenceType.Unknown;

        return null;
    }

    private string FindProjectForFile(CodebaseAnalysis analysis, string relativePath)
    {
        foreach (var project in analysis.Projects)
        {
            var projectDir = Path.GetDirectoryName(project.RelativePath) ?? "";
            if (relativePath.StartsWith(projectDir, StringComparison.OrdinalIgnoreCase))
            {
                return project.Name;
            }
        }
        return "Unknown";
    }

    /// <summary>
    /// Get the content of a file
    /// </summary>
    public async Task<string?> GetFileContentAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            _logger?.LogWarning("[CodeAnalysis] File not found: {FilePath}", filePath);
            return null;
        }

        return await File.ReadAllTextAsync(filePath);
    }

    /// <summary>
    /// Get the content of a file by relative path within the codebase
    /// </summary>
    public async Task<string?> GetFileContentAsync(CodebaseAnalysis analysis, string relativePath)
    {
        var fullPath = Path.Combine(analysis.CodebasePath, relativePath);
        return await GetFileContentAsync(fullPath);
    }

    /// <summary>
    /// Create file modification tasks based on class references
    /// </summary>
    public async Task<List<FileModificationTask>> CreateModificationTasksAsync(
        CodebaseAnalysis analysis,
        ReferenceSearchResult references,
        string modificationDescription,
        CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("[CodeAnalysis] Creating modification tasks for {Count} affected files",
            references.AffectedFiles.Count);

        var tasks = new List<FileModificationTask>();

        // Group references by file
        var fileGroups = references.References
            .GroupBy(r => r.FilePath)
            .OrderBy(g => g.Key);

        foreach (var group in fileGroups)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var firstRef = group.First();
            var fullPath = firstRef.FullPath;

            var task = new FileModificationTask
            {
                FilePath = group.Key,
                FullPath = fullPath,
                ProjectName = firstRef.ProjectName,
                Description = $"Update references to {references.ClassName} in {Path.GetFileName(group.Key)}",
                RelatedReferences = group.ToList()
            };

            // Determine modification type based on references
            var refTypes = group.Select(r => r.ReferenceType).Distinct().ToList();
            if (refTypes.Contains(ReferenceType.Inheritance))
                task.ModificationType = ModificationType.ModifyMethod; // Likely need to update overrides
            else if (refTypes.Contains(ReferenceType.Instantiation) || refTypes.Contains(ReferenceType.StaticCall))
                task.ModificationType = ModificationType.UpdateMethodCall;
            else
                task.ModificationType = ModificationType.General;

            // Load current content
            if (File.Exists(fullPath))
            {
                task.CurrentContent = await File.ReadAllTextAsync(fullPath, cancellationToken);
            }

            // Set target class and method from first meaningful reference
            var meaningfulRef = group.FirstOrDefault(r => !string.IsNullOrEmpty(r.ContainingClass));
            if (meaningfulRef != null)
            {
                task.TargetClass = meaningfulRef.ContainingClass;
                task.TargetMethod = meaningfulRef.ContainingMethod;
            }

            tasks.Add(task);
        }

        _logger?.LogInformation("[CodeAnalysis] Created {Count} modification tasks", tasks.Count);
        return tasks;
    }

    /// <summary>
    /// Generate context for the Planner including class and reference information
    /// </summary>
    public string GenerateModificationContext(
        ClassSearchResult classResult,
        ReferenceSearchResult references,
        List<FileModificationTask> modificationTasks)
    {
        var sb = new System.Text.StringBuilder();

        sb.AppendLine("## TARGET CLASS INFORMATION");
        sb.AppendLine($"Class: {classResult.ClassName}");
        sb.AppendLine($"File: {classResult.FilePath}");
        sb.AppendLine($"Namespace: {classResult.Namespace}");
        sb.AppendLine($"Project: {classResult.ProjectName}");

        if (classResult.BaseTypes.Any())
            sb.AppendLine($"Base Types: {string.Join(", ", classResult.BaseTypes)}");

        if (classResult.Members.Any())
        {
            sb.AppendLine();
            sb.AppendLine("### Current Members:");
            foreach (var member in classResult.Members.Take(20))
            {
                var signature = member.Kind == "Method"
                    ? $"{member.ReturnType} {member.Name}({string.Join(", ", member.Parameters)})"
                    : $"{member.ReturnType} {member.Name}";
                sb.AppendLine($"- {member.Kind}: {signature}");
            }
        }

        sb.AppendLine();
        sb.AppendLine("## REFERENCES (Files that use this class)");
        sb.AppendLine($"Total References: {references.References.Count}");
        sb.AppendLine($"Affected Files: {references.AffectedFiles.Count}");
        sb.AppendLine($"Affected Projects: {string.Join(", ", references.AffectedProjects)}");

        sb.AppendLine();
        sb.AppendLine("### Reference Details by File:");
        var fileGroups = references.References.GroupBy(r => r.FilePath);
        foreach (var group in fileGroups.Take(15))
        {
            sb.AppendLine($"\n**{group.Key}** ({group.First().ProjectName}):");
            foreach (var reference in group.Take(5))
            {
                sb.AppendLine($"  - Line {reference.LineNumber} [{reference.ReferenceType}]: {reference.LineContent}");
            }
            if (group.Count() > 5)
                sb.AppendLine($"  ... and {group.Count() - 5} more references");
        }

        if (modificationTasks.Any())
        {
            sb.AppendLine();
            sb.AppendLine("## FILES TO MODIFY");
            foreach (var task in modificationTasks)
            {
                sb.AppendLine($"- **{task.FilePath}** ({task.ProjectName})");
                sb.AppendLine($"  Type: {task.ModificationType}");
                sb.AppendLine($"  Description: {task.Description}");
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Generate a concise context string for use in prompts
    /// </summary>
    public string GenerateContextForPrompt(CodebaseAnalysis analysis)
    {
        var sb = new System.Text.StringBuilder();

        sb.AppendLine($"## Codebase: {analysis.CodebaseName}");
        sb.AppendLine($"Framework: {analysis.Summary.PrimaryFramework}");
        sb.AppendLine($"Projects: {analysis.Summary.TotalProjects}, Classes: {analysis.Summary.TotalClasses}, Interfaces: {analysis.Summary.TotalInterfaces}");
        sb.AppendLine();

        sb.AppendLine("### Projects:");
        foreach (var proj in analysis.Projects.OrderBy(p => p.Name))
        {
            var projType = proj.IsTestProject ? "Test" : proj.OutputType;
            sb.AppendLine($"- **{proj.Name}** ({projType}, {proj.TargetFramework})");

            if (proj.ProjectReferences.Any())
            {
                sb.AppendLine($"  - References: {string.Join(", ", proj.ProjectReferences)}");
            }

            if (proj.DetectedPatterns.Any())
            {
                sb.AppendLine($"  - Patterns: {string.Join(", ", proj.DetectedPatterns)}");
            }

            // List key interfaces
            if (proj.Interfaces.Any())
            {
                var keyInterfaces = proj.Interfaces.Take(5).Select(i => i.Name);
                sb.AppendLine($"  - Key Interfaces: {string.Join(", ", keyInterfaces)}");
            }

            // List key classes
            if (proj.Classes.Any())
            {
                var keyClasses = proj.Classes
                    .Where(c => !string.IsNullOrEmpty(c.DetectedPattern))
                    .Take(5)
                    .Select(c => c.Name);
                if (keyClasses.Any())
                {
                    sb.AppendLine($"  - Key Classes: {string.Join(", ", keyClasses)}");
                }
            }
        }

        sb.AppendLine();
        sb.AppendLine("### Conventions:");
        sb.AppendLine($"- Private fields: {analysis.Conventions.PrivateFieldPrefix}fieldName");
        if (!string.IsNullOrEmpty(analysis.Conventions.TestFramework))
            sb.AppendLine($"- Test framework: {analysis.Conventions.TestFramework}");
        if (!string.IsNullOrEmpty(analysis.Conventions.DIFramework))
            sb.AppendLine($"- DI framework: {analysis.Conventions.DIFramework}");
        sb.AppendLine($"- Async suffix: {(analysis.Conventions.UsesAsyncSuffix ? "Yes" : "No")}");

        return sb.ToString();
    }
}
