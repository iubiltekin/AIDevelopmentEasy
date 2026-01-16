using AIDevelopmentEasy.Api.Models;
using AIDevelopmentEasy.Core.Models;

namespace AIDevelopmentEasy.Api.Repositories.Interfaces;

/// <summary>
/// Repository interface for codebase management.
/// Handles registration, analysis storage, and retrieval of codebases.
/// </summary>
public interface ICodebaseRepository
{
    /// <summary>
    /// Get all registered codebases
    /// </summary>
    Task<IEnumerable<CodebaseDto>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a codebase by ID
    /// </summary>
    Task<CodebaseDto?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the full analysis for a codebase
    /// </summary>
    Task<CodebaseAnalysis?> GetAnalysisAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Register a new codebase
    /// </summary>
    Task<CodebaseDto> CreateAsync(string name, string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Save analysis results for a codebase
    /// </summary>
    Task SaveAnalysisAsync(string id, CodebaseAnalysis analysis, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update codebase status
    /// </summary>
    Task UpdateStatusAsync(string id, Models.CodebaseStatus status, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete a codebase
    /// </summary>
    Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if a codebase exists
    /// </summary>
    Task<bool> ExistsAsync(string id, CancellationToken cancellationToken = default);
}
