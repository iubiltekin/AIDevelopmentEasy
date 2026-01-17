using System.Text.Json;
using AIDevelopmentEasy.Api.Models;
using AIDevelopmentEasy.Api.Repositories.Interfaces;

namespace AIDevelopmentEasy.Api.Repositories.FileSystem;

/// <summary>
/// File system based implementation of IApprovalRepository.
/// Approval state is stored as _approved.json and _completed.json files.
/// </summary>
public class FileSystemApprovalRepository : IApprovalRepository
{
    private readonly string _storiesPath;
    private readonly ILogger<FileSystemApprovalRepository> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    private const string ApprovedFileName = "_approved.json";
    private const string CompletedFileName = "_completed.json";
    private const string InProgressFileName = "_inprogress.json";

    public FileSystemApprovalRepository(string storiesPath, ILogger<FileSystemApprovalRepository> logger)
    {
        _storiesPath = storiesPath;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };
    }

    public Task<bool> IsPlanApprovedAsync(string storyId, CancellationToken cancellationToken = default)
    {
        var filePath = GetApprovedFilePath(storyId);
        return Task.FromResult(File.Exists(filePath));
    }

    public async Task ApprovePlanAsync(string storyId, CancellationToken cancellationToken = default)
    {
        var folderPath = GetStoryFolder(storyId);
        Directory.CreateDirectory(folderPath);

        var filePath = GetApprovedFilePath(storyId);
        var approvalData = new
        {
            ApprovedAt = DateTime.UtcNow,
            StoryId = storyId
        };

        var json = JsonSerializer.Serialize(approvalData, _jsonOptions);
        await File.WriteAllTextAsync(filePath, json, cancellationToken);

        _logger.LogInformation("Plan approved for story: {StoryId}", storyId);
    }

    public Task<bool> IsCompletedAsync(string storyId, CancellationToken cancellationToken = default)
    {
        var filePath = GetCompletedFilePath(storyId);
        return Task.FromResult(File.Exists(filePath));
    }

    public async Task MarkCompletedAsync(string storyId, CancellationToken cancellationToken = default)
    {
        var folderPath = GetStoryFolder(storyId);
        Directory.CreateDirectory(folderPath);

        var filePath = GetCompletedFilePath(storyId);
        var completionData = new
        {
            CompletedAt = DateTime.UtcNow,
            StoryId = storyId
        };

        var json = JsonSerializer.Serialize(completionData, _jsonOptions);
        await File.WriteAllTextAsync(filePath, json, cancellationToken);

        _logger.LogInformation("Story marked as completed: {StoryId}", storyId);
    }

    public Task ResetApprovalAsync(string storyId, CancellationToken cancellationToken = default)
    {
        var filePath = GetApprovedFilePath(storyId);
        
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
            _logger.LogInformation("Approval reset for story: {StoryId}", storyId);
        }

        return Task.CompletedTask;
    }

    public Task ResetCompletionAsync(string storyId, CancellationToken cancellationToken = default)
    {
        var filePath = GetCompletedFilePath(storyId);
        
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
            _logger.LogInformation("Completion reset for story: {StoryId}", storyId);
        }

        return Task.CompletedTask;
    }

    public Task<bool> IsInProgressAsync(string storyId, CancellationToken cancellationToken = default)
    {
        var filePath = GetInProgressFilePath(storyId);
        return Task.FromResult(File.Exists(filePath));
    }

    public async Task MarkInProgressAsync(string storyId, CancellationToken cancellationToken = default)
    {
        var folderPath = GetStoryFolder(storyId);
        Directory.CreateDirectory(folderPath);

        var filePath = GetInProgressFilePath(storyId);
        var data = new
        {
            StartedAt = DateTime.UtcNow,
            StoryId = storyId
        };

        var json = JsonSerializer.Serialize(data, _jsonOptions);
        await File.WriteAllTextAsync(filePath, json, cancellationToken);

        _logger.LogInformation("Story marked as in progress: {StoryId}", storyId);
    }

    public Task ResetInProgressAsync(string storyId, CancellationToken cancellationToken = default)
    {
        var filePath = GetInProgressFilePath(storyId);
        
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
            _logger.LogInformation("In progress reset for story: {StoryId}", storyId);
        }

        return Task.CompletedTask;
    }

    public async Task<StoryStatus> GetStatusAsync(string storyId, CancellationToken cancellationToken = default)
    {
        var isCompleted = await IsCompletedAsync(storyId, cancellationToken);
        if (isCompleted)
            return StoryStatus.Completed;

        var isInProgress = await IsInProgressAsync(storyId, cancellationToken);
        if (isInProgress)
            return StoryStatus.InProgress;

        var isApproved = await IsPlanApprovedAsync(storyId, cancellationToken);
        if (isApproved)
            return StoryStatus.Approved;

        // Check if has tasks (planned state) - this will be handled by StoryRepository
        return StoryStatus.NotStarted;
    }

    private string GetStoryFolder(string storyId)
    {
        return Path.Combine(_storiesPath, storyId);
    }

    private string GetApprovedFilePath(string storyId)
    {
        return Path.Combine(GetStoryFolder(storyId), ApprovedFileName);
    }

    private string GetCompletedFilePath(string storyId)
    {
        return Path.Combine(GetStoryFolder(storyId), CompletedFileName);
    }

    private string GetInProgressFilePath(string storyId)
    {
        return Path.Combine(GetStoryFolder(storyId), InProgressFileName);
    }
}
