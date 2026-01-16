using AIDevelopmentEasy.Api.Models;

namespace AIDevelopmentEasy.Api.Repositories.Interfaces;

/// <summary>
/// Repository interface for task operations.
/// Tasks are generated during planning phase.
/// </summary>
public interface ITaskRepository
{
    /// <summary>
    /// Get all tasks for a requirement
    /// </summary>
    Task<IEnumerable<TaskDto>> GetByRequirementAsync(string requirementId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a single task
    /// </summary>
    Task<TaskDto?> GetByIdAsync(string requirementId, int taskIndex, CancellationToken cancellationToken = default);

    /// <summary>
    /// Save tasks for a requirement (replaces existing)
    /// </summary>
    Task SaveTasksAsync(string requirementId, IEnumerable<TaskDto> tasks, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update a single task
    /// </summary>
    Task UpdateTaskAsync(string requirementId, TaskDto task, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete all tasks for a requirement
    /// </summary>
    Task DeleteAllAsync(string requirementId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if tasks exist for a requirement
    /// </summary>
    Task<bool> HasTasksAsync(string requirementId, CancellationToken cancellationToken = default);
}
