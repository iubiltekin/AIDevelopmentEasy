using System.Text.RegularExpressions;
using AIDevelopmentEasy.Core.Analysis;
using AIDevelopmentEasy.Core.Models;
using Microsoft.Extensions.Logging;

namespace AIDevelopmentEasy.Core.Agents;

/// <summary>
/// Code Analysis Agent - Orchestrates codebase analysis across one or more languages.
/// Runs all applicable analyzers (C#, Go, Rust, etc.) and merges results for polyglot repos.
/// Also provides C#-specific helpers: FindClassAsync, FindReferencesAsync, GenerateContextForPrompt, etc.
/// </summary>
public class CodeAnalysisAgent
{
    private readonly CodebaseAnalyzerFactory _factory;
    private readonly ILogger<CodeAnalysisAgent>? _logger;

    // C# regex used for FindReferencesAsync and related helpers
    private static readonly Regex ClassRegex = new(@"(public|internal|private|protected)?\s*(abstract|static|sealed|partial)?\s*class\s+(\w+)(?:\s*<[^>]+>)?(?:\s*:\s*([^{]+))?", RegexOptions.Compiled);
    private static readonly Regex MethodRegex = new(@"(public|private|protected|internal)?\s*(static|virtual|override|abstract|async)?\s*([\w<>\[\],\s\?]+)\s+(\w+)\s*\(([^)]*)\)", RegexOptions.Compiled);

    public CodeAnalysisAgent(CodebaseAnalyzerFactory factory, ILogger<CodeAnalysisAgent>? logger = null)
    {
        _factory = factory;
        _logger = logger;
    }

    /// <summary>
    /// Analyze a codebase at the given path. Runs only analyzers that CanAnalyze that exact path
    /// (C#, Go, Rust, Frontend, etc.) and merges results. No parent or sibling directories are scanned.
    /// </summary>
    public async Task<CodebaseAnalysis> AnalyzeAsync(string codebasePath, string codebaseName, CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("[CodeAnalysis] Starting analysis of: {Path}", codebasePath);

        if (!Directory.Exists(codebasePath))
            throw new DirectoryNotFoundException($"Codebase path not found: {codebasePath}");

        var applicable = _factory.GetApplicableAnalyzers(codebasePath);
        if (applicable.Count == 0)
        {
            var csharp = _factory.GetAnalyzer("csharp");
            if (csharp != null)
            {
                _logger?.LogInformation("[CodeAnalysis] No applicable analyzer; falling back to C#.");
                applicable = new List<ICodebaseAnalyzer> { csharp };
            }
            else
                throw new InvalidOperationException("No codebase analyzer is applicable for this path and no C# fallback is registered.");
        }

        _logger?.LogInformation("[CodeAnalysis] Running {Count} analyzer(s) on path only: {Languages}",
            applicable.Count, string.Join(", ", applicable.Select(a => a.LanguageId)));

        var partialResults = new List<CodebaseAnalysis>();
        foreach (var analyzer in applicable)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var partial = await analyzer.AnalyzeAsync(codebasePath, codebaseName, cancellationToken);
                partialResults.Add(partial);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "[CodeAnalysis] Analyzer {LanguageId} failed; skipping.", analyzer.LanguageId);
            }
        }

        if (partialResults.Count == 0)
            throw new InvalidOperationException("All applicable analyzers failed.");

        var merged = MergeAnalyses(codebasePath, codebaseName, partialResults);
        _logger?.LogInformation("[CodeAnalysis] Analysis complete: {Projects} projects, {Classes} classes, {Interfaces} interfaces, languages: {Languages}",
            merged.Projects.Count, merged.Summary.TotalClasses, merged.Summary.TotalInterfaces, string.Join(", ", merged.Summary.Languages));
        return merged;
    }

    /// <summary>
    /// Merge multiple partial analyses (one per language) into a single CodebaseAnalysis.
    /// </summary>
    private static CodebaseAnalysis MergeAnalyses(string codebasePath, string codebaseName, List<CodebaseAnalysis> partials)
    {
        var merged = new CodebaseAnalysis
        {
            CodebaseName = codebaseName,
            CodebasePath = codebasePath,
            AnalyzedAt = DateTime.UtcNow
        };

        foreach (var p in partials)
        {
            merged.Solutions.AddRange(p.Solutions);
            merged.Projects.AddRange(p.Projects);
        }

        merged.Summary = new CodebaseSummary
        {
            TotalSolutions = merged.Solutions.Count,
            TotalProjects = merged.Projects.Count,
            TotalClasses = merged.Projects.Sum(x => x.Classes.Count),
            TotalInterfaces = merged.Projects.Sum(x => x.Interfaces.Count),
            TotalFiles = merged.Projects.Sum(x => x.Classes.Select(c => c.FilePath).Distinct().Count()),
            PrimaryFramework = partials.Count == 1 ? partials[0].Summary.PrimaryFramework : "Mixed",
            DetectedPatterns = merged.Projects.SelectMany(x => x.DetectedPatterns).Distinct().ToList(),
            KeyNamespaces = merged.Projects.SelectMany(x => x.Namespaces).Distinct().OrderBy(n => n).Take(10).ToList(),
            Languages = partials.SelectMany(p => p.Summary?.Languages ?? Enumerable.Empty<string>()).Distinct().ToList()
        };

        merged.Conventions = partials.FirstOrDefault(p => p.Conventions != null)?.Conventions ?? new CodeConventions();

        var reqProjects = new List<ProjectBrief>();
        var reqArchitecture = new List<string>();
        var reqTech = new HashSet<string>();
        var reqExtensionPoints = new List<ExtensionPoint>();
        var reqSummaryText = new System.Text.StringBuilder();

        foreach (var p in partials)
        {
            if (p.RequirementContext?.Projects != null) reqProjects.AddRange(p.RequirementContext.Projects);
            if (p.RequirementContext?.Architecture != null) reqArchitecture.AddRange(p.RequirementContext.Architecture);
            if (p.RequirementContext?.Technologies != null) foreach (var t in p.RequirementContext.Technologies) reqTech.Add(t);
            if (p.RequirementContext?.ExtensionPoints != null) reqExtensionPoints.AddRange(p.RequirementContext.ExtensionPoints);
            if (!string.IsNullOrEmpty(p.RequirementContext?.SummaryText))
                reqSummaryText.AppendLine(p.RequirementContext.SummaryText).AppendLine();
        }

        merged.RequirementContext = new RequirementContext
        {
            Projects = reqProjects,
            Architecture = reqArchitecture.Distinct().ToList(),
            Technologies = reqTech.ToList(),
            ExtensionPoints = reqExtensionPoints.Take(15).ToList(),
            SummaryText = reqSummaryText.ToString().TrimEnd(),
            TokenEstimate = reqSummaryText.Length / 4
        };

        var pipeDetails = new List<ProjectDetail>();
        var pipeContextText = new System.Text.StringBuilder();
        foreach (var p in partials)
        {
            if (p.PipelineContext?.ProjectDetails != null) pipeDetails.AddRange(p.PipelineContext.ProjectDetails);
            if (!string.IsNullOrEmpty(p.PipelineContext?.FullContextText))
                pipeContextText.AppendLine(p.PipelineContext.FullContextText).AppendLine();
        }

        merged.PipelineContext = new PipelineContext
        {
            ProjectDetails = pipeDetails,
            FullContextText = pipeContextText.ToString().TrimEnd(),
            TokenEstimate = pipeContextText.Length / 4
        };

        return merged;
    }

    // ----- C#-specific helpers (used by Pipeline, Planner, etc.) -----


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
    /// Get the file extension(s) to use for a given language (for planner/coder prompts).
    /// </summary>
    private static string GetFileExtensionsForLanguage(string languageId)
    {
        return (languageId?.ToLowerInvariant()) switch
        {
            "csharp" or "c#" => ".cs",
            "go" => ".go",
            "rust" => ".rs",
            "python" => ".py",
            "typescript" or "ts" => ".ts, .tsx (use .tsx for React components)",
            _ => ".cs" // fallback only when unknown
        };
    }

    /// <summary>
    /// Generate a concise context string for use in prompts.
    /// Language-agnostic: includes each project's language and file extensions so the planner
    /// generates target_files with correct extensions (e.g. .tsx for React, .go for Go).
    /// </summary>
    public string GenerateContextForPrompt(CodebaseAnalysis analysis)
    {
        var sb = new System.Text.StringBuilder();

        sb.AppendLine($"## Codebase: {analysis.CodebaseName}");
        sb.AppendLine($"Path: {analysis.CodebasePath}");
        sb.AppendLine($"Projects: {analysis.Summary.TotalProjects}, Classes/Types: {analysis.Summary.TotalClasses}, Interfaces: {analysis.Summary.TotalInterfaces}");
        if (analysis.Summary.Languages?.Count > 0)
            sb.AppendLine($"Languages in this repo: {string.Join(", ", analysis.Summary.Languages)}");
        sb.AppendLine();

        // Codebase-specific: only the languages present in this repo (single source of truth for planner)
        var languagesInRepo = analysis.Projects
            .Select(p => (string.IsNullOrEmpty(p.LanguageId) ? "csharp" : p.LanguageId).ToLowerInvariant())
            .Distinct()
            .ToHashSet();
        // Order: put Go, C#, TypeScript/React first so LLM sees primary backend/frontend before Python (avoids .py when repo is Go+React)
        var langOrder = new[] { "go", "csharp", "typescript", "ts", "rust", "python" };
        var orderedProjects = analysis.Projects
            .OrderBy(p => { var l = (string.IsNullOrEmpty(p.LanguageId) ? "csharp" : p.LanguageId).ToLowerInvariant(); var i = Array.IndexOf(langOrder, l); return i < 0 ? 99 : i; })
            .ThenBy(p => p.Name)
            .ToList();

        sb.AppendLine("### Languages and file extensions (use ONLY these for this codebase):");
        foreach (var proj in orderedProjects)
        {
            var lang = string.IsNullOrEmpty(proj.LanguageId) ? "csharp" : proj.LanguageId;
            var ext = GetFileExtensionsForLanguage(lang);
            var role = string.IsNullOrEmpty(proj.Role) ? "" : $" [{proj.Role}]";
            sb.AppendLine($"- **{proj.Name}** → language: {lang}{role} → file extensions: {ext}");
        }
        sb.AppendLine();
        sb.AppendLine("→ Generate target_files using ONLY the extensions above for each project.");
        // Explicit forbidden: do not use extensions for languages NOT in this codebase (fixes .py when repo is Go+React)
        var forbidden = new List<string>();
        if (!languagesInRepo.Contains("python")) forbidden.Add(".py");
        if (!languagesInRepo.Contains("csharp")) forbidden.Add(".cs");
        if (!languagesInRepo.Contains("go")) forbidden.Add(".go");
        if (!languagesInRepo.Contains("rust")) forbidden.Add(".rs");
        if (!languagesInRepo.Contains("typescript") && !languagesInRepo.Contains("ts")) forbidden.Add(".ts/.tsx");
        if (forbidden.Count > 0)
            sb.AppendLine($"→ Do NOT use these extensions in this codebase: {string.Join(", ", forbidden)}.");
        sb.AppendLine();

        // Separate main projects and test projects
        var mainProjects = analysis.Projects.Where(p => !p.IsTestProject).OrderBy(p => p.Name).ToList();
        var testProjects = analysis.Projects.Where(p => p.IsTestProject).OrderBy(p => p.Name).ToList();

        sb.AppendLine("### Main Projects:");
        foreach (var proj in mainProjects)
        {
            var lang = string.IsNullOrEmpty(proj.LanguageId) ? "csharp" : proj.LanguageId;
            var ext = GetFileExtensionsForLanguage(lang);
            sb.AppendLine($"- **{proj.Name}** ({proj.OutputType}, {proj.TargetFramework}, language: {lang})");
            sb.AppendLine($"  - Path: {proj.RelativePath}");
            sb.AppendLine($"  - Use file extension(s): {ext}");

            if (proj.ProjectReferences.Any())
            {
                sb.AppendLine($"  - References: {string.Join(", ", proj.ProjectReferences)}");
            }

            if (proj.DetectedPatterns.Any())
            {
                sb.AppendLine($"  - Patterns: {string.Join(", ", proj.DetectedPatterns)}");
            }

            // Show folder structure with ACTUAL file paths (so planner sees real extensions: .go, .tsx, etc.)
            var folderGroups = proj.Classes
                .Where(c => !string.IsNullOrEmpty(c.FilePath))
                .GroupBy(c => Path.GetDirectoryName(c.FilePath)?.Replace("\\", "/") ?? "")
                .Where(g => !string.IsNullOrEmpty(g.Key))
                .OrderBy(g => g.Key)
                .Take(10)
                .ToList();

            if (folderGroups.Any())
            {
                sb.AppendLine($"  - Folder Structure (real paths):");
                foreach (var folder in folderGroups)
                {
                    var folderName = folder.Key;
                    sb.AppendLine($"    - {folderName}/");
                    foreach (var cls in folder.Take(3))
                    {
                        sb.AppendLine($"      - {cls.FilePath} (namespace/package: {cls.Namespace})");
                    }
                    if (folder.Count() > 3)
                    {
                        sb.AppendLine($"      - ... and {folder.Count() - 3} more");
                    }
                }
            }

            // List key interfaces with paths
            if (proj.Interfaces.Any())
            {
                var keyInterfaces = proj.Interfaces.Take(5);
                sb.AppendLine($"  - Key Interfaces:");
                foreach (var iface in keyInterfaces)
                {
                    sb.AppendLine($"    - {iface.Name} ({iface.FilePath})");
                }
            }
        }

        // Test Project Mapping (language-agnostic: "test projects" or "where tests go")
        sb.AppendLine();
        sb.AppendLine("### Test Projects (unit tests go here; use same language/extensions as main project):");
        foreach (var testProj in testProjects)
        {
            var testLang = string.IsNullOrEmpty(testProj.LanguageId) ? "csharp" : testProj.LanguageId;
            var testExt = GetFileExtensionsForLanguage(testLang);
            var mainProjName = testProj.Name
                .Replace(".UnitTest", "")
                .Replace(".Tests", "")
                .Replace(".Test", "");

            var mainProj = mainProjects.FirstOrDefault(p =>
                p.Name.Equals(mainProjName, StringComparison.OrdinalIgnoreCase) ||
                p.Name.StartsWith(mainProjName, StringComparison.OrdinalIgnoreCase));

            sb.AppendLine($"- **{testProj.Name}** (language: {testLang}, extensions: {testExt})");
            sb.AppendLine($"  - Path: {testProj.RelativePath}");
            if (mainProj != null)
            {
                sb.AppendLine($"  - Tests for: {mainProj.Name}");
            }

            var testFolders = testProj.Classes
                .Where(c => !string.IsNullOrEmpty(c.FilePath))
                .GroupBy(c => Path.GetDirectoryName(c.FilePath)?.Replace("\\", "/") ?? "")
                .Take(5)
                .ToList();

            if (testFolders.Any())
            {
                sb.AppendLine($"  - Test Files:");
                foreach (var folder in testFolders)
                {
                    foreach (var testClass in folder.Take(2))
                    {
                        sb.AppendLine($"    - {testClass.FilePath}");
                    }
                }
            }
        }

        // Detect database migrator (e.g. scripts/migrator + psqlmigrations) so planner can load planner-migrator rules
        var migratorInfo = DetectMigratorAndMigrationsPath(analysis.CodebasePath);
        if (migratorInfo != null)
        {
            sb.AppendLine();
            sb.AppendLine("### Database migrations (migrator):");
            sb.AppendLine($"- migrator: {migratorInfo.Value.MigratorPath}, migration_path: {migratorInfo.Value.MigrationsDirName}");
        }

        sb.AppendLine();
        sb.AppendLine("### Conventions:");
        sb.AppendLine($"- Private fields: {analysis.Conventions.PrivateFieldPrefix}fieldName");
        if (!string.IsNullOrEmpty(analysis.Conventions.TestFramework))
            sb.AppendLine($"- Test framework: {analysis.Conventions.TestFramework}");
        if (!string.IsNullOrEmpty(analysis.Conventions.DIFramework))
            sb.AppendLine($"- DI framework: {analysis.Conventions.DIFramework}");
        sb.AppendLine($"- Async suffix: {(analysis.Conventions.UsesAsyncSuffix ? "Yes" : "No")}");

        sb.AppendLine();
        sb.AppendLine("### File placement (use extensions from Languages section above):");
        sb.AppendLine("- Helper/utility code: [ProjectPath]/Helpers/[Name]<correct_extension>");
        sb.AppendLine("- Services: [ProjectPath]/Services/[Name]<correct_extension>");
        sb.AppendLine("- Models: [ProjectPath]/Models/[Name]<correct_extension>");
        sb.AppendLine("- Tests: in test project or same package with <correct_test_extension> (e.g. _test.go, .test.ts, Tests.cs for C#)");
        sb.AppendLine("- Follow existing namespace/package/module conventions in each project");

        return sb.ToString();
    }

    /// <summary>
    /// Detects a database migrator app and its migrations directory (e.g. scripts/migrator + psqlmigrations)
    /// so the planner can load planner-migrator rules. Returns null if not found.
    /// </summary>
    private static (string MigratorPath, string MigrationsDirName)? DetectMigratorAndMigrationsPath(string codebasePath)
    {
        if (string.IsNullOrEmpty(codebasePath) || !Directory.Exists(codebasePath))
            return null;

        var migrationDirNames = new[] { "psqlmigrations", "migrations" };
        try
        {
            foreach (var dir in Directory.GetDirectories(codebasePath, "*", SearchOption.AllDirectories))
            {
                var dirName = Path.GetFileName(dir);
                if (!dirName.Contains("migrator", StringComparison.OrdinalIgnoreCase))
                    continue;
                foreach (var migrationDir in migrationDirNames)
                {
                    var migrationPath = Path.Combine(dir, migrationDir);
                    if (Directory.Exists(migrationPath))
                    {
                        var relativeMigrator = Path.GetRelativePath(codebasePath, dir).Replace('\\', '/');
                        return (relativeMigrator, migrationDir);
                    }
                }
            }
        }
        catch
        {
            // Ignore access or path errors
        }

        return null;
    }
}