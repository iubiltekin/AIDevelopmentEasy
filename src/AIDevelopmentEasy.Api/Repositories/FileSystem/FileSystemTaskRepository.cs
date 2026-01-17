using System.Text.Json;
using AIDevelopmentEasy.Api.Models;
using AIDevelopmentEasy.Api.Repositories.Interfaces;

namespace AIDevelopmentEasy.Api.Repositories.FileSystem;

/// <summary>
/// File system based implementation of ITaskRepository.
/// Tasks are stored as JSON files in stories/{id}/tasks/ directory.
/// </summary>
public class FileSystemTaskRepository : ITaskRepository
{
    private readonly string _storiesPath;
    private readonly ILogger<FileSystemTaskRepository> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public FileSystemTaskRepository(string storiesPath, ILogger<FileSystemTaskRepository> logger)
    {
        _storiesPath = storiesPath;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };
    }

    public async Task<IEnumerable<TaskDto>> GetByStoryAsync(string storyId, CancellationToken cancellationToken = default)
    {
        var tasks = new List<TaskDto>();
        var tasksDir = GetTasksDirectory(storyId);

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

    public async Task<TaskDto?> GetByIdAsync(string storyId, int taskIndex, CancellationToken cancellationToken = default)
    {
        var filePath = GetTaskFilePath(storyId, taskIndex);
        
        if (!File.Exists(filePath))
            return null;

        try
        {
            var json = await File.ReadAllTextAsync(filePath, cancellationToken);
            return JsonSerializer.Deserialize<TaskDto>(json, _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading task: {StoryId}/{TaskIndex}", storyId, taskIndex);
            return null;
        }
    }

    public async Task SaveTasksAsync(string storyId, IEnumerable<TaskDto> tasks, CancellationToken cancellationToken = default)
    {
        var tasksDir = GetTasksDirectory(storyId);
        
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
            
            var filePath = GetTaskFilePath(storyId, task.Index);
            var json = JsonSerializer.Serialize(task, _jsonOptions);
            await File.WriteAllTextAsync(filePath, json, cancellationToken);
        }

        _logger.LogInformation("Saved {Count} tasks for story: {StoryId}", taskList.Count, storyId);
    }

    public async Task UpdateTaskAsync(string storyId, TaskDto task, CancellationToken cancellationToken = default)
    {
        var tasksDir = GetTasksDirectory(storyId);
        Directory.CreateDirectory(tasksDir);

        var filePath = GetTaskFilePath(storyId, task.Index);
        var json = JsonSerializer.Serialize(task, _jsonOptions);
        await File.WriteAllTextAsync(filePath, json, cancellationToken);

        _logger.LogInformation("Updated task {Index} for story: {StoryId}", task.Index, storyId);
    }

    public async Task AppendFixTasksAsync(string storyId, IEnumerable<TaskDto> fixTasks, int retryAttempt, CancellationToken cancellationToken = default)
    {
        var tasksDir = GetTasksDirectory(storyId);
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

            var filePath = GetTaskFilePath(storyId, task.Index);
            var json = JsonSerializer.Serialize(task, _jsonOptions);
            await File.WriteAllTextAsync(filePath, json, cancellationToken);
        }

        _logger.LogInformation("Appended {Count} fix tasks (retry #{Retry}) for story: {StoryId}. Total tasks: {Total}",
            fixTaskList.Count, retryAttempt, storyId, maxIndex + fixTaskList.Count);
    }

    public Task DeleteAllAsync(string storyId, CancellationToken cancellationToken = default)
    {
        var tasksDir = GetTasksDirectory(storyId);
        
        if (Directory.Exists(tasksDir))
        {
            Directory.Delete(tasksDir, recursive: true);
            _logger.LogInformation("Deleted all tasks for story: {StoryId}", storyId);
        }

        return Task.CompletedTask;
    }

    public Task<bool> HasTasksAsync(string storyId, CancellationToken cancellationToken = default)
    {
        var tasksDir = GetTasksDirectory(storyId);
        
        if (!Directory.Exists(tasksDir))
            return Task.FromResult(false);

        return Task.FromResult(Directory.GetFiles(tasksDir, "task-*.json").Length > 0);
    }

    private string GetTasksDirectory(string storyId)
    {
        return Path.Combine(_storiesPath, storyId, "tasks");
    }

    private string GetTaskFilePath(string storyId, int taskIndex)
    {
        return Path.Combine(GetTasksDirectory(storyId), $"task-{taskIndex:D2}.json");
    }
}
