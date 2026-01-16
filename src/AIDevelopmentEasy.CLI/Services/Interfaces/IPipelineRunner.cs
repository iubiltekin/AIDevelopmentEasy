using AIDevelopmentEasy.CLI.Models;

namespace AIDevelopmentEasy.CLI.Services.Interfaces;

/// <summary>
/// Pipeline runner for step-by-step requirement processing
/// </summary>
public interface IPipelineRunner
{
    /// <summary>
    /// Process a requirement through the full pipeline with user confirmations
    /// </summary>
    Task ProcessAsync(RequirementInfo requirement);
}
