namespace AIDevelopmentEasy.Api.Repositories.Interfaces;

/// <summary>
/// Repository interface for generated output (code files, reports).
/// </summary>
public interface IOutputRepository
{
    /// <summary>
    /// Save generated code for a story
    /// </summary>
    Task<string> SaveOutputAsync(string storyId, string projectName, Dictionary<string, string> files, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the output directory path for a story
    /// </summary>
    Task<string?> GetOutputPathAsync(string storyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all generated files for a story
    /// </summary>
    Task<Dictionary<string, string>> GetGeneratedFilesAsync(string storyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Save review report
    /// </summary>
    Task SaveReviewReportAsync(string storyId, string report, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get review report
    /// </summary>
    Task<string?> GetReviewReportAsync(string storyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// List all output directories
    /// </summary>
    Task<IEnumerable<string>> ListOutputsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Save pipeline execution history (summary of all phases)
    /// </summary>
    Task SavePipelineHistoryAsync(string storyId, object pipelineStatus, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get pipeline execution history
    /// </summary>
    Task<string?> GetPipelineHistoryAsync(string storyId, CancellationToken cancellationToken = default);
}
