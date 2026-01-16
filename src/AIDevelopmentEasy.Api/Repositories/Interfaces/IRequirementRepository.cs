using AIDevelopmentEasy.Api.Models;

namespace AIDevelopmentEasy.Api.Repositories.Interfaces;

/// <summary>
/// Repository interface for requirement operations.
/// Implementations can be file-based, database, or Jira-based.
/// </summary>
public interface IRequirementRepository
{
    /// <summary>
    /// Get all requirements
    /// </summary>
    Task<IEnumerable<RequirementDto>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a single requirement by ID
    /// </summary>
    Task<RequirementDto?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get requirement content (raw text or JSON)
    /// </summary>
    Task<string?> GetContentAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a new requirement
    /// </summary>
    Task<RequirementDto> CreateAsync(string name, string content, RequirementType type, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update requirement status
    /// </summary>
    Task UpdateStatusAsync(string id, RequirementStatus status, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete a requirement
    /// </summary>
    Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if requirement exists
    /// </summary>
    Task<bool> ExistsAsync(string id, CancellationToken cancellationToken = default);
}
