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
/// Resolved paths based on ProgramData directory for persistent storage
/// </summary>
public class ResolvedPaths
{
    public string AppDataDirectory { get; }
    public string RequirementsPath { get; }
    public string OutputPath { get; }
    public string LogsPath { get; }
    public string PromptsPath { get; }
    public string CodebasesPath { get; }
    public string CodingStandardsPath { get; }
    public string? CodingStandards { get; private set; }

    public ResolvedPaths(AIDevelopmentEasySettings settings)
    {
        // Use ProgramData for all data directories (persistent storage)
        var programDataDir = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        AppDataDirectory = Path.Combine(programDataDir, "AIDevelopmentEasy");

        RequirementsPath = Path.Combine(AppDataDirectory, "requirements");
        OutputPath = Path.Combine(AppDataDirectory, "output");
        LogsPath = Path.Combine(AppDataDirectory, "logs");
        PromptsPath = Path.Combine(AppDataDirectory, "prompts");
        CodebasesPath = Path.Combine(AppDataDirectory, "codebases");
        CodingStandardsPath = ResolvePath(settings.CodingStandardsFile, AppContext.BaseDirectory);

        // Ensure directories exist
        Directory.CreateDirectory(AppDataDirectory);
        Directory.CreateDirectory(RequirementsPath);
        Directory.CreateDirectory(OutputPath);
        Directory.CreateDirectory(LogsPath);
        Directory.CreateDirectory(PromptsPath);
        Directory.CreateDirectory(CodebasesPath);

        // Copy default prompts from app directory if prompts directory is empty
        CopyDefaultPromptsIfNeeded();

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

    private void CopyDefaultPromptsIfNeeded()
    {
        var appPromptsDir = Path.Combine(AppContext.BaseDirectory, "prompts");
        if (Directory.Exists(appPromptsDir) && !Directory.EnumerateFiles(PromptsPath, "*.md").Any())
        {
            foreach (var file in Directory.GetFiles(appPromptsDir, "*.md"))
            {
                var destFile = Path.Combine(PromptsPath, Path.GetFileName(file));
                if (!File.Exists(destFile))
                {
                    File.Copy(file, destFile);
                }
            }
        }
    }

    private static string ResolvePath(string path, string basePath)
    {
        return Path.IsPathRooted(path) ? path : Path.Combine(basePath, path);
    }
}
