using System.Text.Json;
using AIDevelopmentEasy.Api.Models;
using AIDevelopmentEasy.Api.Repositories.Interfaces;

namespace AIDevelopmentEasy.Api.Repositories.FileSystem;

/// <summary>
/// File system based implementation of ITaskRepository.
/// Tasks are stored as JSON files in requirements/{id}/tasks/ directory.
/// </summary>
public class FileSystemTaskRepository : ITaskRepository
{
    private readonly string _requirementsPath;
    private readonly ILogger<FileSystemTaskRepository> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public FileSystemTaskRepository(string requirementsPath, ILogger<FileSystemTaskRepository> logger)
    {
        _requirementsPath = requirementsPath;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };
    }

    public async Task<IEnumerable<TaskDto>> GetByRequirementAsync(string requirementId, CancellationToken cancellationToken = default)
    {
        var tasks = new List<TaskDto>();
        var tasksDir = GetTasksDirectory(requirementId);

        if (!Directory.Exists(tasksDir))
            return tasks;

        var taskFiles = Directory.GetFiles(tasksDir, "task-*.json")
            .OrderBy(f => f);

        foreach (var file in taskFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            try
            {
                var json = await File.ReadAllTextAsync(file, cancellationToken);
                var task = JsonSerializer.Deserialize<TaskDto>(json, _jsonOptions);
                if (task != null)
                {
                    tasks.Add(task);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error reading task file: {File}", file);
            }
        }

        return tasks;
    }

    public async Task<TaskDto?> GetByIdAsync(string requirementId, int taskIndex, CancellationToken cancellationToken = default)
    {
        var filePath = GetTaskFilePath(requirementId, taskIndex);
        
        if (!File.Exists(filePath))
            return null;

        try
        {
            var json = await File.ReadAllTextAsync(filePath, cancellationToken);
            return JsonSerializer.Deserialize<TaskDto>(json, _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading task: {RequirementId}/{TaskIndex}", requirementId, taskIndex);
            return null;
        }
    }

    public async Task SaveTasksAsync(string requirementId, IEnumerable<TaskDto> tasks, CancellationToken cancellationToken = default)
    {
        var tasksDir = GetTasksDirectory(requirementId);
        
        // Ensure directory exists
        Directory.CreateDirectory(tasksDir);

        // Delete existing tasks
        foreach (var existingFile in Directory.GetFiles(tasksDir, "task-*.json"))
        {
            File.Delete(existingFile);
        }

        // Save new tasks
        var taskList = tasks.ToList();
        for (int i = 0; i < taskList.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var task = taskList[i];
            task.Index = i + 1; // Ensure correct index
            
            var filePath = GetTaskFilePath(requirementId, task.Index);
            var json = JsonSerializer.Serialize(task, _jsonOptions);
            await File.WriteAllTextAsync(filePath, json, cancellationToken);
        }

        _logger.LogInformation("Saved {Count} tasks for requirement: {RequirementId}", taskList.Count, requirementId);
    }

    public async Task UpdateTaskAsync(string requirementId, TaskDto task, CancellationToken cancellationToken = default)
    {
        var tasksDir = GetTasksDirectory(requirementId);
        Directory.CreateDirectory(tasksDir);

        var filePath = GetTaskFilePath(requirementId, task.Index);
        var json = JsonSerializer.Serialize(task, _jsonOptions);
        await File.WriteAllTextAsync(filePath, json, cancellationToken);

        _logger.LogInformation("Updated task {Index} for requirement: {RequirementId}", task.Index, requirementId);
    }

    public async Task AppendFixTasksAsync(string requirementId, IEnumerable<TaskDto> fixTasks, int retryAttempt, CancellationToken cancellationToken = default)
    {
        var tasksDir = GetTasksDirectory(requirementId);
        Directory.CreateDirectory(tasksDir);

        // Get current max index from existing tasks
        var existingFiles = Directory.GetFiles(tasksDir, "task-*.json");
        int maxIndex = 0;
        
        foreach (var file in existingFiles)
        {
            var fileName = Path.GetFileNameWithoutExtension(file);
            if (fileName.StartsWith("task-") && int.TryParse(fileName.Substring(5), out int idx))
            {
                maxIndex = Math.Max(maxIndex, idx);
            }
        }

        // Append fix tasks with new indices
        var fixTaskList = fixTasks.ToList();
        for (int i = 0; i < fixTaskList.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var task = fixTaskList[i];
            task.Index = maxIndex + i + 1; // Continue from last index
            task.Type = TaskType.Fix;
            task.RetryAttempt = retryAttempt;

            var filePath = GetTaskFilePath(requirementId, task.Index);
            var json = JsonSerializer.Serialize(task, _jsonOptions);
            await File.WriteAllTextAsync(filePath, json, cancellationToken);
        }

        _logger.LogInformation("Appended {Count} fix tasks (retry #{Retry}) for requirement: {RequirementId}. Total tasks: {Total}",
            fixTaskList.Count, retryAttempt, requirementId, maxIndex + fixTaskList.Count);
    }

    public Task DeleteAllAsync(string requirementId, CancellationToken cancellationToken = default)
    {
        var tasksDir = GetTasksDirectory(requirementId);
        
        if (Directory.Exists(tasksDir))
        {
            Directory.Delete(tasksDir, recursive: true);
            _logger.LogInformation("Deleted all tasks for requirement: {RequirementId}", requirementId);
        }

        return Task.CompletedTask;
    }

    public Task<bool> HasTasksAsync(string requirementId, CancellationToken cancellationToken = default)
    {
        var tasksDir = GetTasksDirectory(requirementId);
        
        if (!Directory.Exists(tasksDir))
            return Task.FromResult(false);

        return Task.FromResult(Directory.GetFiles(tasksDir, "task-*.json").Length > 0);
    }

    private string GetTasksDirectory(string requirementId)
    {
        return Path.Combine(_requirementsPath, requirementId, "tasks");
    }

    private string GetTaskFilePath(string requirementId, int taskIndex)
    {
        return Path.Combine(GetTasksDirectory(requirementId), $"task-{taskIndex:D2}.json");
    }
}
