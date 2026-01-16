using System.Text.Json;
using AIDevelopmentEasy.Api.Models;
using AIDevelopmentEasy.Api.Repositories.Interfaces;
using AIDevelopmentEasy.Core.Models;

// Alias to avoid ambiguity
using CodebaseStatus = AIDevelopmentEasy.Api.Models.CodebaseStatus;

namespace AIDevelopmentEasy.Api.Repositories.FileSystem;

/// <summary>
/// File system based implementation of ICodebaseRepository.
/// Codebases are stored in codebases/{name}/ directories with:
/// - _metadata.json: Basic info (name, path, status, dates)
/// - _analysis.json: Full analysis results
/// </summary>
public class FileSystemCodebaseRepository : ICodebaseRepository
{
    private readonly string _codebasesPath;
    private readonly ILogger<FileSystemCodebaseRepository> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    private const string MetadataFileName = "_metadata.json";
    private const string AnalysisFileName = "_analysis.json";

    public FileSystemCodebaseRepository(string codebasesPath, ILogger<FileSystemCodebaseRepository> logger)
    {
        _codebasesPath = codebasesPath;
        _logger = logger;
        // Don't use naming policy - let JsonPropertyName attributes control serialization
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        if (!Directory.Exists(_codebasesPath))
        {
            Directory.CreateDirectory(_codebasesPath);
        }
    }

    public async Task<IEnumerable<CodebaseDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var codebases = new List<CodebaseDto>();

        if (!Directory.Exists(_codebasesPath))
            return codebases;

        var directories = Directory.GetDirectories(_codebasesPath);

        foreach (var dir in directories)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var metadataPath = Path.Combine(dir, MetadataFileName);
            if (File.Exists(metadataPath))
            {
                try
                {
                    var codebase = await LoadCodebaseDtoAsync(dir, cancellationToken);
                    if (codebase != null)
                    {
                        codebases.Add(codebase);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error loading codebase from: {Dir}", dir);
                }
            }
        }

        return codebases.OrderBy(c => c.Name);
    }

    public async Task<CodebaseDto?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var codebaseDir = GetCodebaseDirectory(id);
        if (!Directory.Exists(codebaseDir))
            return null;

        return await LoadCodebaseDtoAsync(codebaseDir, cancellationToken);
    }

    public async Task<CodebaseAnalysis?> GetAnalysisAsync(string id, CancellationToken cancellationToken = default)
    {
        var codebaseDir = GetCodebaseDirectory(id);
        var analysisPath = Path.Combine(codebaseDir, AnalysisFileName);
        if (!File.Exists(analysisPath))
            return null;

        try
        {
            var json = await File.ReadAllTextAsync(analysisPath, cancellationToken);
            var analysis = JsonSerializer.Deserialize<CodebaseAnalysis>(json, _jsonOptions);
            
            if (analysis == null)
                return null;

            // IMPORTANT: Ensure CodebasePath is set - fall back to metadata if empty
            if (string.IsNullOrEmpty(analysis.CodebasePath))
            {
                var metadataPath = Path.Combine(codebaseDir, MetadataFileName);
                if (File.Exists(metadataPath))
                {
                    var metadataJson = await File.ReadAllTextAsync(metadataPath, cancellationToken);
                    var metadata = JsonSerializer.Deserialize<CodebaseMetadata>(metadataJson, _jsonOptions);
                    if (metadata != null && !string.IsNullOrEmpty(metadata.Path))
                    {
                        analysis.CodebasePath = metadata.Path;
                        _logger.LogWarning("CodebasePath was empty in analysis, restored from metadata: {Path}", metadata.Path);
                    }
                }
            }

            _logger.LogInformation("[GetAnalysis] Loaded analysis for {Id}, CodebasePath: {Path}", 
                id, analysis.CodebasePath);

            return analysis;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading analysis for codebase: {Id}", id);
            return null;
        }
    }

    public async Task<CodebaseDto> CreateAsync(string name, string path, CancellationToken cancellationToken = default)
    {
        var id = SanitizeId(name);
        var codebaseDir = GetCodebaseDirectory(id);

        // Ensure unique ID
        var counter = 1;
        while (Directory.Exists(codebaseDir))
        {
            id = $"{SanitizeId(name)}-{counter}";
            codebaseDir = GetCodebaseDirectory(id);
            counter++;
        }

        Directory.CreateDirectory(codebaseDir);

        var metadata = new CodebaseMetadata
        {
            Id = id,
            Name = name,
            Path = path,
            Status = CodebaseStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        var metadataPath = Path.Combine(codebaseDir, MetadataFileName);
        var json = JsonSerializer.Serialize(metadata, _jsonOptions);
        await File.WriteAllTextAsync(metadataPath, json, cancellationToken);

        _logger.LogInformation("Created codebase: {Id} at {Path}", id, path);

        return new CodebaseDto
        {
            Id = metadata.Id,
            Name = metadata.Name,
            Path = metadata.Path,
            Status = metadata.Status,
            CreatedAt = metadata.CreatedAt
        };
    }

    public async Task SaveAnalysisAsync(string id, CodebaseAnalysis analysis, CancellationToken cancellationToken = default)
    {
        var codebaseDir = GetCodebaseDirectory(id);
        if (!Directory.Exists(codebaseDir))
        {
            throw new DirectoryNotFoundException($"Codebase not found: {id}");
        }

        // Ensure CodebasePath is preserved from metadata if not set
        if (string.IsNullOrEmpty(analysis.CodebasePath))
        {
            var metadataPath = Path.Combine(codebaseDir, MetadataFileName);
            if (File.Exists(metadataPath))
            {
                var metadataJson = await File.ReadAllTextAsync(metadataPath, cancellationToken);
                var existingMetadata = JsonSerializer.Deserialize<CodebaseMetadata>(metadataJson, _jsonOptions);
                if (existingMetadata != null)
                {
                    analysis.CodebasePath = existingMetadata.Path;
                }
            }
        }

        // Save analysis
        var analysisPath = Path.Combine(codebaseDir, AnalysisFileName);
        var analysisJson = JsonSerializer.Serialize(analysis, _jsonOptions);
        await File.WriteAllTextAsync(analysisPath, analysisJson, cancellationToken);

        _logger.LogInformation("Saved analysis for codebase: {Id}, CodebasePath: {Path}", id, analysis.CodebasePath);

        // Update metadata
        var metadataPath2 = Path.Combine(codebaseDir, MetadataFileName);
        if (File.Exists(metadataPath2))
        {
            var metadataJson = await File.ReadAllTextAsync(metadataPath2, cancellationToken);
            var metadata = JsonSerializer.Deserialize<CodebaseMetadata>(metadataJson, _jsonOptions)
                ?? new CodebaseMetadata { Id = id };

            metadata.Status = CodebaseStatus.Ready;
            metadata.AnalyzedAt = analysis.AnalyzedAt;

            var updatedJson = JsonSerializer.Serialize(metadata, _jsonOptions);
            await File.WriteAllTextAsync(metadataPath2, updatedJson, cancellationToken);
        }
    }

    public async Task UpdateStatusAsync(string id, CodebaseStatus status, CancellationToken cancellationToken = default)
    {
        var metadataPath = Path.Combine(GetCodebaseDirectory(id), MetadataFileName);
        if (!File.Exists(metadataPath))
            return;

        var json = await File.ReadAllTextAsync(metadataPath, cancellationToken);
        var metadata = JsonSerializer.Deserialize<CodebaseMetadata>(json, _jsonOptions);

        if (metadata != null)
        {
            metadata.Status = status;
            var updatedJson = JsonSerializer.Serialize(metadata, _jsonOptions);
            await File.WriteAllTextAsync(metadataPath, updatedJson, cancellationToken);

            _logger.LogInformation("Updated codebase {Id} status to: {Status}", id, status);
        }
    }

    public Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var codebaseDir = GetCodebaseDirectory(id);
        if (!Directory.Exists(codebaseDir))
            return Task.FromResult(false);

        Directory.Delete(codebaseDir, recursive: true);
        _logger.LogInformation("Deleted codebase: {Id}", id);

        return Task.FromResult(true);
    }

    public Task<bool> ExistsAsync(string id, CancellationToken cancellationToken = default)
    {
        var codebaseDir = GetCodebaseDirectory(id);
        return Task.FromResult(Directory.Exists(codebaseDir) && 
            File.Exists(Path.Combine(codebaseDir, MetadataFileName)));
    }

    private string GetCodebaseDirectory(string id)
    {
        return Path.Combine(_codebasesPath, id);
    }

    private async Task<CodebaseDto?> LoadCodebaseDtoAsync(string codebaseDir, CancellationToken cancellationToken)
    {
        var metadataPath = Path.Combine(codebaseDir, MetadataFileName);
        if (!File.Exists(metadataPath))
            return null;

        try
        {
            var json = await File.ReadAllTextAsync(metadataPath, cancellationToken);
            var metadata = JsonSerializer.Deserialize<CodebaseMetadata>(json, _jsonOptions);

            if (metadata == null)
                return null;

            var dto = new CodebaseDto
            {
                Id = metadata.Id,
                Name = metadata.Name,
                Path = metadata.Path,
                Status = metadata.Status,
                AnalyzedAt = metadata.AnalyzedAt,
                CreatedAt = metadata.CreatedAt
            };

            // Load summary from analysis if available
            var analysisPath = Path.Combine(codebaseDir, AnalysisFileName);
            if (File.Exists(analysisPath))
            {
                var analysisJson = await File.ReadAllTextAsync(analysisPath, cancellationToken);
                var analysis = JsonSerializer.Deserialize<CodebaseAnalysis>(analysisJson, _jsonOptions);

                if (analysis?.Summary != null)
                {
                    dto.Summary = new CodebaseSummaryDto
                    {
                        TotalSolutions = analysis.Summary.TotalSolutions,
                        TotalProjects = analysis.Summary.TotalProjects,
                        TotalClasses = analysis.Summary.TotalClasses,
                        TotalInterfaces = analysis.Summary.TotalInterfaces,
                        PrimaryFramework = analysis.Summary.PrimaryFramework,
                        DetectedPatterns = analysis.Summary.DetectedPatterns,
                        KeyNamespaces = analysis.Summary.KeyNamespaces
                    };
                }
            }

            return dto;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading codebase metadata from: {Path}", metadataPath);
            return null;
        }
    }

    private static string SanitizeId(string name)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(name.Where(c => !invalidChars.Contains(c)).ToArray());
        return sanitized.Replace(' ', '-').ToLower();
    }

    /// <summary>
    /// Internal metadata structure
    /// </summary>
    private class CodebaseMetadata
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public CodebaseStatus Status { get; set; }
        public DateTime? AnalyzedAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
