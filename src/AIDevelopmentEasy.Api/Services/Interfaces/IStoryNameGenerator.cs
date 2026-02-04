namespace AIDevelopmentEasy.Api.Services.Interfaces;

/// <summary>
/// Generates a short, human-readable story title from story content (e.g. via LLM).
/// </summary>
public interface IStoryNameGenerator
{
    /// <summary>
    /// Generate a concise story name from the given content.
    /// Returns a fallback (e.g. first line truncated) if generation fails.
    /// </summary>
    Task<string> GenerateStoryNameAsync(string content, CancellationToken cancellationToken = default);
}
