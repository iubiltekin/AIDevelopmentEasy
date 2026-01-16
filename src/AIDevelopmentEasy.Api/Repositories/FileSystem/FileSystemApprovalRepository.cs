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
    private readonly string _requirementsPath;
    private readonly ILogger<FileSystemApprovalRepository> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    private const string ApprovedFileName = "_approved.json";
    private const string CompletedFileName = "_completed.json";

    public FileSystemApprovalRepository(string requirementsPath, ILogger<FileSystemApprovalRepository> logger)
    {
        _requirementsPath = requirementsPath;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };
    }

    public Task<bool> IsPlanApprovedAsync(string requirementId, CancellationToken cancellationToken = default)
    {
        var filePath = GetApprovedFilePath(requirementId);
        return Task.FromResult(File.Exists(filePath));
    }

    public async Task ApprovePlanAsync(string requirementId, CancellationToken cancellationToken = default)
    {
        var folderPath = GetRequirementFolder(requirementId);
        Directory.CreateDirectory(folderPath);

        var filePath = GetApprovedFilePath(requirementId);
        var approvalData = new
        {
            ApprovedAt = DateTime.UtcNow,
            RequirementId = requirementId
        };

        var json = JsonSerializer.Serialize(approvalData, _jsonOptions);
        await File.WriteAllTextAsync(filePath, json, cancellationToken);

        _logger.LogInformation("Plan approved for requirement: {RequirementId}", requirementId);
    }

    public Task<bool> IsCompletedAsync(string requirementId, CancellationToken cancellationToken = default)
    {
        var filePath = GetCompletedFilePath(requirementId);
        return Task.FromResult(File.Exists(filePath));
    }

    public async Task MarkCompletedAsync(string requirementId, CancellationToken cancellationToken = default)
    {
        var folderPath = GetRequirementFolder(requirementId);
        Directory.CreateDirectory(folderPath);

        var filePath = GetCompletedFilePath(requirementId);
        var completionData = new
        {
            CompletedAt = DateTime.UtcNow,
            RequirementId = requirementId
        };

        var json = JsonSerializer.Serialize(completionData, _jsonOptions);
        await File.WriteAllTextAsync(filePath, json, cancellationToken);

        _logger.LogInformation("Requirement marked as completed: {RequirementId}", requirementId);
    }

    public Task ResetApprovalAsync(string requirementId, CancellationToken cancellationToken = default)
    {
        var filePath = GetApprovedFilePath(requirementId);
        
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
            _logger.LogInformation("Approval reset for requirement: {RequirementId}", requirementId);
        }

        return Task.CompletedTask;
    }

    public Task ResetCompletionAsync(string requirementId, CancellationToken cancellationToken = default)
    {
        var filePath = GetCompletedFilePath(requirementId);
        
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
            _logger.LogInformation("Completion reset for requirement: {RequirementId}", requirementId);
        }

        return Task.CompletedTask;
    }

    public async Task<RequirementStatus> GetStatusAsync(string requirementId, CancellationToken cancellationToken = default)
    {
        var isCompleted = await IsCompletedAsync(requirementId, cancellationToken);
        if (isCompleted)
            return RequirementStatus.Completed;

        var isApproved = await IsPlanApprovedAsync(requirementId, cancellationToken);
        if (isApproved)
            return RequirementStatus.Approved;

        // Check if has tasks (planned state) - this will be handled by RequirementRepository
        return RequirementStatus.NotStarted;
    }

    private string GetRequirementFolder(string requirementId)
    {
        return Path.Combine(_requirementsPath, requirementId);
    }

    private string GetApprovedFilePath(string requirementId)
    {
        return Path.Combine(GetRequirementFolder(requirementId), ApprovedFileName);
    }

    private string GetCompletedFilePath(string requirementId)
    {
        return Path.Combine(GetRequirementFolder(requirementId), CompletedFileName);
    }
}
