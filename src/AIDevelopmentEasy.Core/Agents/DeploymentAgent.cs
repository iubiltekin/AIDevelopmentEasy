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
        var codebasePath = codebaseAnalysis.CodebasePath;
        _logger?.LogInformation("[Deployment] Starting deployment to: {Path}", codebasePath);
        _logger?.LogInformation("[Deployment] Files to deploy: {Count}", generatedFiles.Count);
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
            var fileMappings = AnalyzeFileMappings(codebaseAnalysis, generatedFiles);
            _logger?.LogInformation("[Deployment] Mapped {Count} files to projects", fileMappings.Count);

            // Step 2: Copy files to their target locations
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
                    // Ultimate fallback: use the generated path structure
                    var pathParts = generatedPath.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
                    var startIndex = pathParts.Length > 2 && !pathParts[0].Contains('.') ? 1 : 0;
                    mapping.TargetPath = Path.Combine(codebasePath,
                        string.Join(Path.DirectorySeparatorChar.ToString(), pathParts.Skip(startIndex)));

                    _logger?.LogWarning("[Deployment] No mapping found for namespace '{NS}', using fallback: {Target}",
                        fileNamespace, mapping.TargetPath);
                }
            }

            mappings.Add(mapping);
        }

        return mappings;
    }

    /// <summary>
    /// Copy files to their target locations in the codebase
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

                // Check if file exists (modification vs new file)
                result.IsNewFile = !File.Exists(mapping.TargetPath);

                // Write the file
                await File.WriteAllTextAsync(mapping.TargetPath, mapping.Content, cancellationToken);
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
            }
        }

        // Add dependent projects
        foreach (var path in dependentProjects)
        {
            if (!projectsToBuild.Contains(path, StringComparer.OrdinalIgnoreCase))
            {
                projectsToBuild.Add(path);
            }
        }

        _logger?.LogInformation("[Deployment] Building {Count} projects (modified + dependents)",
            projectsToBuild.Count);

        // Build each project
        foreach (var projectPath in projectsToBuild)
        {
            _logger?.LogInformation("[Deployment] Building project: {Project}",
                Path.GetFileName(projectPath));

            var buildResult = await BuildWithMSBuildAsync(msbuildPath, projectPath, cancellationToken);
            results.Add(buildResult);

            // If build fails, log but continue with other projects
            if (!buildResult.Success)
            {
                _logger?.LogWarning("[Deployment] Build failed for {Project}: {Error}",
                    Path.GetFileName(projectPath), buildResult.Error);
            }
        }

        return results;
    }

    private async Task<BuildResult> BuildWithMSBuildAsync(
        string msbuildPath,
        string targetPath,
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
                Arguments = $"\"{targetPath}\" /t:Build /p:Configuration=Debug /nologo /v:minimal /m",
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
}

#region Result Models

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

public class FileMapping
{
    public string GeneratedPath { get; set; } = "";
    public string Content { get; set; } = "";
    public string? TargetPath { get; set; }
    public string? ProjectName { get; set; }
    public string? ProjectPath { get; set; }
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
