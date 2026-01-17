using System.Text.Json;
using AIDevelopmentEasy.Api.Models;
using AIDevelopmentEasy.Api.Repositories.Interfaces;

namespace AIDevelopmentEasy.Api.Repositories.FileSystem;

/// <summary>
/// File system based implementation of IStoryRepository.
/// Stories are stored as .md, .txt, or .json files in the stories directory.
/// </summary>
public class FileSystemStoryRepository : IStoryRepository
{
    private readonly string _storiesPath;
    private readonly IApprovalRepository _approvalRepository;
    private readonly ITaskRepository _taskRepository;
    private readonly ILogger<FileSystemStoryRepository> _logger;

    private static readonly string[] SupportedExtensions = { ".json", ".md", ".txt" };

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

        var files = Directory.GetFiles(_storiesPath)
            .Where(f => SupportedExtensions.Contains(Path.GetExtension(f).ToLower()))
            .OrderBy(f => f);

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var story = await CreateStoryDtoFromFile(file, cancellationToken);
            if (story != null)
            {
                stories.Add(story);
            }
        }

        return stories;
    }

    public async Task<StoryDto?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var filePath = FindStoryFile(id);
        if (filePath == null)
            return null;

        return await CreateStoryDtoFromFile(filePath, cancellationToken);
    }

    public async Task<string?> GetContentAsync(string id, CancellationToken cancellationToken = default)
    {
        var filePath = FindStoryFile(id);
        if (filePath == null)
            return null;

        return await File.ReadAllTextAsync(filePath, cancellationToken);
    }

    public async Task<StoryDto> CreateAsync(string name, string content, StoryType type, string? codebaseId = null, CancellationToken cancellationToken = default)
    {
        // All stories are now single-project type (stored as .md)
        var extension = ".md";
        var fileName = SanitizeFileName(name) + extension;
        var filePath = Path.Combine(_storiesPath, fileName);

        // Ensure unique filename
        var counter = 1;
        while (File.Exists(filePath))
        {
            fileName = $"{SanitizeFileName(name)}_{counter}{extension}";
            filePath = Path.Combine(_storiesPath, fileName);
            counter++;
        }

        await File.WriteAllTextAsync(filePath, content, cancellationToken);

        var id = Path.GetFileNameWithoutExtension(fileName);

        // Save codebaseId in metadata file if provided
        if (!string.IsNullOrEmpty(codebaseId))
        {
            await SaveCodebaseIdAsync(id, codebaseId, cancellationToken);
        }

        _logger.LogInformation("Created story file: {FilePath} (codebaseId: {CodebaseId})", filePath, codebaseId ?? "none");

        return new StoryDto
        {
            Id = id,
            Name = id,
            Content = content,
            Type = type,
            Status = StoryStatus.NotStarted,
            CodebaseId = codebaseId,
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Save codebase ID for a story in a metadata file
    /// </summary>
    private async Task SaveCodebaseIdAsync(string storyId, string codebaseId, CancellationToken cancellationToken)
    {
        var metadataDir = Path.Combine(_storiesPath, storyId);
        Directory.CreateDirectory(metadataDir);
        
        var metadataPath = Path.Combine(metadataDir, "metadata.json");
        var metadata = new StoryMetadata { CodebaseId = codebaseId };
        
        var json = JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(metadataPath, json, cancellationToken);
    }

    /// <summary>
    /// Load codebase ID for a story from metadata file
    /// </summary>
    private async Task<string?> LoadCodebaseIdAsync(string storyId, CancellationToken cancellationToken)
    {
        var metadataPath = Path.Combine(_storiesPath, storyId, "metadata.json");
        
        if (!File.Exists(metadataPath))
            return null;

        try
        {
            var json = await File.ReadAllTextAsync(metadataPath, cancellationToken);
            var metadata = JsonSerializer.Deserialize<StoryMetadata>(json);
            return metadata?.CodebaseId;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load metadata for story: {Id}", storyId);
            return null;
        }
    }

    /// <summary>
    /// Metadata stored alongside story
    /// </summary>
    private class StoryMetadata
    {
        public string? CodebaseId { get; set; }
    }

    public async Task UpdateStatusAsync(string id, StoryStatus status, CancellationToken cancellationToken = default)
    {
        // Status is managed through approval repository, this is for future database implementations
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
                // Reset in-progress flag so it shows as failed, not running
                await _approvalRepository.ResetInProgressAsync(id, cancellationToken);
                // Save failed status to metadata file
                await SaveFailedStatusAsync(id, cancellationToken);
                break;
            case StoryStatus.NotStarted:
                await _approvalRepository.ResetApprovalAsync(id, cancellationToken);
                await _approvalRepository.ResetCompletionAsync(id, cancellationToken);
                await _approvalRepository.ResetInProgressAsync(id, cancellationToken);
                // Clear failed status if exists
                await ClearFailedStatusAsync(id, cancellationToken);
                break;
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

    public async Task<bool> UpdateContentAsync(string id, string content, CancellationToken cancellationToken = default)
    {
        var filePath = FindStoryFile(id);
        if (filePath == null)
            return false;

        await File.WriteAllTextAsync(filePath, content, cancellationToken);
        _logger.LogInformation("Updated story content: {Id}", id);
        return true;
    }

    public Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var filePath = FindStoryFile(id);
        if (filePath == null)
            return Task.FromResult(false);

        File.Delete(filePath);

        // Also delete associated folder
        var folderPath = Path.Combine(_storiesPath, id);
        if (Directory.Exists(folderPath))
        {
            Directory.Delete(folderPath, recursive: true);
        }

        _logger.LogInformation("Deleted story: {Id}", id);
        return Task.FromResult(true);
    }

    public Task<bool> ExistsAsync(string id, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(FindStoryFile(id) != null);
    }

    private string? FindStoryFile(string id)
    {
        foreach (var ext in SupportedExtensions)
        {
            var path = Path.Combine(_storiesPath, id + ext);
            if (File.Exists(path))
                return path;
        }
        return null;
    }

    private async Task<StoryDto?> CreateStoryDtoFromFile(string filePath, CancellationToken cancellationToken)
    {
        try
        {
            var fileName = Path.GetFileName(filePath);
            var id = Path.GetFileNameWithoutExtension(fileName);
            var extension = Path.GetExtension(filePath).ToLower();
            var content = await File.ReadAllTextAsync(filePath, cancellationToken);

            // All stories are now single-project type
            var type = StoryType.Single;
            var status = await _approvalRepository.GetStatusAsync(id, cancellationToken);
            var hasTasks = await _taskRepository.HasTasksAsync(id, cancellationToken);

            // Check if failed (overrides other statuses)
            var failedFile = Path.Combine(_storiesPath, id, ".failed");
            if (File.Exists(failedFile))
            {
                status = StoryStatus.Failed;
            }
            // Adjust status based on tasks
            else if (status == StoryStatus.NotStarted && hasTasks)
            {
                status = StoryStatus.Planned;
            }

            var tasks = await _taskRepository.GetByStoryAsync(id, cancellationToken);
            
            // Load codebaseId from metadata
            var codebaseId = await LoadCodebaseIdAsync(id, cancellationToken);

            return new StoryDto
            {
                Id = id,
                Name = id,
                Content = content,
                Type = type,
                Status = status,
                CodebaseId = codebaseId,
                CreatedAt = File.GetCreationTimeUtc(filePath),
                LastProcessedAt = File.GetLastWriteTimeUtc(filePath),
                Tasks = tasks.ToList()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading story file: {FilePath}", filePath);
            return null;
        }
    }

    private static string SanitizeFileName(string name)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(name.Where(c => !invalidChars.Contains(c)).ToArray());
        return sanitized.Replace(' ', '-').ToLower();
    }
}
