using AIDevelopmentEasy.Api.Models;
using AIDevelopmentEasy.Core.Models;

namespace AIDevelopmentEasy.Api.Services.Interfaces;

/// <summary>
/// Service interface for Knowledge Base operations.
/// Provides high-level operations for capturing and utilizing knowledge.
/// </summary>
public interface IKnowledgeService
{
    // ═══════════════════════════════════════════════════════════════════════════════
    // Pattern Capture (from pipeline success)
    // ═══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Capture a successful code pattern from a completed story
    /// </summary>
    Task CapturePatternFromStoryAsync(
        string storyId,
        string taskTitle,
        string taskDescription,
        string solutionCode,
        List<string>? tags = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Capture successful patterns from a completed pipeline
    /// </summary>
    Task CaptureFromCompletedPipelineAsync(
        string storyId,
        Dictionary<string, string> generatedFiles,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Capture agent usage insights from a completed pipeline (LLM calls per agent).
    /// Feeds the knowledge-base insights folder.
    /// </summary>
    Task CaptureAgentInsightsFromPipelineAsync(
        string storyId,
        string storyTitle,
        IReadOnlyList<LLMCallResult> llmCalls,
        CancellationToken cancellationToken = default);

    // ═══════════════════════════════════════════════════════════════════════════════
    // Error Capture (from pipeline failures)
    // ═══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Capture an error and its fix from debugging phase
    /// </summary>
    Task CaptureErrorFixAsync(
        string storyId,
        string errorMessage,
        ErrorType errorType,
        string fixDescription,
        string? fixCode = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Capture test failure and fix
    /// </summary>
    Task CaptureTestFailureFixAsync(
        string storyId,
        string testName,
        string errorMessage,
        string fixDescription,
        string? fixCode = null,
        CancellationToken cancellationToken = default);

    // ═══════════════════════════════════════════════════════════════════════════════
    // Knowledge Utilization
    // ═══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Find relevant patterns for a task description (for PlannerAgent)
    /// </summary>
    Task<List<SuccessfulPattern>> FindRelevantPatternsAsync(
        string taskDescription,
        int limit = 3,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Find a known fix for an error (for DebuggerAgent)
    /// </summary>
    Task<CommonError?> FindKnownErrorFixAsync(
        string errorMessage,
        ErrorType? errorType = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate context for prompt injection (combines relevant patterns)
    /// </summary>
    Task<string?> GenerateKnowledgeContextAsync(
        string taskDescription,
        int maxPatterns = 3,
        CancellationToken cancellationToken = default);

    // ═══════════════════════════════════════════════════════════════════════════════
    // Verification
    // ═══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Mark all knowledge entries from a story as verified (after successful PR)
    /// </summary>
    Task VerifyStoryKnowledgeAsync(string storyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Record that a knowledge entry was used and whether it was successful
    /// </summary>
    Task RecordKnowledgeUsageAsync(
        string knowledgeId,
        bool wasSuccessful,
        CancellationToken cancellationToken = default);
}
