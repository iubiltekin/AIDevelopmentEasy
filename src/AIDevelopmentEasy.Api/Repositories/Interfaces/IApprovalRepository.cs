using AIDevelopmentEasy.Api.Models;

namespace AIDevelopmentEasy.Api.Repositories.Interfaces;

/// <summary>
/// Repository interface for approval state management.
/// Tracks which phases have been approved for each story.
/// </summary>
public interface IApprovalRepository
{
    /// <summary>
    /// Check if a story's plan has been approved
    /// </summary>
    Task<bool> IsPlanApprovedAsync(string storyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Approve the plan for a story
    /// </summary>
    Task ApprovePlanAsync(string storyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if a story is marked as completed
    /// </summary>
    Task<bool> IsCompletedAsync(string storyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Mark a story as completed
    /// </summary>
    Task MarkCompletedAsync(string storyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if a story is in progress
    /// </summary>
    Task<bool> IsInProgressAsync(string storyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Mark a story as in progress
    /// </summary>
    Task MarkInProgressAsync(string storyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reset in progress state
    /// </summary>
    Task ResetInProgressAsync(string storyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reset approval state (to reprocess)
    /// </summary>
    Task ResetApprovalAsync(string storyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reset completion state (to reprocess)
    /// </summary>
    Task ResetCompletionAsync(string storyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the current status of a story based on approval/completion state
    /// </summary>
    Task<StoryStatus> GetStatusAsync(string storyId, CancellationToken cancellationToken = default);
}
