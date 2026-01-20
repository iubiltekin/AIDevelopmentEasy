using AIDevelopmentEasy.Api.Models;

namespace AIDevelopmentEasy.Api.Repositories.Interfaces;

/// <summary>
/// Repository interface for story operations.
/// Implementations can be file-based, database, or Jira-based.
/// </summary>
public interface IStoryRepository
{
    /// <summary>
    /// Get all stories
    /// </summary>
    Task<IEnumerable<StoryDto>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a single story by ID
    /// </summary>
    Task<StoryDto?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get story content (raw text or JSON)
    /// </summary>
    Task<string?> GetContentAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a new story
    /// </summary>
    Task<StoryDto> CreateAsync(string name, string content, StoryType type, string? codebaseId = null, string? requirementId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a new story with full request details including target info
    /// </summary>
    Task<StoryDto> CreateAsync(CreateStoryRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update story status
    /// </summary>
    Task UpdateStatusAsync(string id, StoryStatus status, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update story target information (project, file, class, method, change type)
    /// </summary>
    Task<bool> UpdateTargetAsync(string id, UpdateStoryTargetRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update story content (only allowed when status is NotStarted/Draft)
    /// </summary>
    Task<bool> UpdateContentAsync(string id, string content, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update story name
    /// </summary>
    Task<bool> UpdateNameAsync(string id, string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete a story
    /// </summary>
    Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if story exists
    /// </summary>
    Task<bool> ExistsAsync(string id, CancellationToken cancellationToken = default);
}
