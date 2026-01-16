using AIDevelopmentEasy.Core.Services;

namespace AIDevelopmentEasy.CLI.Configuration;

/// <summary>
/// Strongly typed application settings
/// </summary>
public class AppSettings
{
    public AzureOpenAISettings AzureOpenAI { get; set; } = new();
    public AIDevelopmentEasySettings AIDevelopmentEasy { get; set; } = new();
}

public class AzureOpenAISettings
{
    public string Endpoint { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string DeploymentName { get; set; } = "gpt-4";
    public string ApiVersion { get; set; } = "2024-02-15-preview";

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Endpoint))
            throw new InvalidOperationException("AzureOpenAI:Endpoint not configured");
        if (string.IsNullOrWhiteSpace(ApiKey))
            throw new InvalidOperationException("AzureOpenAI:ApiKey not configured");
    }
}

public class AIDevelopmentEasySettings
{
    public string RequirementsDirectory { get; set; } = "requirements";
    public string OutputDirectory { get; set; } = "output";
    public string CodingStandardsFile { get; set; } = "coding-standards.json";
    public string TargetLanguage { get; set; } = "csharp";
    public int DebugMaxRetries { get; set; } = 3;
}

/// <summary>
/// Resolved paths based on solution directory
/// </summary>
public class ResolvedPaths
{
    public string SolutionDirectory { get; }
    public string RequirementsPath { get; }
    public string OutputPath { get; }
    public string LogsPath { get; }
    public string PromptsPath { get; }
    public string CodingStandardsPath { get; }
    public string? CodingStandards { get; private set; }

    public ResolvedPaths(AIDevelopmentEasySettings settings)
    {
        SolutionDirectory = FindSolutionDirectory();

        RequirementsPath = ResolvePath(settings.RequirementsDirectory, SolutionDirectory);
        OutputPath = ResolvePath(settings.OutputDirectory, SolutionDirectory);
        LogsPath = Path.Combine(SolutionDirectory, "logs");
        PromptsPath = Path.Combine(SolutionDirectory, "prompts");
        CodingStandardsPath = ResolvePath(settings.CodingStandardsFile, AppContext.BaseDirectory);

        // Ensure directories exist
        Directory.CreateDirectory(RequirementsPath);
        Directory.CreateDirectory(OutputPath);
        Directory.CreateDirectory(LogsPath);
        Directory.CreateDirectory(PromptsPath);
        
        // Initialize PromptLoader with correct prompts directory
        PromptLoader.Initialize(PromptsPath);
    }

    public async Task LoadCodingStandardsAsync()
    {
        if (File.Exists(CodingStandardsPath))
        {
            CodingStandards = await File.ReadAllTextAsync(CodingStandardsPath);
        }
    }

    private static string ResolvePath(string path, string basePath)
    {
        return Path.IsPathRooted(path) ? path : Path.Combine(basePath, path);
    }

    private static string FindSolutionDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (dir.GetFiles("*.sln").Length > 0)
                return dir.FullName;
            dir = dir.Parent;
        }
        return AppContext.BaseDirectory;
    }
}
