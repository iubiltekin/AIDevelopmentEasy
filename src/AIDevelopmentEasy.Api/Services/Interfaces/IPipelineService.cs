using AIDevelopmentEasy.Api.Models;

namespace AIDevelopmentEasy.Api.Services.Interfaces;

/// <summary>
/// Service interface for pipeline orchestration.
/// Manages the execution of the multi-agent pipeline.
/// </summary>
public interface IPipelineService
{
    /// <summary>
    /// Start processing a requirement
    /// </summary>
    Task<PipelineStatusDto> StartAsync(string requirementId, bool autoApproveAll = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the current status of a pipeline
    /// </summary>
    Task<PipelineStatusDto?> GetStatusAsync(string requirementId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Approve a phase and continue execution
    /// </summary>
    Task<bool> ApprovePhaseAsync(string requirementId, PipelinePhase phase, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reject a phase (stop execution)
    /// </summary>
    Task<bool> RejectPhaseAsync(string requirementId, PipelinePhase phase, string? reason = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancel a running pipeline
    /// </summary>
    Task CancelAsync(string requirementId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if a pipeline is currently running
    /// </summary>
    Task<bool> IsRunningAsync(string requirementId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all running pipelines
    /// </summary>
    Task<IEnumerable<string>> GetRunningPipelinesAsync(CancellationToken cancellationToken = default);
}
