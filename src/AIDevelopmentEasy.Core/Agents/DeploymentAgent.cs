using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using AIDevelopmentEasy.Core.Models;
using Microsoft.Extensions.Logging;

namespace AIDevelopmentEasy.Core.Agents;

/// <summary>
/// Deployment Agent - Deploys generated code to the target codebase.
/// This agent does NOT use LLM - it performs file operations and builds.
/// 
/// Responsibilities:
/// 1. Copy generated files to the correct locations in the codebase
/// 2. Update .csproj files to include new files (if needed)
/// 3. Build affected projects using MSBuild
/// </summary>
public class DeploymentAgent
{
    private readonly ILogger<DeploymentAgent>? _logger;

    public DeploymentAgent(ILogger<DeploymentAgent>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Deploy generated files to the codebase using codebase analysis for accurate project paths
    /// </summary>
    /// <param name="codebaseAnalysis">Codebase analysis with project information</param>
    /// <param name="generatedFiles">Dictionary of relative path -> code content</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Deployment result with details</returns>
    public async Task<DeploymentResult> DeployAsync(
        CodebaseAnalysis codebaseAnalysis,
        Dictionary<string, string> generatedFiles,
        CancellationToken cancellationToken = default)
    {
        // Convert to DeploymentFile format without modification info
        var files = generatedFiles.Select(kvp => new DeploymentFile
        {
            RelativePath = kvp.Key,
            Content = kvp.Value,
            IsModification = false
        }).ToList();

        return await DeployAsync(codebaseAnalysis, files, cancellationToken);
    }

    /// <summary>
    /// Deploy generated files to the codebase with modification support.
    /// For modification files, only the specified method will be merged into the existing file.
    /// </summary>
    /// <param name="codebaseAnalysis">Codebase analysis with project information</param>
    /// <param name="files">List of files to deploy with modification metadata</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Deployment result with details</returns>
    public async Task<DeploymentResult> DeployAsync(
        CodebaseAnalysis codebaseAnalysis,
        List<DeploymentFile> files,
        CancellationToken cancellationToken = default)
    {
        var codebasePath = codebaseAnalysis.CodebasePath;
        _logger?.LogInformation("[Deployment] Starting deployment to: {Path}", codebasePath);
        _logger?.LogInformation("[Deployment] Files to deploy: {Count} ({Mods} modifications)",
            files.Count, files.Count(f => f.IsModification));
        _logger?.LogInformation("[Deployment] Available projects: {Projects}",
            string.Join(", ", codebaseAnalysis.Projects.Select(p => $"{p.Name} ({p.RelativePath})")));

        var result = new DeploymentResult
        {
            CodebasePath = codebasePath,
            StartedAt = DateTime.UtcNow
        };

        try
        {
            // Step 1: Analyze and map files to projects using codebase analysis
            var generatedFiles = files.ToDictionary(f => f.RelativePath, f => f.Content);
            var fileMappings = AnalyzeFileMappings(codebaseAnalysis, generatedFiles);
            
            // Apply modification metadata to mappings
            foreach (var mapping in fileMappings)
            {
                var sourceFile = files.FirstOrDefault(f => f.RelativePath == mapping.GeneratedPath);
                if (sourceFile != null)
                {
                    mapping.IsModification = sourceFile.IsModification;
                    mapping.TargetMethodName = sourceFile.TargetMethodName;
                    mapping.TargetClassName = sourceFile.TargetClassName;
                    
                    // Copy test file info for namespace conversion
                    mapping.IsTestFile = sourceFile.IsTestFile;
                    mapping.RealClassNamespace = sourceFile.RealClassNamespace;
                    mapping.RealClassName = sourceFile.RealClassName;
                }
            }
            
            _logger?.LogInformation("[Deployment] Mapped {Count} files to projects", fileMappings.Count);

            // Step 2: Copy files to their target locations (with merge for modifications)
            var copyResults = await CopyFilesToCodebaseAsync(codebasePath, fileMappings, cancellationToken);
            result.CopiedFiles = copyResults;

            // Step 3: Update .csproj files for new files
            var csprojUpdates = await UpdateCsprojFilesAsync(codebasePath, fileMappings, cancellationToken);
            result.UpdatedProjects = csprojUpdates;

            // Step 4: Build affected projects and their dependents
            var modifiedProjects = fileMappings
                .Select(f => f.ProjectPath)
                .Where(p => !string.IsNullOrEmpty(p))
                .Distinct()
                .ToList();

            var buildResults = await BuildProjectsAsync(codebaseAnalysis, modifiedProjects!, cancellationToken);
            result.BuildResults = buildResults;

            result.Success = buildResults.All(b => b.Success);
            result.CompletedAt = DateTime.UtcNow;

            _logger?.LogInformation("[Deployment] Deployment completed. Success: {Success}", result.Success);

            return result;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[Deployment] Deployment failed: {Message}", ex.Message);
            result.Success = false;
            result.Error = ex.Message;
            result.CompletedAt = DateTime.UtcNow;
            return result;
        }
    }

    /// <summary>
    /// Analyze generated file paths and map them to target projects using pre-calculated 
    /// namespace-folder mappings from codebase analysis.
    /// </summary>
    private List<FileMapping> AnalyzeFileMappings(CodebaseAnalysis analysis, Dictionary<string, string> generatedFiles)
    {
        var mappings = new List<FileMapping>();
        var codebasePath = analysis.CodebasePath;

        // Build lookup: full namespace -> (project, folder path)
        var namespaceToProjectFolder = new Dictionary<string, (ProjectInfo Project, string FolderPath)>(StringComparer.OrdinalIgnoreCase);

        foreach (var proj in analysis.Projects)
        {
            var projectDir = !string.IsNullOrEmpty(proj.ProjectDirectory)
                ? proj.ProjectDirectory
                : Path.GetDirectoryName(proj.RelativePath) ?? "";

            // Add mappings from pre-calculated NamespaceFolderMap
            if (proj.NamespaceFolderMap.Count > 0 && !string.IsNullOrEmpty(proj.RootNamespace))
            {
                foreach (var (nsSuffix, folderPath) in proj.NamespaceFolderMap)
                {
                    var fullNs = string.IsNullOrEmpty(nsSuffix)
                        ? proj.RootNamespace
                        : $"{proj.RootNamespace}.{nsSuffix}";

                    var fullFolderPath = string.IsNullOrEmpty(folderPath)
                        ? projectDir
                        : Path.Combine(projectDir, folderPath.Replace('/', Path.DirectorySeparatorChar));

                    if (!namespaceToProjectFolder.ContainsKey(fullNs))
                    {
                        namespaceToProjectFolder[fullNs] = (proj, fullFolderPath);
                    }
                }
            }

            // Also add all known namespaces from the project
            foreach (var ns in proj.Namespaces)
            {
                if (!namespaceToProjectFolder.ContainsKey(ns))
                {
                    // Try to find folder from NamespaceFolderMap
                    var nsSuffix = ns.StartsWith(proj.RootNamespace + ".", StringComparison.OrdinalIgnoreCase)
                        ? ns.Substring(proj.RootNamespace.Length + 1)
                        : (ns.Equals(proj.RootNamespace, StringComparison.OrdinalIgnoreCase) ? "" : ns);

                    if (proj.NamespaceFolderMap.TryGetValue(nsSuffix, out var folderPath))
                    {
                        var fullFolderPath = string.IsNullOrEmpty(folderPath)
                            ? projectDir
                            : Path.Combine(projectDir, folderPath.Replace('/', Path.DirectorySeparatorChar));
                        namespaceToProjectFolder[ns] = (proj, fullFolderPath);
                    }
                    else
                    {
                        // Fallback: convert namespace suffix to folder path
                        var folderFromNs = nsSuffix.Replace('.', Path.DirectorySeparatorChar);
                        var fullFolderPath = string.IsNullOrEmpty(folderFromNs)
                            ? projectDir
                            : Path.Combine(projectDir, folderFromNs);
                        namespaceToProjectFolder[ns] = (proj, fullFolderPath);
                    }
                }
            }

            // Add root namespace mapping
            if (!string.IsNullOrEmpty(proj.RootNamespace) && !namespaceToProjectFolder.ContainsKey(proj.RootNamespace))
            {
                namespaceToProjectFolder[proj.RootNamespace] = (proj, projectDir);
            }
        }

        _logger?.LogDebug("[Deployment] Built namespace lookup with {Count} entries", namespaceToProjectFolder.Count);

        foreach (var (generatedPath, content) in generatedFiles)
        {
            var mapping = new FileMapping
            {
                GeneratedPath = generatedPath,
                Content = content
            };

            _logger?.LogDebug("[Deployment] Analyzing file: {Path}", generatedPath);

            // Extract namespace from file content
            var nsMatch = Regex.Match(content, @"namespace\s+([\w.]+)");
            if (!nsMatch.Success)
            {
                _logger?.LogWarning("[Deployment] No namespace found in file: {Path}", generatedPath);
                mappings.Add(mapping);
                continue;
            }

            var fileNamespace = nsMatch.Groups[1].Value;
            var fileName = Path.GetFileName(generatedPath);

            // Try exact namespace match first
            if (namespaceToProjectFolder.TryGetValue(fileNamespace, out var exactMatch))
            {
                mapping.ProjectName = exactMatch.Project.Name;
                mapping.ProjectPath = exactMatch.Project.Path;
                mapping.TargetPath = Path.Combine(codebasePath, exactMatch.FolderPath, fileName);

                _logger?.LogInformation("[Deployment] Mapped (exact): {Generated} -> {Target} (Project: {Project}, NS: {NS})",
                    generatedPath, mapping.TargetPath, exactMatch.Project.Name, fileNamespace);
            }
            else
            {
                // Try progressively shorter namespace prefixes
                var nsParts = fileNamespace.Split('.');
                ProjectInfo? matchedProject = null;
                string targetFolder = "";
                string matchedPrefix = "";

                for (int len = nsParts.Length - 1; len >= 1; len--)
                {
                    var nsPrefix = string.Join(".", nsParts.Take(len));

                    if (namespaceToProjectFolder.TryGetValue(nsPrefix, out var prefixMatch))
                    {
                        matchedProject = prefixMatch.Project;
                        matchedPrefix = nsPrefix;

                        // Calculate the additional folder path from the namespace suffix
                        var nsSuffix = fileNamespace.Substring(nsPrefix.Length).TrimStart('.');
                        var additionalPath = nsSuffix.Replace('.', Path.DirectorySeparatorChar);

                        targetFolder = string.IsNullOrEmpty(additionalPath)
                            ? prefixMatch.FolderPath
                            : Path.Combine(prefixMatch.FolderPath, additionalPath);

                        break;
                    }
                }

                if (matchedProject != null)
                {
                    mapping.ProjectName = matchedProject.Name;
                    mapping.ProjectPath = matchedProject.Path;
                    mapping.TargetPath = Path.Combine(codebasePath, targetFolder, fileName);

                    _logger?.LogInformation("[Deployment] Mapped (prefix): {Generated} -> {Target} (Project: {Project}, NS: {NS}, Prefix: {Prefix})",
                        generatedPath, mapping.TargetPath, matchedProject.Name, fileNamespace, matchedPrefix);
                }
                else
                {
                    // Path-based fallback: find project from generated path and use NamespaceFolderMap
                    var pathBasedMapping = TryPathBasedMapping(analysis, generatedPath, fileNamespace, fileName);

                    if (pathBasedMapping.HasValue)
                    {
                        mapping.ProjectName = pathBasedMapping.Value.Project.Name;
                        mapping.ProjectPath = pathBasedMapping.Value.Project.Path;
                        mapping.TargetPath = Path.Combine(codebasePath, pathBasedMapping.Value.TargetFolder, fileName);

                        _logger?.LogInformation("[Deployment] Mapped (path): {Generated} -> {Target} (Project: {Project}, NS: {NS})",
                            generatedPath, mapping.TargetPath, pathBasedMapping.Value.Project.Name, fileNamespace);
                    }
                    else
                    {
                        // Ultimate fallback: use the generated path structure
                        var pathParts = generatedPath.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
                        var startIndex = pathParts.Length > 2 && !pathParts[0].Contains('.') ? 1 : 0;
                        mapping.TargetPath = Path.Combine(codebasePath,
                            string.Join(Path.DirectorySeparatorChar.ToString(), pathParts.Skip(startIndex)));

                        _logger?.LogWarning("[Deployment] No mapping found for namespace '{NS}', using fallback: {Target}",
                            fileNamespace, mapping.TargetPath);
                    }
                }
            }

            mappings.Add(mapping);
        }

        return mappings;
    }

    /// <summary>
    /// Try to map a file using path-based project detection and namespace folder mapping
    /// </summary>
    private (ProjectInfo Project, string TargetFolder)? TryPathBasedMapping(
        CodebaseAnalysis analysis, string generatedPath, string fileNamespace, string fileName)
    {
        var pathParts = generatedPath.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);

        // Find project by name in the path
        foreach (var part in pathParts)
        {
            var matchedProject = analysis.Projects.FirstOrDefault(p =>
                p.Name.Equals(part, StringComparison.OrdinalIgnoreCase));

            if (matchedProject != null)
            {
                _logger?.LogDebug("[Deployment] Found project in path: {Part} -> {Project}", part, matchedProject.Name);

                // Get project directory
                var projectDir = !string.IsNullOrEmpty(matchedProject.ProjectDirectory)
                    ? matchedProject.ProjectDirectory
                    : Path.GetDirectoryName(matchedProject.RelativePath) ?? "";

                // Try to find folder mapping using the namespace suffix
                // The file might have short namespace like "Helpers" instead of "Picus.Common.Helpers"
                // Check if the namespace matches any suffix in NamespaceFolderMap
                if (matchedProject.NamespaceFolderMap.TryGetValue(fileNamespace, out var folderPath))
                {
                    var targetFolder = string.IsNullOrEmpty(folderPath)
                        ? projectDir
                        : Path.Combine(projectDir, folderPath.Replace('/', Path.DirectorySeparatorChar));

                    _logger?.LogDebug("[Deployment] Found folder mapping: NS={NS} -> Folder={Folder}",
                        fileNamespace, folderPath);

                    return (matchedProject, targetFolder);
                }

                // Also try without common prefixes like "UnitTests.", "Tests."
                var strippedNs = fileNamespace
                    .Replace("UnitTests.", "")
                    .Replace("UnitTest.", "")
                    .Replace("Tests.", "")
                    .Replace("Test.", "");

                if (strippedNs != fileNamespace && matchedProject.NamespaceFolderMap.TryGetValue(strippedNs, out var strippedFolder))
                {
                    var targetFolder = string.IsNullOrEmpty(strippedFolder)
                        ? projectDir
                        : Path.Combine(projectDir, strippedFolder.Replace('/', Path.DirectorySeparatorChar));

                    _logger?.LogDebug("[Deployment] Found folder mapping (stripped): NS={NS} -> Folder={Folder}",
                        strippedNs, strippedFolder);

                    return (matchedProject, targetFolder);
                }

                // Fallback: use namespace as folder path if it looks like a subfolder
                if (!fileNamespace.Contains('.') && !string.IsNullOrEmpty(fileNamespace))
                {
                    // Simple namespace like "Helpers" - use it as folder name
                    var targetFolder = Path.Combine(projectDir, fileNamespace);

                    _logger?.LogDebug("[Deployment] Using namespace as folder: {NS}", fileNamespace);

                    return (matchedProject, targetFolder);
                }

                // Last resort: just use project directory
                return (matchedProject, projectDir);
            }
        }

        return null;
    }

    /// <summary>
    /// Copy files to their target locations in the codebase.
    /// For modification tasks, merges the generated code into existing files.
    /// </summary>
    private async Task<List<FileCopyResult>> CopyFilesToCodebaseAsync(
        string codebasePath,
        List<FileMapping> mappings,
        CancellationToken cancellationToken)
    {
        var results = new List<FileCopyResult>();

        foreach (var mapping in mappings)
        {
            var result = new FileCopyResult
            {
                SourcePath = mapping.GeneratedPath,
                TargetPath = mapping.TargetPath ?? ""
            };

            try
            {
                if (string.IsNullOrEmpty(mapping.TargetPath))
                {
                    result.Success = false;
                    result.Error = "Could not determine target path";
                    results.Add(result);
                    continue;
                }

                // Create directory if it doesn't exist
                var targetDir = Path.GetDirectoryName(mapping.TargetPath);
                if (!string.IsNullOrEmpty(targetDir) && !Directory.Exists(targetDir))
                {
                    Directory.CreateDirectory(targetDir);
                    _logger?.LogInformation("[Deployment] Created directory: {Dir}", targetDir);
                }

                // Check if file exists
                var fileExists = File.Exists(mapping.TargetPath);
                result.IsNewFile = !fileExists;

                string finalContent;

                // If this is a modification and the file exists, merge the changes
                if (mapping.IsModification && fileExists && !string.IsNullOrEmpty(mapping.TargetMethodName))
                {
                    var existingContent = await File.ReadAllTextAsync(mapping.TargetPath, cancellationToken);
                    finalContent = MergeMethodIntoFile(existingContent, mapping.Content, mapping.TargetMethodName, mapping.TargetClassName);
                    
                    _logger?.LogInformation("[Deployment] ════════════════════════════════════════════════════");
                    _logger?.LogInformation("[Deployment] METHOD MERGE COMPLETED:");
                    _logger?.LogInformation("[Deployment]   File: {Path}", mapping.TargetPath);
                    _logger?.LogInformation("[Deployment]   Method: {Method}()", mapping.TargetMethodName);
                    _logger?.LogInformation("[Deployment]   Class: {Class}", mapping.TargetClassName ?? "N/A");
                    _logger?.LogInformation("[Deployment]   Original size: {Size} chars", existingContent.Length);
                    _logger?.LogInformation("[Deployment]   Final size: {Size} chars", finalContent.Length);
                    _logger?.LogInformation("[Deployment] ════════════════════════════════════════════════════");
                }
                else
                {
                    // New file or full replacement
                    finalContent = mapping.Content;
                    
                    if (mapping.IsModification)
                    {
                        _logger?.LogInformation("[Deployment] Full file replacement: {Path}", mapping.TargetPath);
                    }
                }

                // For test files, convert dummy namespace references to real class namespace
                if (mapping.IsTestFile && !string.IsNullOrEmpty(mapping.RealClassNamespace))
                {
                    finalContent = ConvertTestFileNamespaces(finalContent, mapping.RealClassNamespace, mapping.RealClassName);
                    
                    _logger?.LogInformation("[Deployment] ════════════════════════════════════════════════════");
                    _logger?.LogInformation("[Deployment] TEST FILE NAMESPACE CONVERSION:");
                    _logger?.LogInformation("[Deployment]   File: {Path}", mapping.TargetPath);
                    _logger?.LogInformation("[Deployment]   Target Class: {Class}", mapping.RealClassName ?? "N/A");
                    _logger?.LogInformation("[Deployment]   Real Namespace: {NS}", mapping.RealClassNamespace);
                    _logger?.LogInformation("[Deployment] ════════════════════════════════════════════════════");
                }

                // Write the file
                await File.WriteAllTextAsync(mapping.TargetPath, finalContent, cancellationToken);
                result.Success = true;

                _logger?.LogInformation("[Deployment] {Action} file: {Path}",
                    result.IsNewFile ? "Created" : "Updated", mapping.TargetPath);
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Error = ex.Message;
                _logger?.LogError(ex, "[Deployment] Failed to copy file: {Path}", mapping.TargetPath);
            }

            results.Add(result);
        }

        return results;
    }

    /// <summary>
    /// Convert dummy namespace references in test file to real class namespace.
    /// This allows test files to be compiled with dummy wrappers during debugging,
    /// then converted to use real class references during deployment.
    /// </summary>
    private string ConvertTestFileNamespaces(string testContent, string realNamespace, string? realClassName)
    {
        var result = testContent;

        // Add using statement for the real namespace if not already present
        if (!result.Contains($"using {realNamespace};"))
        {
            // Find the last using statement and add after it
            var lastUsingMatch = Regex.Match(result, @"(using\s+[^;]+;\s*\n)(?!using)", RegexOptions.RightToLeft);
            if (lastUsingMatch.Success)
            {
                var insertPosition = lastUsingMatch.Index + lastUsingMatch.Length;
                result = result.Insert(insertPosition, $"using {realNamespace};\n");
                _logger?.LogDebug("[Deployment] Added using statement: using {NS};", realNamespace);
            }
            else
            {
                // No using statements found, add at the beginning
                result = $"using {realNamespace};\n{result}";
            }
        }

        // Remove any dummy/test namespace using statements that might conflict
        result = Regex.Replace(result, @"using\s+TargetedModification\s*;\s*\n?", "");
        result = Regex.Replace(result, @"using\s+DummyNamespace\s*;\s*\n?", "");

        // If there's a dummy wrapper class instantiation, it should be removed or converted
        // This is a safety measure in case the LLM generated dummy references

        _logger?.LogDebug("[Deployment] Test file namespace conversion completed");
        return result;
    }

    /// <summary>
    /// Merge a generated method into an existing file by replacing the old method implementation.
    /// </summary>
    private string MergeMethodIntoFile(string existingContent, string generatedContent, string methodName, string? className)
    {
        _logger?.LogDebug("[Deployment] Merging method '{Method}' into existing file", methodName);

        // Extract the new method from generated content
        var newMethod = ExtractMethod(generatedContent, methodName);
        
        if (string.IsNullOrEmpty(newMethod))
        {
            _logger?.LogWarning("[Deployment] Could not extract method '{Method}' from generated content, using full replacement", methodName);
            return generatedContent;
        }

        // Find and replace the old method in existing content
        var oldMethod = ExtractMethod(existingContent, methodName);
        
        if (string.IsNullOrEmpty(oldMethod))
        {
            _logger?.LogWarning("[Deployment] Could not find method '{Method}' in existing file, using full replacement", methodName);
            return generatedContent;
        }

        // Replace old method with new method
        var result = existingContent.Replace(oldMethod, newMethod);
        
        _logger?.LogInformation("[Deployment] Successfully merged method '{Method}' ({OldLen} -> {NewLen} chars)",
            methodName, oldMethod.Length, newMethod.Length);

        return result;
    }

    /// <summary>
    /// Extract a method from C# source code including its signature and body.
    /// </summary>
    private string? ExtractMethod(string sourceCode, string methodName)
    {
        if (string.IsNullOrEmpty(sourceCode) || string.IsNullOrEmpty(methodName))
            return null;

        // Pattern to match method with various modifiers, return types, and generic parameters
        // Captures: modifiers + return type + method name + generic params + parameters + where clauses + body
        var pattern = $@"((?:(?:public|private|protected|internal|static|virtual|override|abstract|async|sealed|new|partial)\s+)*[\w<>\[\],\s\?]+\s+{Regex.Escape(methodName)}\s*(?:<[^>]+>)?\s*\([^)]*\)\s*(?:where[^{{]+)?\s*\{{)";
        
        var match = Regex.Match(sourceCode, pattern, RegexOptions.Singleline);
        
        if (!match.Success)
        {
            _logger?.LogDebug("[Deployment] Method pattern did not match for '{Method}'", methodName);
            return null;
        }

        // Find the matching closing brace
        var startIndex = match.Index;
        var braceCount = 0;
        var endIndex = startIndex;
        var inString = false;
        var inChar = false;
        var inVerbatimString = false;
        var escaped = false;

        for (var i = match.Index + match.Length - 1; i < sourceCode.Length; i++)
        {
            var c = sourceCode[i];
            var prevChar = i > 0 ? sourceCode[i - 1] : '\0';

            if (escaped)
            {
                escaped = false;
                continue;
            }

            if (c == '\\' && (inString || inChar) && !inVerbatimString)
            {
                escaped = true;
                continue;
            }

            // Handle verbatim strings
            if (c == '@' && i + 1 < sourceCode.Length && sourceCode[i + 1] == '"' && !inString && !inChar)
            {
                inVerbatimString = true;
                i++; // Skip the quote
                continue;
            }

            if (inVerbatimString)
            {
                if (c == '"')
                {
                    // Check for escaped quote in verbatim string
                    if (i + 1 < sourceCode.Length && sourceCode[i + 1] == '"')
                    {
                        i++; // Skip the escaped quote
                        continue;
                    }
                    inVerbatimString = false;
                }
                continue;
            }

            if (c == '"' && !inChar)
            {
                inString = !inString;
                continue;
            }

            if (c == '\'' && !inString)
            {
                inChar = !inChar;
                continue;
            }

            if (!inString && !inChar)
            {
                if (c == '{') braceCount++;
                if (c == '}')
                {
                    braceCount--;
                    if (braceCount == 0)
                    {
                        endIndex = i + 1;
                        break;
                    }
                }
            }
        }

        if (endIndex > startIndex)
        {
            // Include any XML documentation comments before the method
            var methodWithDocs = IncludeXmlDocumentation(sourceCode, startIndex, endIndex);
            return methodWithDocs;
        }

        return null;
    }

    /// <summary>
    /// Include XML documentation comments that precede a method
    /// </summary>
    private string IncludeXmlDocumentation(string sourceCode, int methodStart, int methodEnd)
    {
        var methodCode = sourceCode.Substring(methodStart, methodEnd - methodStart);
        
        // Look backwards for XML comments
        var searchStart = methodStart;
        var lines = sourceCode.Substring(0, methodStart).Split('\n');
        
        // Find the start of XML docs (if any)
        var xmlDocStart = methodStart;
        for (int i = lines.Length - 1; i >= 0; i--)
        {
            var line = lines[i].Trim();
            
            if (line.StartsWith("///") || line.StartsWith("[") || string.IsNullOrWhiteSpace(line))
            {
                // This could be part of the method's documentation/attributes
                xmlDocStart -= lines[i].Length + 1; // +1 for newline
            }
            else
            {
                break;
            }
        }

        // Ensure we don't go negative
        if (xmlDocStart < 0) xmlDocStart = 0;
        
        // Find actual start (skip leading whitespace but keep indentation)
        while (xmlDocStart < methodStart && (sourceCode[xmlDocStart] == '\r' || sourceCode[xmlDocStart] == '\n'))
        {
            xmlDocStart++;
        }

        return sourceCode.Substring(xmlDocStart, methodEnd - xmlDocStart);
    }

    /// <summary>
    /// Update .csproj files to include new files (if they use explicit includes)
    /// </summary>
    private async Task<List<CsprojUpdateResult>> UpdateCsprojFilesAsync(
        string codebasePath,
        List<FileMapping> mappings,
        CancellationToken cancellationToken)
    {
        var results = new List<CsprojUpdateResult>();

        // Group new files by project
        var newFilesByProject = mappings
            .Where(m => !string.IsNullOrEmpty(m.ProjectPath))
            .GroupBy(m => m.ProjectPath!)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var (csprojPath, files) in newFilesByProject)
        {
            var result = new CsprojUpdateResult
            {
                ProjectPath = csprojPath,
                AddedFiles = new List<string>()
            };

            try
            {
                if (!File.Exists(csprojPath))
                {
                    result.Success = false;
                    result.Error = "Project file not found";
                    results.Add(result);
                    continue;
                }

                var csprojContent = await File.ReadAllTextAsync(csprojPath, cancellationToken);
                var doc = XDocument.Parse(csprojContent);
                var projectDir = Path.GetDirectoryName(csprojPath)!;

                // Check if this is an SDK-style project (no explicit Compile includes needed)
                var sdkAttribute = doc.Root?.Attribute("Sdk");
                if (sdkAttribute != null)
                {
                    // SDK-style project - no need to add Compile items
                    result.Success = true;
                    result.Message = "SDK-style project - files auto-included";
                    results.Add(result);
                    continue;
                }

                // Traditional .NET Framework project - need to add Compile items
                var ns = doc.Root?.GetDefaultNamespace() ?? XNamespace.None;
                var itemGroups = doc.Descendants(ns + "ItemGroup").ToList();
                var compileItemGroup = itemGroups.FirstOrDefault(ig => ig.Elements(ns + "Compile").Any());

                if (compileItemGroup == null)
                {
                    // Create a new ItemGroup for Compile items
                    compileItemGroup = new XElement(ns + "ItemGroup");
                    doc.Root?.Add(compileItemGroup);
                }

                bool modified = false;
                foreach (var mapping in files)
                {
                    if (string.IsNullOrEmpty(mapping.TargetPath)) continue;

                    // Calculate relative path from project file
                    var relativePath = Path.GetRelativePath(projectDir, mapping.TargetPath);

                    // Check if already exists
                    var existingCompile = compileItemGroup.Elements(ns + "Compile")
                        .FirstOrDefault(e => e.Attribute("Include")?.Value
                            .Equals(relativePath, StringComparison.OrdinalIgnoreCase) == true);

                    if (existingCompile == null)
                    {
                        // Add new Compile item
                        compileItemGroup.Add(new XElement(ns + "Compile",
                            new XAttribute("Include", relativePath)));
                        result.AddedFiles.Add(relativePath);
                        modified = true;
                        _logger?.LogInformation("[Deployment] Added to {Project}: {File}",
                            Path.GetFileName(csprojPath), relativePath);
                    }
                }

                if (modified)
                {
                    // Save the updated csproj
                    await File.WriteAllTextAsync(csprojPath, doc.ToString(), cancellationToken);
                }

                result.Success = true;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Error = ex.Message;
                _logger?.LogError(ex, "[Deployment] Failed to update csproj: {Path}", csprojPath);
            }

            results.Add(result);
        }

        return results;
    }

    /// <summary>
    /// Build affected projects and their dependents using MSBuild.
    /// Does NOT build the entire solution - only modified projects and projects that reference them.
    /// </summary>
    private async Task<List<BuildResult>> BuildProjectsAsync(
        CodebaseAnalysis analysis,
        List<string> modifiedProjectPaths,
        CancellationToken cancellationToken)
    {
        var results = new List<BuildResult>();
        var msbuildPath = FindMSBuildPath();

        if (string.IsNullOrEmpty(msbuildPath))
        {
            _logger?.LogWarning("[Deployment] MSBuild not found - skipping build verification");
            results.Add(new BuildResult
            {
                Success = false,
                Error = "MSBuild not found. Please install Visual Studio or Build Tools."
            });
            return results;
        }

        // Get names of modified projects for dependency lookup
        var modifiedProjectNames = modifiedProjectPaths
            .Select(p => Path.GetFileNameWithoutExtension(p))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        _logger?.LogInformation("[Deployment] Modified projects: {Projects}",
            string.Join(", ", modifiedProjectNames));

        // Find all projects that reference the modified projects (dependents)
        var dependentProjects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var project in analysis.Projects)
        {
            // Check if this project references any of the modified projects
            foreach (var reference in project.ProjectReferences)
            {
                var refName = Path.GetFileNameWithoutExtension(reference);
                if (modifiedProjectNames.Contains(refName))
                {
                    var fullPath = Path.Combine(analysis.CodebasePath, project.RelativePath);
                    if (File.Exists(fullPath))
                    {
                        dependentProjects.Add(fullPath);
                        _logger?.LogInformation("[Deployment] Found dependent project: {Project} (references {Ref})",
                            project.Name, refName);
                    }
                    break;
                }
            }
        }

        // Build order: first modified projects, then dependent projects
        var projectsToBuild = new List<string>();

        // Add modified projects first
        foreach (var path in modifiedProjectPaths.Distinct())
        {
            if (File.Exists(path))
            {
                projectsToBuild.Add(path);
                _logger?.LogInformation("[Deployment] Will build MODIFIED project: {Project}", 
                    Path.GetFileNameWithoutExtension(path));
            }
        }

        // Add dependent projects (projects that reference the modified ones)
        foreach (var path in dependentProjects)
        {
            if (!projectsToBuild.Contains(path, StringComparer.OrdinalIgnoreCase))
            {
                projectsToBuild.Add(path);
                _logger?.LogInformation("[Deployment] Will build DEPENDENT project: {Project} (references modified code)", 
                    Path.GetFileNameWithoutExtension(path));
            }
        }

        _logger?.LogInformation("[Deployment] ═══════════════════════════════════════════════════════════");
        _logger?.LogInformation("[Deployment] BUILD VERIFICATION: {Count} projects total", projectsToBuild.Count);
        _logger?.LogInformation("[Deployment]   - Modified: {Modified}", modifiedProjectPaths.Count);
        _logger?.LogInformation("[Deployment]   - Dependents: {Dependents}", dependentProjects.Count);
        _logger?.LogInformation("[Deployment] ═══════════════════════════════════════════════════════════");

        // Build each project
        foreach (var projectPath in projectsToBuild)
        {
            var projectName = Path.GetFileNameWithoutExtension(projectPath);

            // Use "LocalTest" configuration for unit test projects, "Debug" for others
            // LocalTest configuration is used for local testing before PR creation
            var isTestProject = projectName.Contains("Test", StringComparison.OrdinalIgnoreCase) ||
                               projectName.Contains("UnitTest", StringComparison.OrdinalIgnoreCase);
            var configuration = isTestProject ? "LocalTest" : "Debug";

            _logger?.LogInformation("[Deployment] Building project: {Project} (Configuration: {Config})",
                projectName, configuration);

            var buildResult = await BuildWithMSBuildAsync(msbuildPath, projectPath, configuration, cancellationToken);
            results.Add(buildResult);

            // Log build result
            if (buildResult.Success)
            {
                _logger?.LogInformation("[Deployment] ✓ Build PASSED: {Project}", projectName);
            }
            else
            {
                _logger?.LogWarning("[Deployment] ✗ Build FAILED: {Project}", projectName);
                _logger?.LogWarning("[Deployment]   Error: {Error}", buildResult.Error);
                
                // If a dependent project fails, it means the method change broke something
                if (dependentProjects.Contains(projectPath, StringComparer.OrdinalIgnoreCase))
                {
                    _logger?.LogError("[Deployment] ⚠️ BREAKING CHANGE DETECTED: Project '{Project}' that references the modified code failed to build!",
                        projectName);
                }
            }
        }

        // Summary
        var passedCount = results.Count(r => r.Success);
        var failedCount = results.Count(r => !r.Success);
        _logger?.LogInformation("[Deployment] ═══════════════════════════════════════════════════════════");
        _logger?.LogInformation("[Deployment] BUILD SUMMARY: {Passed} passed, {Failed} failed", passedCount, failedCount);
        _logger?.LogInformation("[Deployment] ═══════════════════════════════════════════════════════════");

        return results;
    }

    private async Task<BuildResult> BuildWithMSBuildAsync(
        string msbuildPath,
        string targetPath,
        string configuration,
        CancellationToken cancellationToken)
    {
        var result = new BuildResult
        {
            TargetPath = targetPath
        };

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = msbuildPath,
                Arguments = $"\"{targetPath}\" /t:Build /p:Configuration={configuration} /nologo /v:minimal /m",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(targetPath)
            };

            using var process = new Process { StartInfo = psi };
            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

            process.OutputDataReceived += (_, e) => { if (e.Data != null) outputBuilder.AppendLine(e.Data); };
            process.ErrorDataReceived += (_, e) => { if (e.Data != null) errorBuilder.AppendLine(e.Data); };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            var completed = await Task.Run(() => process.WaitForExit(300000), cancellationToken); // 5 min timeout

            if (!completed)
            {
                try { process.Kill(); } catch { }
                result.Success = false;
                result.Error = "Build timed out (5 minutes)";
                return result;
            }

            result.Output = outputBuilder.ToString();
            var errors = errorBuilder.ToString();
            var allOutput = string.Join("\n", result.Output, errors).Trim();

            if (process.ExitCode != 0 || allOutput.Contains("error CS") || allOutput.Contains("error MSB"))
            {
                result.Success = false;
                result.Error = $"Build failed with exit code {process.ExitCode}";
                result.Output = allOutput;
                _logger?.LogWarning("[Deployment] Build failed: {Output}", allOutput);
            }
            else
            {
                result.Success = true;
                _logger?.LogInformation("[Deployment] Build successful: {Target}", Path.GetFileName(targetPath));
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = ex.Message;
            _logger?.LogError(ex, "[Deployment] Build exception: {Message}", ex.Message);
        }

        return result;
    }

    private string? FindMSBuildPath()
    {
        var possiblePaths = new[]
        {
            // Visual Studio 2022
            @"C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe",
            @"C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe",
            @"C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe",
            @"C:\Program Files\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe",
            // Visual Studio 2019
            @"C:\Program Files (x86)\Microsoft Visual Studio\2019\Enterprise\MSBuild\Current\Bin\MSBuild.exe",
            @"C:\Program Files (x86)\Microsoft Visual Studio\2019\Professional\MSBuild\Current\Bin\MSBuild.exe",
            @"C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe",
            @"C:\Program Files (x86)\Microsoft Visual Studio\2019\BuildTools\MSBuild\Current\Bin\MSBuild.exe",
            // .NET Framework MSBuild
            @"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe",
            @"C:\Windows\Microsoft.NET\Framework\v4.0.30319\MSBuild.exe",
        };

        foreach (var path in possiblePaths)
        {
            if (File.Exists(path))
            {
                _logger?.LogDebug("[Deployment] Found MSBuild at: {Path}", path);
                return path;
            }
        }

        // Try vswhere
        try
        {
            var vswherePath = @"C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe";
            if (File.Exists(vswherePath))
            {
                var psi = new ProcessStartInfo
                {
                    FileName = vswherePath,
                    Arguments = "-latest -requires Microsoft.Component.MSBuild -find MSBuild\\**\\Bin\\MSBuild.exe",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process != null)
                {
                    var output = process.StandardOutput.ReadToEnd().Trim();
                    process.WaitForExit(5000);

                    if (!string.IsNullOrEmpty(output) && File.Exists(output.Split('\n')[0].Trim()))
                    {
                        return output.Split('\n')[0].Trim();
                    }
                }
            }
        }
        catch { }

        return null;
    }

    /// <summary>
    /// Rollback a deployment - delete created files and revert csproj changes
    /// </summary>
    public async Task<RollbackResult> RollbackAsync(DeploymentResult deployment, CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("[Deployment] Starting rollback for deployment at: {Path}", deployment.CodebasePath);

        var result = new RollbackResult
        {
            DeploymentPath = deployment.CodebasePath,
            StartedAt = DateTime.UtcNow
        };

        try
        {
            // Delete newly created files (not modified ones)
            foreach (var file in deployment.CopiedFiles.Where(f => f.Success && f.IsNewFile))
            {
                try
                {
                    if (File.Exists(file.TargetPath))
                    {
                        File.Delete(file.TargetPath);
                        result.DeletedFiles.Add(file.TargetPath);
                        _logger?.LogInformation("[Rollback] Deleted file: {Path}", file.TargetPath);

                        // Try to remove empty parent directories
                        var dir = Path.GetDirectoryName(file.TargetPath);
                        while (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                        {
                            if (!Directory.EnumerateFileSystemEntries(dir).Any())
                            {
                                Directory.Delete(dir);
                                result.DeletedDirectories.Add(dir);
                                _logger?.LogInformation("[Rollback] Deleted empty directory: {Dir}", dir);
                                dir = Path.GetDirectoryName(dir);
                            }
                            else
                            {
                                break;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"Failed to delete {file.TargetPath}: {ex.Message}");
                    _logger?.LogWarning(ex, "[Rollback] Failed to delete file: {Path}", file.TargetPath);
                }
            }

            // Revert csproj changes (remove added compile items)
            foreach (var update in deployment.UpdatedProjects.Where(u => u.Success && u.AddedFiles.Any()))
            {
                try
                {
                    await RevertCsprojChangesAsync(update.ProjectPath, update.AddedFiles, cancellationToken);
                    result.RevertedProjects.Add(update.ProjectPath);
                    _logger?.LogInformation("[Rollback] Reverted csproj: {Path}", update.ProjectPath);
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"Failed to revert {update.ProjectPath}: {ex.Message}");
                    _logger?.LogWarning(ex, "[Rollback] Failed to revert csproj: {Path}", update.ProjectPath);
                }
            }

            result.Success = result.Errors.Count == 0;
            result.CompletedAt = DateTime.UtcNow;

            _logger?.LogInformation("[Rollback] Rollback completed. Deleted {Files} files, {Dirs} directories, reverted {Projects} projects",
                result.DeletedFiles.Count, result.DeletedDirectories.Count, result.RevertedProjects.Count);

            return result;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[Rollback] Rollback failed: {Message}", ex.Message);
            result.Success = false;
            result.Errors.Add(ex.Message);
            result.CompletedAt = DateTime.UtcNow;
            return result;
        }
    }

    /// <summary>
    /// Remove compile items from a csproj file
    /// </summary>
    private async Task RevertCsprojChangesAsync(string csprojPath, List<string> filesToRemove, CancellationToken cancellationToken)
    {
        if (!File.Exists(csprojPath) || filesToRemove.Count == 0)
            return;

        var csprojContent = await File.ReadAllTextAsync(csprojPath, cancellationToken);
        var doc = XDocument.Parse(csprojContent);
        var ns = doc.Root?.GetDefaultNamespace() ?? XNamespace.None;

        bool modified = false;
        foreach (var fileToRemove in filesToRemove)
        {
            var compileElements = doc.Descendants(ns + "Compile")
                .Where(e => e.Attribute("Include")?.Value.Equals(fileToRemove, StringComparison.OrdinalIgnoreCase) == true)
                .ToList();

            foreach (var element in compileElements)
            {
                element.Remove();
                modified = true;
                _logger?.LogDebug("[Rollback] Removed compile item: {File} from {Project}", fileToRemove, Path.GetFileName(csprojPath));
            }
        }

        if (modified)
        {
            await File.WriteAllTextAsync(csprojPath, doc.ToString(), cancellationToken);
        }
    }
}

#region Result Models

/// <summary>
/// Represents a file to be deployed with optional modification metadata
/// </summary>
public class DeploymentFile
{
    /// <summary>
    /// Relative path of the file (e.g., "ProjectName/Folder/ClassName.cs")
    /// </summary>
    public string RelativePath { get; set; } = "";
    
    /// <summary>
    /// The generated code content
    /// </summary>
    public string Content { get; set; } = "";
    
    /// <summary>
    /// If true, this is a modification to an existing file.
    /// Only the specified method will be merged, not the entire file replaced.
    /// </summary>
    public bool IsModification { get; set; }
    
    /// <summary>
    /// The specific method name to replace (for targeted modifications)
    /// </summary>
    public string? TargetMethodName { get; set; }
    
    /// <summary>
    /// The specific class name being modified
    /// </summary>
    public string? TargetClassName { get; set; }

    /// <summary>
    /// If true, this is a test file that needs namespace conversion.
    /// </summary>
    public bool IsTestFile { get; set; }

    /// <summary>
    /// The real namespace of the class being tested (for test files).
    /// </summary>
    public string? RealClassNamespace { get; set; }

    /// <summary>
    /// The real class name being tested (for test files).
    /// </summary>
    public string? RealClassName { get; set; }
}

public class DeploymentResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string CodebasePath { get; set; } = "";
    public DateTime StartedAt { get; set; }
    public DateTime CompletedAt { get; set; }
    public List<FileCopyResult> CopiedFiles { get; set; } = new();
    public List<CsprojUpdateResult> UpdatedProjects { get; set; } = new();
    public List<BuildResult> BuildResults { get; set; } = new();

    public int TotalFilesCopied => CopiedFiles.Count(f => f.Success);
    public int NewFilesCreated => CopiedFiles.Count(f => f.Success && f.IsNewFile);
    public int FilesModified => CopiedFiles.Count(f => f.Success && !f.IsNewFile);
}

public class RollbackResult
{
    public bool Success { get; set; }
    public string DeploymentPath { get; set; } = "";
    public DateTime StartedAt { get; set; }
    public DateTime CompletedAt { get; set; }
    public List<string> DeletedFiles { get; set; } = new();
    public List<string> DeletedDirectories { get; set; } = new();
    public List<string> RevertedProjects { get; set; } = new();
    public List<string> Errors { get; set; } = new();
}

public class FileMapping
{
    public string GeneratedPath { get; set; } = "";
    public string Content { get; set; } = "";
    public string? TargetPath { get; set; }
    public string? ProjectName { get; set; }
    public string? ProjectPath { get; set; }
    
    /// <summary>
    /// If true, this is a modification to an existing file.
    /// The Content should be merged into the existing file rather than replacing it entirely.
    /// </summary>
    public bool IsModification { get; set; }
    
    /// <summary>
    /// The specific method name to replace (for targeted modifications)
    /// </summary>
    public string? TargetMethodName { get; set; }
    
    /// <summary>
    /// The specific class name being modified
    /// </summary>
    public string? TargetClassName { get; set; }

    /// <summary>
    /// If true, this is a test file that needs namespace conversion.
    /// Dummy namespaces will be replaced with real class namespaces.
    /// </summary>
    public bool IsTestFile { get; set; }

    /// <summary>
    /// The real namespace of the class being tested (for test files).
    /// Used to replace dummy namespace references.
    /// </summary>
    public string? RealClassNamespace { get; set; }

    /// <summary>
    /// The real class name being tested (for test files).
    /// </summary>
    public string? RealClassName { get; set; }
}

public class FileCopyResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string SourcePath { get; set; } = "";
    public string TargetPath { get; set; } = "";
    public bool IsNewFile { get; set; }
}

public class CsprojUpdateResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? Message { get; set; }
    public string ProjectPath { get; set; } = "";
    public List<string> AddedFiles { get; set; } = new();
}

public class BuildResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? Output { get; set; }
    public string? TargetPath { get; set; }
}

#endregion
