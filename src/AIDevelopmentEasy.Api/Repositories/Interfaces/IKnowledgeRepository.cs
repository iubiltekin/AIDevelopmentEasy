using AIDevelopmentEasy.Core.Models;

namespace AIDevelopmentEasy.Api.Repositories.Interfaces;

/// <summary>
/// Repository interface for Knowledge Base operations.
/// Manages patterns, common errors, templates, and agent insights.
/// </summary>
public interface IKnowledgeRepository
{
    // ═══════════════════════════════════════════════════════════════════════════════
    // CRUD Operations
    // ═══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Get all knowledge entries
    /// </summary>
    Task<IEnumerable<KnowledgeEntry>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get knowledge entries by category
    /// </summary>
    Task<IEnumerable<KnowledgeEntry>> GetByCategoryAsync(KnowledgeCategory category, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a single knowledge entry by ID
    /// </summary>
    Task<KnowledgeEntry?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a new knowledge entry
    /// </summary>
    Task<KnowledgeEntry> CreateAsync(KnowledgeEntry entry, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update an existing knowledge entry
    /// </summary>
    Task<bool> UpdateAsync(KnowledgeEntry entry, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete a knowledge entry
    /// </summary>
    Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if a knowledge entry exists
    /// </summary>
    Task<bool> ExistsAsync(string id, CancellationToken cancellationToken = default);

    // ═══════════════════════════════════════════════════════════════════════════════
    // Pattern Operations
    // ═══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Get all successful patterns
    /// </summary>
    Task<IEnumerable<SuccessfulPattern>> GetPatternsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a pattern by ID
    /// </summary>
    Task<SuccessfulPattern?> GetPatternByIdAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Find patterns similar to a problem description
    /// </summary>
    Task<PatternSearchResult> FindSimilarPatternsAsync(
        string problemDescription,
        int limit = 5,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get patterns by subcategory
    /// </summary>
    Task<IEnumerable<SuccessfulPattern>> GetPatternsBySubcategoryAsync(
        PatternSubcategory subcategory,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a new pattern
    /// </summary>
    Task<SuccessfulPattern> CreatePatternAsync(CapturePatternRequest request, CancellationToken cancellationToken = default);

    // ═══════════════════════════════════════════════════════════════════════════════
    // Error Operations
    // ═══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Get all common errors
    /// </summary>
    Task<IEnumerable<CommonError>> GetErrorsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get an error by ID
    /// </summary>
    Task<CommonError?> GetErrorByIdAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Find an error that matches the given error message
    /// </summary>
    Task<ErrorMatchResult> FindMatchingErrorAsync(
        string errorMessage,
        ErrorType? errorType = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get errors by type
    /// </summary>
    Task<IEnumerable<CommonError>> GetErrorsByTypeAsync(
        ErrorType errorType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a new error entry
    /// </summary>
    Task<CommonError> CreateErrorAsync(CaptureErrorRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Increment error occurrence count
    /// </summary>
    Task IncrementErrorOccurrenceAsync(string id, CancellationToken cancellationToken = default);

    // ═══════════════════════════════════════════════════════════════════════════════
    // Template Operations
    // ═══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Get all project templates
    /// </summary>
    Task<IEnumerable<ProjectTemplate>> GetTemplatesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a template by ID
    /// </summary>
    Task<ProjectTemplate?> GetTemplateByIdAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get templates by type
    /// </summary>
    Task<IEnumerable<ProjectTemplate>> GetTemplatesByTypeAsync(
        string templateType,
        CancellationToken cancellationToken = default);

    // ═══════════════════════════════════════════════════════════════════════════════
    // Search Operations
    // ═══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Search knowledge base with parameters
    /// </summary>
    Task<IEnumerable<KnowledgeEntry>> SearchAsync(
        KnowledgeSearchParams searchParams,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Search by tags
    /// </summary>
    Task<IEnumerable<KnowledgeEntry>> SearchByTagsAsync(
        List<string> tags,
        KnowledgeCategory? category = null,
        CancellationToken cancellationToken = default);

    // ═══════════════════════════════════════════════════════════════════════════════
    // Usage Tracking
    // ═══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Record that a knowledge entry was used
    /// </summary>
    Task RecordUsageAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update success rate of a knowledge entry
    /// </summary>
    Task UpdateSuccessRateAsync(string id, bool wasSuccessful, CancellationToken cancellationToken = default);

    /// <summary>
    /// Mark a knowledge entry as verified
    /// </summary>
    Task MarkAsVerifiedAsync(string id, CancellationToken cancellationToken = default);

    // ═══════════════════════════════════════════════════════════════════════════════
    // Statistics
    // ═══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Get knowledge base statistics
    /// </summary>
    Task<KnowledgeStats> GetStatsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all unique tags
    /// </summary>
    Task<IEnumerable<string>> GetAllTagsAsync(CancellationToken cancellationToken = default);
}
