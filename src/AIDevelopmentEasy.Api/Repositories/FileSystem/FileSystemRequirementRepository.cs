using System.Text.Json;
using AIDevelopmentEasy.Api.Models;
using AIDevelopmentEasy.Api.Repositories.Interfaces;

namespace AIDevelopmentEasy.Api.Repositories.FileSystem;

/// <summary>
/// File system based implementation of IRequirementRepository.
/// Requirements are stored as .md, .txt, or .json files in the requirements directory.
/// </summary>
public class FileSystemRequirementRepository : IRequirementRepository
{
    private readonly string _requirementsPath;
    private readonly IApprovalRepository _approvalRepository;
    private readonly ITaskRepository _taskRepository;
    private readonly ILogger<FileSystemRequirementRepository> _logger;

    private static readonly string[] SupportedExtensions = { ".json", ".md", ".txt" };

    public FileSystemRequirementRepository(
        string requirementsPath,
        IApprovalRepository approvalRepository,
        ITaskRepository taskRepository,
        ILogger<FileSystemRequirementRepository> logger)
    {
        _requirementsPath = requirementsPath;
        _approvalRepository = approvalRepository;
        _taskRepository = taskRepository;
        _logger = logger;

        if (!Directory.Exists(_requirementsPath))
        {
            Directory.CreateDirectory(_requirementsPath);
        }
    }

    public async Task<IEnumerable<RequirementDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var requirements = new List<RequirementDto>();

        if (!Directory.Exists(_requirementsPath))
            return requirements;

        var files = Directory.GetFiles(_requirementsPath)
            .Where(f => SupportedExtensions.Contains(Path.GetExtension(f).ToLower()))
            .OrderBy(f => f);

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var requirement = await CreateRequirementDtoFromFile(file, cancellationToken);
            if (requirement != null)
            {
                requirements.Add(requirement);
            }
        }

        return requirements;
    }

    public async Task<RequirementDto?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var filePath = FindRequirementFile(id);
        if (filePath == null)
            return null;

        return await CreateRequirementDtoFromFile(filePath, cancellationToken);
    }

    public async Task<string?> GetContentAsync(string id, CancellationToken cancellationToken = default)
    {
        var filePath = FindRequirementFile(id);
        if (filePath == null)
            return null;

        return await File.ReadAllTextAsync(filePath, cancellationToken);
    }

    public async Task<RequirementDto> CreateAsync(string name, string content, RequirementType type, CancellationToken cancellationToken = default)
    {
        var extension = type == RequirementType.Multi ? ".json" : ".md";
        var fileName = SanitizeFileName(name) + extension;
        var filePath = Path.Combine(_requirementsPath, fileName);

        // Ensure unique filename
        var counter = 1;
        while (File.Exists(filePath))
        {
            fileName = $"{SanitizeFileName(name)}_{counter}{extension}";
            filePath = Path.Combine(_requirementsPath, fileName);
            counter++;
        }

        await File.WriteAllTextAsync(filePath, content, cancellationToken);

        _logger.LogInformation("Created requirement file: {FilePath}", filePath);

        return new RequirementDto
        {
            Id = Path.GetFileNameWithoutExtension(fileName),
            Name = Path.GetFileNameWithoutExtension(fileName),
            Content = content,
            Type = type,
            Status = RequirementStatus.NotStarted,
            CreatedAt = DateTime.UtcNow
        };
    }

    public async Task UpdateStatusAsync(string id, RequirementStatus status, CancellationToken cancellationToken = default)
    {
        // Status is managed through approval repository, this is for future database implementations
        switch (status)
        {
            case RequirementStatus.InProgress:
                await _approvalRepository.MarkInProgressAsync(id, cancellationToken);
                break;
            case RequirementStatus.Approved:
                await _approvalRepository.ResetInProgressAsync(id, cancellationToken);
                await _approvalRepository.ApprovePlanAsync(id, cancellationToken);
                break;
            case RequirementStatus.Completed:
                await _approvalRepository.ResetInProgressAsync(id, cancellationToken);
                await _approvalRepository.MarkCompletedAsync(id, cancellationToken);
                break;
            case RequirementStatus.NotStarted:
                await _approvalRepository.ResetApprovalAsync(id, cancellationToken);
                await _approvalRepository.ResetCompletionAsync(id, cancellationToken);
                await _approvalRepository.ResetInProgressAsync(id, cancellationToken);
                break;
        }
    }

    public Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var filePath = FindRequirementFile(id);
        if (filePath == null)
            return Task.FromResult(false);

        File.Delete(filePath);

        // Also delete associated folder
        var folderPath = Path.Combine(_requirementsPath, id);
        if (Directory.Exists(folderPath))
        {
            Directory.Delete(folderPath, recursive: true);
        }

        _logger.LogInformation("Deleted requirement: {Id}", id);
        return Task.FromResult(true);
    }

    public Task<bool> ExistsAsync(string id, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(FindRequirementFile(id) != null);
    }

    private string? FindRequirementFile(string id)
    {
        foreach (var ext in SupportedExtensions)
        {
            var path = Path.Combine(_requirementsPath, id + ext);
            if (File.Exists(path))
                return path;
        }
        return null;
    }

    private async Task<RequirementDto?> CreateRequirementDtoFromFile(string filePath, CancellationToken cancellationToken)
    {
        try
        {
            var fileName = Path.GetFileName(filePath);
            var id = Path.GetFileNameWithoutExtension(fileName);
            var extension = Path.GetExtension(filePath).ToLower();
            var content = await File.ReadAllTextAsync(filePath, cancellationToken);

            var type = extension == ".json" ? RequirementType.Multi : RequirementType.Single;
            var status = await _approvalRepository.GetStatusAsync(id, cancellationToken);
            var hasTasks = await _taskRepository.HasTasksAsync(id, cancellationToken);

            // Adjust status based on tasks
            if (status == RequirementStatus.NotStarted && hasTasks)
            {
                status = RequirementStatus.Planned;
            }

            var tasks = await _taskRepository.GetByRequirementAsync(id, cancellationToken);

            return new RequirementDto
            {
                Id = id,
                Name = id,
                Content = content,
                Type = type,
                Status = status,
                CreatedAt = File.GetCreationTimeUtc(filePath),
                LastProcessedAt = File.GetLastWriteTimeUtc(filePath),
                Tasks = tasks.ToList()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading requirement file: {FilePath}", filePath);
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
