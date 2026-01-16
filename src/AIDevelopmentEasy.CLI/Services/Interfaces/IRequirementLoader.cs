using AIDevelopmentEasy.CLI.Models;
using AIDevelopmentEasy.Core.Models;

namespace AIDevelopmentEasy.CLI.Services.Interfaces;

/// <summary>
/// Requirement file loading interface
/// </summary>
public interface IRequirementLoader
{
    /// <summary>
    /// Gets all requirements with their status information
    /// </summary>
    IReadOnlyList<RequirementInfo> GetAllRequirements();
    
    /// <summary>
    /// Refreshes status of all requirements
    /// </summary>
    void RefreshStatuses();
    
    /// <summary>
    /// Checks if any requirements exist
    /// </summary>
    bool HasRequirements { get; }
    
    /// <summary>
    /// Loads a single project requirement content
    /// </summary>
    Task<string> LoadSingleProjectRequirementAsync(string filePath);
    
    /// <summary>
    /// Loads a multi-project requirement
    /// </summary>
    Task<MultiProjectRequirement?> LoadMultiProjectRequirementAsync(string filePath);
}
