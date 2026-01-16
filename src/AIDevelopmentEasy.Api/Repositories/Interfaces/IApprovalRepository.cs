using AIDevelopmentEasy.Api.Models;

namespace AIDevelopmentEasy.Api.Repositories.Interfaces;

/// <summary>
/// Repository interface for approval state management.
/// Tracks which phases have been approved for each requirement.
/// </summary>
public interface IApprovalRepository
{
    /// <summary>
    /// Check if a requirement's plan has been approved
    /// </summary>
    Task<bool> IsPlanApprovedAsync(string requirementId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Approve the plan for a requirement
    /// </summary>
    Task ApprovePlanAsync(string requirementId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if a requirement is marked as completed
    /// </summary>
    Task<bool> IsCompletedAsync(string requirementId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Mark a requirement as completed
    /// </summary>
    Task MarkCompletedAsync(string requirementId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reset approval state (to reprocess)
    /// </summary>
    Task ResetApprovalAsync(string requirementId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reset completion state (to reprocess)
    /// </summary>
    Task ResetCompletionAsync(string requirementId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the current status of a requirement based on approval/completion state
    /// </summary>
    Task<RequirementStatus> GetStatusAsync(string requirementId, CancellationToken cancellationToken = default);
}
