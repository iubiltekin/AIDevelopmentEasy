using System.Text.Json;
using AIDevelopmentEasy.Api.Models;
using AIDevelopmentEasy.Api.Repositories.Interfaces;

namespace AIDevelopmentEasy.Api.Repositories.FileSystem;

/// <summary>
/// File system based implementation of IStoryRepository.
/// Stories are stored as JSON files with unique IDs (STR-YYYYMMDD-XXXX format).
/// </summary>
public class FileSystemStoryRepository : IStoryRepository
{
    private readonly string _storiesPath;
    private readonly IApprovalRepository _approvalRepository;
    private readonly ITaskRepository _taskRepository;
    private readonly ILogger<FileSystemStoryRepository> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    // Legacy extensions for backward compatibility
    private static readonly string[] LegacyExtensions = { ".md", ".txt" };

    public FileSystemStoryRepository(
        string storiesPath,
        IApprovalRepository approvalRepository,
        ITaskRepository taskRepository,
        ILogger<FileSystemStoryRepository> logger)
    {
        _storiesPath = storiesPath;
        _approvalRepository = approvalRepository;
        _taskRepository = taskRepository;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        if (!Directory.Exists(_storiesPath))
        {
            Directory.CreateDirectory(_storiesPath);
        }
    }

    public async Task<IEnumerable<StoryDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var stories = new List<StoryDto>();

        if (!Directory.Exists(_storiesPath))
            return stories;

        // Get new format stories (STR-*.json)
        var jsonFiles = Directory.GetFiles(_storiesPath, "STR-*.json")
            .OrderByDescending(f => File.GetCreationTimeUtc(f));

        foreach (var file in jsonFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var story = await LoadStoryFromJsonAsync(file, cancellationToken);
            if (story != null)
            {
                stories.Add(story);
            }
        }

        // Also load legacy format stories (.md, .txt files that don't start with STR-)
        var legacyFiles = Directory.GetFiles(_storiesPath)
            .Where(f => LegacyExtensions.Contains(Path.GetExtension(f).ToLower()))
            .Where(f => !Path.GetFileName(f).StartsWith("STR-"))
            .OrderByDescending(f => File.GetCreationTimeUtc(f));

        foreach (var file in legacyFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var story = await LoadLegacyStoryAsync(file, cancellationToken);
            if (story != null)
            {
                stories.Add(story);
            }
        }

        return stories.OrderByDescending(s => s.CreatedAt);
    }

    public async Task<StoryDto?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        // Try new format first
        var jsonPath = Path.Combine(_storiesPath, $"{id}.json");
        if (File.Exists(jsonPath))
        {
            return await LoadStoryFromJsonAsync(jsonPath, cancellationToken);
        }

        // Try legacy format
        var legacyPath = FindLegacyStoryFile(id);
        if (legacyPath != null)
        {
            return await LoadLegacyStoryAsync(legacyPath, cancellationToken);
        }

        return null;
    }

    public async Task<string?> GetContentAsync(string id, CancellationToken cancellationToken = default)
    {
        var story = await GetByIdAsync(id, cancellationToken);
        return story?.Content;
    }

    public async Task<StoryDto> CreateAsync(string name, string content, StoryType type, string? codebaseId = null, string? requirementId = null, CancellationToken cancellationToken = default)
    {
        // Generate unique ID
        var id = GenerateId();

        var storyData = new StoryData
        {
            Id = id,
            Name = name,
            Content = content,
            Type = type,
            CodebaseId = codebaseId,
            RequirementId = requirementId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await SaveStoryDataAsync(storyData, cancellationToken);

        _logger.LogInformation("Created story: {Id} - {Name} (codebaseId: {CodebaseId}, requirementId: {RequirementId})", 
            id, name, codebaseId ?? "none", requirementId ?? "none");

        return new StoryDto
        {
            Id = id,
            Name = name,
            Content = content,
            Type = type,
            Status = StoryStatus.NotStarted,
            CodebaseId = codebaseId,
            RequirementId = requirementId,
            CreatedAt = storyData.CreatedAt
        };
    }

    public async Task UpdateStatusAsync(string id, StoryStatus status, CancellationToken cancellationToken = default)
    {
        // Status is managed through approval repository
        switch (status)
        {
            case StoryStatus.InProgress:
                await _approvalRepository.MarkInProgressAsync(id, cancellationToken);
                break;
            case StoryStatus.Approved:
                await _approvalRepository.ResetInProgressAsync(id, cancellationToken);
                await _approvalRepository.ApprovePlanAsync(id, cancellationToken);
                break;
            case StoryStatus.Completed:
                await _approvalRepository.ResetInProgressAsync(id, cancellationToken);
                await _approvalRepository.MarkCompletedAsync(id, cancellationToken);
                break;
            case StoryStatus.Failed:
                await _approvalRepository.ResetInProgressAsync(id, cancellationToken);
                await SaveFailedStatusAsync(id, cancellationToken);
                break;
            case StoryStatus.NotStarted:
                await _approvalRepository.ResetApprovalAsync(id, cancellationToken);
                await _approvalRepository.ResetCompletionAsync(id, cancellationToken);
                await _approvalRepository.ResetInProgressAsync(id, cancellationToken);
                await ClearFailedStatusAsync(id, cancellationToken);
                break;
        }
    }

    public async Task<bool> UpdateContentAsync(string id, string content, CancellationToken cancellationToken = default)
    {
        // Try new format first
        var jsonPath = Path.Combine(_storiesPath, $"{id}.json");
        if (File.Exists(jsonPath))
        {
            var storyData = await LoadStoryDataAsync(jsonPath, cancellationToken);
            if (storyData != null)
            {
                storyData.Content = content;
                storyData.UpdatedAt = DateTime.UtcNow;
                await SaveStoryDataAsync(storyData, cancellationToken);
                _logger.LogInformation("Updated story content: {Id}", id);
                return true;
            }
        }

        // Try legacy format
        var legacyPath = FindLegacyStoryFile(id);
        if (legacyPath != null)
        {
            await File.WriteAllTextAsync(legacyPath, content, cancellationToken);
            _logger.LogInformation("Updated legacy story content: {Id}", id);
            return true;
        }

        return false;
    }

    public async Task<bool> UpdateNameAsync(string id, string name, CancellationToken cancellationToken = default)
    {
        var jsonPath = Path.Combine(_storiesPath, $"{id}.json");
        if (File.Exists(jsonPath))
        {
            var storyData = await LoadStoryDataAsync(jsonPath, cancellationToken);
            if (storyData != null)
            {
                storyData.Name = name;
                storyData.UpdatedAt = DateTime.UtcNow;
                await SaveStoryDataAsync(storyData, cancellationToken);
                _logger.LogInformation("Updated story name: {Id} → {Name}", id, name);
                return true;
            }
        }

        return false;
    }

    public Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var deleted = false;

        // Delete new format
        var jsonPath = Path.Combine(_storiesPath, $"{id}.json");
        if (File.Exists(jsonPath))
        {
            File.Delete(jsonPath);
            deleted = true;
        }

        // Delete legacy format
        var legacyPath = FindLegacyStoryFile(id);
        if (legacyPath != null)
        {
            File.Delete(legacyPath);
            deleted = true;
        }

        // Delete associated folder (tasks, approvals, etc.)
        var folderPath = Path.Combine(_storiesPath, id);
        if (Directory.Exists(folderPath))
        {
            Directory.Delete(folderPath, recursive: true);
        }

        if (deleted)
        {
            _logger.LogInformation("Deleted story: {Id}", id);
        }

        return Task.FromResult(deleted);
    }

    public Task<bool> ExistsAsync(string id, CancellationToken cancellationToken = default)
    {
        var jsonPath = Path.Combine(_storiesPath, $"{id}.json");
        if (File.Exists(jsonPath))
            return Task.FromResult(true);

        return Task.FromResult(FindLegacyStoryFile(id) != null);
    }

    public async Task<StoryDto> CreateAsync(CreateStoryRequest request, CancellationToken cancellationToken = default)
    {
        // Generate unique ID
        var id = GenerateId();

        var storyData = new StoryData
        {
            Id = id,
            Name = request.Name,
            Content = request.Content,
            Type = request.Type,
            CodebaseId = request.CodebaseId,
            RequirementId = request.RequirementId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            // Target Info
            TargetProject = request.TargetProject,
            TargetFile = request.TargetFile,
            TargetClass = request.TargetClass,
            TargetMethod = request.TargetMethod,
            ChangeType = request.ChangeType,
            // Test Target Info
            TargetTestProject = request.TargetTestProject,
            TargetTestFile = request.TargetTestFile,
            TargetTestClass = request.TargetTestClass
        };

        await SaveStoryDataAsync(storyData, cancellationToken);

        _logger.LogInformation("Created story: {Id} - {Name} (target: {Project}/{File}, test: {TestFile})", 
            id, request.Name, request.TargetProject ?? "none", request.TargetFile ?? "none", request.TargetTestFile ?? "none");

        return new StoryDto
        {
            Id = id,
            Name = request.Name,
            Content = request.Content,
            Type = request.Type,
            Status = StoryStatus.NotStarted,
            CodebaseId = request.CodebaseId,
            RequirementId = request.RequirementId,
            CreatedAt = storyData.CreatedAt,
            TargetProject = request.TargetProject,
            TargetFile = request.TargetFile,
            TargetClass = request.TargetClass,
            TargetMethod = request.TargetMethod,
            ChangeType = request.ChangeType,
            TargetTestProject = request.TargetTestProject,
            TargetTestFile = request.TargetTestFile,
            TargetTestClass = request.TargetTestClass
        };
    }

    public async Task<bool> UpdateTargetAsync(string id, UpdateStoryTargetRequest request, CancellationToken cancellationToken = default)
    {
        var jsonPath = Path.Combine(_storiesPath, $"{id}.json");
        if (!File.Exists(jsonPath))
        {
            _logger.LogWarning("Story not found for target update: {Id}", id);
            return false;
        }

        var storyData = await LoadStoryDataAsync(jsonPath, cancellationToken);
        if (storyData == null)
            return false;

        // Update target fields
        storyData.TargetProject = request.TargetProject;
        storyData.TargetFile = request.TargetFile;
        storyData.TargetClass = request.TargetClass;
        storyData.TargetMethod = request.TargetMethod;
        storyData.ChangeType = request.ChangeType;
        // Update test target fields
        storyData.TargetTestProject = request.TargetTestProject;
        storyData.TargetTestFile = request.TargetTestFile;
        storyData.TargetTestClass = request.TargetTestClass;
        storyData.UpdatedAt = DateTime.UtcNow;

        await SaveStoryDataAsync(storyData, cancellationToken);

        _logger.LogInformation("Updated story target: {Id} → {Project}/{File}/{Class}.{Method} ({ChangeType}), Test: {TestFile}", 
            id, 
            request.TargetProject ?? "any", 
            request.TargetFile ?? "any", 
            request.TargetClass ?? "any",
            request.TargetMethod ?? "any",
            request.ChangeType,
            request.TargetTestFile ?? "none");

        return true;
    }

    #region Private Methods

    private string GenerateId()
    {
        // Format: STR-YYYYMMDD-XXXX (similar to REQ-YYYYMMDD-XXXX)
        var date = DateTime.UtcNow.ToString("yyyyMMdd");
        var random = Guid.NewGuid().ToString("N")[..4].ToUpper();
        return $"STR-{date}-{random}";
    }

    private async Task SaveStoryDataAsync(StoryData data, CancellationToken cancellationToken)
    {
        var filePath = Path.Combine(_storiesPath, $"{data.Id}.json");
        var json = JsonSerializer.Serialize(data, _jsonOptions);
        await File.WriteAllTextAsync(filePath, json, cancellationToken);
    }

    private async Task<StoryData?> LoadStoryDataAsync(string filePath, CancellationToken cancellationToken)
    {
        try
        {
            var json = await File.ReadAllTextAsync(filePath, cancellationToken);
            return JsonSerializer.Deserialize<StoryData>(json, _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load story data from: {Path}", filePath);
            return null;
        }
    }

    private async Task<StoryDto?> LoadStoryFromJsonAsync(string filePath, CancellationToken cancellationToken)
    {
        try
        {
            var data = await LoadStoryDataAsync(filePath, cancellationToken);
            if (data == null)
                return null;

            var status = await GetStoryStatusAsync(data.Id, cancellationToken);
            var tasks = await _taskRepository.GetByStoryAsync(data.Id, cancellationToken);

            return new StoryDto
            {
                Id = data.Id,
                Name = data.Name,
                Content = data.Content,
                Type = data.Type,
                Status = status,
                CodebaseId = data.CodebaseId,
                RequirementId = data.RequirementId,
                CreatedAt = data.CreatedAt,
                LastProcessedAt = data.UpdatedAt,
                Tasks = tasks.ToList(),
                // Target Info
                TargetProject = data.TargetProject,
                TargetFile = data.TargetFile,
                TargetClass = data.TargetClass,
                TargetMethod = data.TargetMethod,
                ChangeType = data.ChangeType,
                // Test Target Info
                TargetTestProject = data.TargetTestProject,
                TargetTestFile = data.TargetTestFile,
                TargetTestClass = data.TargetTestClass
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading story from: {Path}", filePath);
            return null;
        }
    }

    private async Task<StoryDto?> LoadLegacyStoryAsync(string filePath, CancellationToken cancellationToken)
    {
        try
        {
            var fileName = Path.GetFileName(filePath);
            var id = Path.GetFileNameWithoutExtension(fileName);
            var content = await File.ReadAllTextAsync(filePath, cancellationToken);

            var status = await GetStoryStatusAsync(id, cancellationToken);
            var tasks = await _taskRepository.GetByStoryAsync(id, cancellationToken);

            // Load legacy metadata if exists
            var metadata = await LoadLegacyMetadataAsync(id, cancellationToken);

            return new StoryDto
            {
                Id = id,
                Name = id, // For legacy stories, name = id
                Content = content,
                Type = StoryType.Single,
                Status = status,
                CodebaseId = metadata?.CodebaseId,
                RequirementId = metadata?.RequirementId,
                CreatedAt = File.GetCreationTimeUtc(filePath),
                LastProcessedAt = File.GetLastWriteTimeUtc(filePath),
                Tasks = tasks.ToList()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading legacy story from: {Path}", filePath);
            return null;
        }
    }

    private async Task<StoryStatus> GetStoryStatusAsync(string id, CancellationToken cancellationToken)
    {
        // Check if failed first
        var failedFile = Path.Combine(_storiesPath, id, ".failed");
        if (File.Exists(failedFile))
            return StoryStatus.Failed;

        var status = await _approvalRepository.GetStatusAsync(id, cancellationToken);
        var hasTasks = await _taskRepository.HasTasksAsync(id, cancellationToken);

        // Adjust status based on tasks
        if (status == StoryStatus.NotStarted && hasTasks)
            return StoryStatus.Planned;

        return status;
    }

    private string? FindLegacyStoryFile(string id)
    {
        foreach (var ext in LegacyExtensions)
        {
            var path = Path.Combine(_storiesPath, id + ext);
            if (File.Exists(path))
                return path;
        }
        return null;
    }

    private async Task<LegacyMetadata?> LoadLegacyMetadataAsync(string storyId, CancellationToken cancellationToken)
    {
        var metadataPath = Path.Combine(_storiesPath, storyId, "metadata.json");
        
        if (!File.Exists(metadataPath))
            return null;

        try
        {
            var json = await File.ReadAllTextAsync(metadataPath, cancellationToken);
            return JsonSerializer.Deserialize<LegacyMetadata>(json, _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load legacy metadata for story: {Id}", storyId);
            return null;
        }
    }

    private async Task SaveFailedStatusAsync(string id, CancellationToken cancellationToken)
    {
        var folderPath = Path.Combine(_storiesPath, id);
        if (!Directory.Exists(folderPath))
            Directory.CreateDirectory(folderPath);

        var failedFile = Path.Combine(folderPath, ".failed");
        await File.WriteAllTextAsync(failedFile, DateTime.UtcNow.ToString("O"), cancellationToken);
    }

    private Task ClearFailedStatusAsync(string id, CancellationToken cancellationToken)
    {
        var failedFile = Path.Combine(_storiesPath, id, ".failed");
        if (File.Exists(failedFile))
        {
            File.Delete(failedFile);
        }
        return Task.CompletedTask;
    }

    #endregion

    #region Data Classes

    /// <summary>
    /// Story data stored in JSON file
    /// </summary>
    private class StoryData
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public StoryType Type { get; set; }
        public string? CodebaseId { get; set; }
        public string? RequirementId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // Target Info (Optional)
        public string? TargetProject { get; set; }
        public string? TargetFile { get; set; }
        public string? TargetClass { get; set; }
        public string? TargetMethod { get; set; }
        public ChangeType ChangeType { get; set; } = ChangeType.Create;

        // Test Target Info (Optional)
        public string? TargetTestProject { get; set; }
        public string? TargetTestFile { get; set; }
        public string? TargetTestClass { get; set; }
    }

    /// <summary>
    /// Legacy metadata format
    /// </summary>
    private class LegacyMetadata
    {
        public string? CodebaseId { get; set; }
        public string? RequirementId { get; set; }
    }

    #endregion
}
