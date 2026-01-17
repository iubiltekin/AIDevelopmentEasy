using AIDevelopmentEasy.CLI.Models;

namespace AIDevelopmentEasy.CLI.Services.Interfaces;

/// <summary>
/// Story file loading interface
/// </summary>
public interface IStoryLoader
{
    /// <summary>
    /// Gets all stories with their status information
    /// </summary>
    IReadOnlyList<StoryInfo> GetAllStories();
    
    /// <summary>
    /// Refreshes status of all stories
    /// </summary>
    void RefreshStatuses();
    
    /// <summary>
    /// Checks if any stories exist
    /// </summary>
    bool HasStories { get; }
    
    /// <summary>
    /// Loads story content from file
    /// </summary>
    Task<string> LoadStoryAsync(string filePath);
}
