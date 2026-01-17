using AIDevelopmentEasy.CLI.Models;

namespace AIDevelopmentEasy.CLI.Services.Interfaces;

/// <summary>
/// Pipeline runner for step-by-step story processing
/// </summary>
public interface IPipelineRunner
{
    /// <summary>
    /// Process a story through the full pipeline with user confirmations
    /// </summary>
    Task ProcessAsync(StoryInfo story);
}
