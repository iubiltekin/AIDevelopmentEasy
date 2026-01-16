using System.Text.Json;
using AIDevelopmentEasy.CLI.Configuration;
using AIDevelopmentEasy.CLI.Models;
using AIDevelopmentEasy.CLI.Services.Interfaces;
using AIDevelopmentEasy.Core.Models;

namespace AIDevelopmentEasy.CLI.Services;

/// <summary>
/// Loads requirement files from the requirements directory
/// </summary>
public class RequirementLoader : IRequirementLoader
{
    private readonly ResolvedPaths _paths;
    private readonly List<RequirementInfo> _requirements;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

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

    public async Task<string> LoadSingleProjectRequirementAsync(string filePath)
    {
        return await File.ReadAllTextAsync(filePath);
    }

    public async Task<MultiProjectRequirement?> LoadMultiProjectRequirementAsync(string filePath)
    {
        try
        {
            var json = await File.ReadAllTextAsync(filePath);
            return JsonSerializer.Deserialize<MultiProjectRequirement>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private List<RequirementInfo> LoadAllRequirements()
    {
        var requirements = new List<RequirementInfo>();

        // Single project files (.txt, .md)
        var singleFiles = Directory.GetFiles(_paths.RequirementsPath, "*.txt")
            .Concat(Directory.GetFiles(_paths.RequirementsPath, "*.md"))
            .Where(f => !Path.GetFileName(f).StartsWith("_"))
            .OrderBy(f => Path.GetFileName(f));

        foreach (var file in singleFiles)
        {
            var name = Path.GetFileNameWithoutExtension(file);
            var workingDir = Path.Combine(_paths.RequirementsPath, name);
            
            var req = new RequirementInfo
            {
                FilePath = file,
                Type = RequirementType.Single,
                WorkingDirectory = workingDir
            };
            req.RefreshStatus();
            requirements.Add(req);
        }

        // Multi-project files (.json)
        var multiFiles = Directory.GetFiles(_paths.RequirementsPath, "*.json")
            .Where(f => !Path.GetFileName(f).StartsWith("_") && !Path.GetFileName(f).Contains("task-"))
            .OrderBy(f => Path.GetFileName(f));

        foreach (var file in multiFiles)
        {
            var name = Path.GetFileNameWithoutExtension(file);
            var workingDir = Path.Combine(_paths.RequirementsPath, name);
            
            var req = new RequirementInfo
            {
                FilePath = file,
                Type = RequirementType.Multi,
                WorkingDirectory = workingDir
            };
            req.RefreshStatus();
            requirements.Add(req);
        }

        return requirements;
    }
}
