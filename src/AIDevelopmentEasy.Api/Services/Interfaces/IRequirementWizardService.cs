using AIDevelopmentEasy.Api.Models;

namespace AIDevelopmentEasy.Api.Services.Interfaces;

/// <summary>
/// Service interface for Requirement Wizard orchestration.
/// Manages the wizard workflow with approval gates at each phase.
/// </summary>
public interface IRequirementWizardService
{
    /// <summary>
    /// Start the wizard for a requirement.
    /// Begins with Analysis phase (LLM generates questions).
    /// </summary>
    /// <param name="requirementId">The requirement to process</param>
    /// <param name="autoApproveAll">Skip approval gates and run to completion</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Current wizard status</returns>
    Task<WizardStatusDto> StartAsync(string requirementId, bool autoApproveAll = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get current wizard status for a requirement.
    /// </summary>
    Task<WizardStatusDto?> GetStatusAsync(string requirementId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Approve the current phase and proceed to the next.
    /// </summary>
    /// <param name="requirementId">The requirement</param>
    /// <param name="approved">Whether to approve (true) or reject (false)</param>
    /// <param name="comment">Optional comment</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated wizard status</returns>
    Task<WizardStatusDto> ApprovePhaseAsync(string requirementId, bool approved = true, string? comment = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Submit answers to questions (Questions phase → Refinement phase).
    /// </summary>
    /// <param name="requirementId">The requirement</param>
    /// <param name="request">Answers and optional AI notes</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated wizard status</returns>
    Task<WizardStatusDto> SubmitAnswersAsync(string requirementId, SubmitAnswersRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create stories from selected definitions (Review phase → Completed).
    /// </summary>
    /// <param name="requirementId">The requirement</param>
    /// <param name="request">Selected story IDs</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated wizard status with created story IDs</returns>
    Task<WizardStatusDto> CreateStoriesAsync(string requirementId, CreateStoriesRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancel the wizard process.
    /// </summary>
    Task<WizardStatusDto> CancelAsync(string requirementId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if wizard is currently running for a requirement.
    /// </summary>
    Task<bool> IsRunningAsync(string requirementId, CancellationToken cancellationToken = default);
}
