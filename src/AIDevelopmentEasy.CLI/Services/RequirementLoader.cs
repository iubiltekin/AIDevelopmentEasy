using AIDevelopmentEasy.CLI.Configuration;
using AIDevelopmentEasy.CLI.Models;
using AIDevelopmentEasy.CLI.Services.Interfaces;

namespace AIDevelopmentEasy.CLI.Services;

/// <summary>
/// Loads requirement files from the requirements directory.
/// Supports .txt, .md, and .json files as requirement inputs.
/// </summary>
public class RequirementLoader : IRequirementLoader
{
    private readonly ResolvedPaths _paths;
    private readonly List<RequirementInfo> _requirements;

    public RequirementLoader(ResolvedPaths paths)
    {
        _paths = paths;
        _requirements = LoadAllRequirements();
    }

    public IReadOnlyList<RequirementInfo> GetAllRequirements() => _requirements;

    public void RefreshStatuses()
    {
        foreach (var req in _requirements)
        {
            req.RefreshStatus();
        }
    }

    public bool HasRequirements => _requirements.Count > 0;

    public async Task<string> LoadRequirementAsync(string filePath)
    {
        return await File.ReadAllTextAsync(filePath);
    }

    private List<RequirementInfo> LoadAllRequirements()
    {
        var requirements = new List<RequirementInfo>();

        // Support .txt, .md, and .json files
        var files = Directory.GetFiles(_paths.RequirementsPath, "*.txt")
            .Concat(Directory.GetFiles(_paths.RequirementsPath, "*.md"))
            .Concat(Directory.GetFiles(_paths.RequirementsPath, "*.json"))
            .Where(f => !Path.GetFileName(f).StartsWith("_") && !Path.GetFileName(f).Contains("task-"))
            .OrderBy(f => Path.GetFileName(f));

        foreach (var file in files)
        {
            var name = Path.GetFileNameWithoutExtension(file);
            var workingDir = Path.Combine(_paths.RequirementsPath, name);
            
            var req = new RequirementInfo
            {
                FilePath = file,
                WorkingDirectory = workingDir
            };
            req.RefreshStatus();
            requirements.Add(req);
        }

        return requirements;
    }
}
