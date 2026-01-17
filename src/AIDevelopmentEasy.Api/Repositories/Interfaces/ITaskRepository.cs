using AIDevelopmentEasy.Api.Models;

namespace AIDevelopmentEasy.Api.Repositories.Interfaces;

/// <summary>
/// Repository interface for task operations.
/// Tasks are generated during planning phase.
/// </summary>
public interface ITaskRepository
{
    /// <summary>
    /// Get all tasks for a story
    /// </summary>
    Task<IEnumerable<TaskDto>> GetByStoryAsync(string storyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a single task
    /// </summary>
    Task<TaskDto?> GetByIdAsync(string storyId, int taskIndex, CancellationToken cancellationToken = default);

    /// <summary>
    /// Save tasks for a story (replaces existing)
    /// </summary>
    Task SaveTasksAsync(string storyId, IEnumerable<TaskDto> tasks, CancellationToken cancellationToken = default);

    /// <summary>
    /// Append fix tasks to existing tasks (preserves original tasks)
    /// </summary>
    Task AppendFixTasksAsync(string storyId, IEnumerable<TaskDto> fixTasks, int retryAttempt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update a single task
    /// </summary>
    Task UpdateTaskAsync(string storyId, TaskDto task, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete all tasks for a story
    /// </summary>
    Task DeleteAllAsync(string storyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if tasks exist for a story
    /// </summary>
    Task<bool> HasTasksAsync(string storyId, CancellationToken cancellationToken = default);
}
