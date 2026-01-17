using AIDevelopmentEasy.CLI.Configuration;
using AIDevelopmentEasy.CLI.Models;
using AIDevelopmentEasy.CLI.Services.Interfaces;

namespace AIDevelopmentEasy.CLI.Services;

/// <summary>
/// Loads story files from the stories directory.
/// Supports .txt, .md, and .json files as story inputs.
/// </summary>
public class StoryLoader : IStoryLoader
{
    private readonly ResolvedPaths _paths;
    private readonly List<StoryInfo> _stories;

    public StoryLoader(ResolvedPaths paths)
    {
        _paths = paths;
        _stories = LoadAllStories();
    }

    public IReadOnlyList<StoryInfo> GetAllStories() => _stories;

    public void RefreshStatuses()
    {
        foreach (var story in _stories)
        {
            story.RefreshStatus();
        }
    }

    public bool HasStories => _stories.Count > 0;

    public async Task<string> LoadStoryAsync(string filePath)
    {
        return await File.ReadAllTextAsync(filePath);
    }

    private List<StoryInfo> LoadAllStories()
    {
        var stories = new List<StoryInfo>();

        // Support .txt, .md, and .json files
        var files = Directory.GetFiles(_paths.StoriesPath, "*.txt")
            .Concat(Directory.GetFiles(_paths.StoriesPath, "*.md"))
            .Concat(Directory.GetFiles(_paths.StoriesPath, "*.json"))
            .Where(f => !Path.GetFileName(f).StartsWith("_") && !Path.GetFileName(f).Contains("task-"))
            .OrderBy(f => Path.GetFileName(f));

        foreach (var file in files)
        {
            var name = Path.GetFileNameWithoutExtension(file);
            var workingDir = Path.Combine(_paths.StoriesPath, name);
            
            var story = new StoryInfo
            {
                FilePath = file,
                WorkingDirectory = workingDir
            };
            story.RefreshStatus();
            stories.Add(story);
        }

        return stories;
    }
}
