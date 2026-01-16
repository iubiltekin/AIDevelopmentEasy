using AIDevelopmentEasy.CLI.Models;

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
    /// Loads requirement content from file
    /// </summary>
    Task<string> LoadRequirementAsync(string filePath);
}
