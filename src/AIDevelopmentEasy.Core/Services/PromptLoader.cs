namespace AIDevelopmentEasy.Core.Services;

/// <summary>
/// Loads agent prompts from external files for easy editing
/// </summary>
public class PromptLoader
{
    private readonly string _promptsDirectory;
    private readonly Dictionary<string, string> _promptCache = new();
    private static PromptLoader? _instance;
    private static readonly object _lock = new();

    /// <summary>
    /// Gets the singleton instance of PromptLoader
    /// </summary>
    public static PromptLoader Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new PromptLoader();
                }
            }
            return _instance;
        }
    }

    /// <summary>
    /// Initialize with custom prompts directory
    /// </summary>
    public static void Initialize(string promptsDirectory)
    {
        lock (_lock)
        {
            _instance = new PromptLoader(promptsDirectory);
        }
    }

    private PromptLoader() : this(FindPromptsDirectory())
    {
    }

    private PromptLoader(string promptsDirectory)
    {
        _promptsDirectory = promptsDirectory;

        if (!Directory.Exists(_promptsDirectory))
        {
            Directory.CreateDirectory(_promptsDirectory);
        }
    }

    /// <summary>
    /// Load a prompt by name (without extension). Throws if file does not exist (no fallback).
    /// </summary>
    /// <param name="promptName">Name of the prompt file (e.g., "planner", "coder-csharp")</param>
    /// <returns>The prompt content</returns>
    /// <exception cref="FileNotFoundException">When the prompt file is missing</exception>
    public string LoadPromptRequired(string promptName)
    {
        if (_promptCache.TryGetValue(promptName, out var cached))
            return cached;

        var filePath = Path.Combine(_promptsDirectory, $"{promptName}.md");
        if (!File.Exists(filePath))
            filePath = Path.Combine(_promptsDirectory, promptName);
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Prompt file not found: {promptName}.md in {_promptsDirectory}");

        var content = File.ReadAllText(filePath);
        content = RemoveMarkdownTitle(content);
        _promptCache[promptName] = content;
        return content;
    }

    /// <summary>
    /// Load a prompt with variable substitution. Throws if file does not exist.
    /// </summary>
    /// <param name="promptName">Name of the prompt file</param>
    /// <param name="variables">Variables to substitute (e.g., {{REQUIREMENT}} -> "..." )</param>
    /// <returns>The prompt content with variables replaced</returns>
    public string LoadPromptRequired(string promptName, Dictionary<string, string> variables)
    {
        var prompt = LoadPromptRequired(promptName);
        foreach (var (key, value) in variables)
            prompt = prompt.Replace($"{{{{{key}}}}}", value ?? "");
        return prompt;
    }

    /// <summary>
    /// Load a prompt by name. Throws if file does not exist (no fallback).
    /// </summary>
    public string LoadPrompt(string promptName) => LoadPromptRequired(promptName);

    /// <summary>
    /// Clear the prompt cache (useful for hot-reloading)
    /// </summary>
    public void ClearCache()
    {
        _promptCache.Clear();
    }

    /// <summary>
    /// Check if a prompt file exists
    /// </summary>
    public bool PromptExists(string promptName)
    {
        var filePath = Path.Combine(_promptsDirectory, $"{promptName}.md");
        return File.Exists(filePath);
    }

    /// <summary>
    /// Get the prompts directory path
    /// </summary>
    public string PromptsDirectory => _promptsDirectory;

    private static string RemoveMarkdownTitle(string content)
    {
        var lines = content.Split('\n');
        if (lines.Length > 0 && lines[0].TrimStart().StartsWith('#'))
        {
            // Skip the first line (title) and any following empty lines
            var startIndex = 1;
            while (startIndex < lines.Length && string.IsNullOrWhiteSpace(lines[startIndex]))
            {
                startIndex++;
            }
            return string.Join('\n', lines.Skip(startIndex));
        }
        return content;
    }

    private static string FindPromptsDirectory()
    {
        // Try to find prompts directory by looking up from base directory
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir != null)
        {
            var promptsPath = Path.Combine(dir.FullName, "prompts");
            if (Directory.Exists(promptsPath))
            {
                return promptsPath;
            }

            // Also check if we're at solution root (has .sln file)
            if (dir.GetFiles("*.sln").Length > 0)
            {
                promptsPath = Path.Combine(dir.FullName, "prompts");
                return promptsPath; // Return even if doesn't exist yet
            }

            dir = dir.Parent;
        }

        // Fallback to base directory
        return Path.Combine(AppContext.BaseDirectory, "prompts");
    }
}
