namespace AIDevelopmentEasy.Api.Repositories.Interfaces;

/// <summary>
/// Repository interface for generated output (code files, reports).
/// </summary>
public interface IOutputRepository
{
    /// <summary>
    /// Save generated code for a requirement
    /// </summary>
    Task<string> SaveOutputAsync(string requirementId, string projectName, Dictionary<string, string> files, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the output directory path for a requirement
    /// </summary>
    Task<string?> GetOutputPathAsync(string requirementId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all generated files for a requirement
    /// </summary>
    Task<Dictionary<string, string>> GetGeneratedFilesAsync(string requirementId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Save review report
    /// </summary>
    Task SaveReviewReportAsync(string requirementId, string report, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get review report
    /// </summary>
    Task<string?> GetReviewReportAsync(string requirementId, CancellationToken cancellationToken = default);

    /// <summary>
    /// List all output directories
    /// </summary>
    Task<IEnumerable<string>> ListOutputsAsync(CancellationToken cancellationToken = default);
}
