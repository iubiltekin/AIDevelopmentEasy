using AIDevelopmentEasy.Core.Services;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Logging;

namespace AIDevelopmentEasy.Core.Agents.Base;

/// <summary>
/// Base class for all AIDevelopmentEasy agents.
/// Provides common LLM interaction and logging functionality.
/// </summary>
public abstract class BaseAgent : IAgent
{
    protected readonly OpenAIClient _openAIClient;
    protected readonly ILogger? _logger;
    protected readonly string _deploymentName;

    public abstract string Name { get; }
    public abstract string Role { get; }
    
    /// <summary>
    /// The name of the prompt file (without extension) in the prompts/ directory
    /// </summary>
    protected virtual string? PromptFileName => null;

    protected BaseAgent(OpenAIClient openAIClient, string deploymentName, ILogger? logger = null)
    {
        _openAIClient = openAIClient ?? throw new ArgumentNullException(nameof(openAIClient));
        _deploymentName = deploymentName;
        _logger = logger;
    }

    public abstract Task<AgentResponse> RunAsync(AgentRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the system prompt for this agent's role.
    /// First tries to load from prompts/ directory, then falls back to hardcoded.
    /// </summary>
    protected virtual string GetSystemPrompt()
    {
        // Try to load from file if PromptFileName is specified
        if (!string.IsNullOrEmpty(PromptFileName))
        {
            try
            {
                return PromptLoader.Instance.LoadPrompt(PromptFileName, GetFallbackPrompt());
            }
            catch (FileNotFoundException)
            {
                _logger?.LogWarning("[{Agent}] Prompt file not found: {File}, using fallback", Name, PromptFileName);
            }
        }
        
        return GetFallbackPrompt();
    }
    
    /// <summary>
    /// Get the fallback hardcoded prompt (override in derived classes)
    /// </summary>
    protected virtual string GetFallbackPrompt() => $"You are a {Role}.";

    /// <summary>
    /// Call the LLM with given messages
    /// </summary>
    protected async Task<(string Content, int Tokens)> CallLLMAsync(
        string systemPrompt,
        string userPrompt,
        float temperature = 0.3f,
        int maxTokens = 4000,
        CancellationToken cancellationToken = default)
    {
        _logger?.LogDebug("[{Agent}] Calling LLM with prompt length: {Length}", Name, userPrompt.Length);

        var options = new ChatCompletionsOptions
        {
            DeploymentName = _deploymentName,
            Messages =
            {
                new ChatRequestSystemMessage(systemPrompt),
                new ChatRequestUserMessage(userPrompt)
            },
            Temperature = temperature,
            MaxTokens = maxTokens
        };

        var response = await _openAIClient.GetChatCompletionsAsync(options, cancellationToken);
        var content = response.Value.Choices[0].Message.Content;
        var tokens = response.Value.Usage.TotalTokens;

        _logger?.LogDebug("[{Agent}] LLM response received. Tokens: {Tokens}", Name, tokens);

        return (content, tokens);
    }

    /// <summary>
    /// Call LLM with conversation history
    /// </summary>
    protected async Task<(string Content, int Tokens)> CallLLMWithHistoryAsync(
        string systemPrompt,
        List<(string Role, string Content)> messages,
        float temperature = 0.3f,
        int maxTokens = 4000,
        CancellationToken cancellationToken = default)
    {
        var options = new ChatCompletionsOptions
        {
            DeploymentName = _deploymentName,
            Temperature = temperature,
            MaxTokens = maxTokens
        };

        options.Messages.Add(new ChatRequestSystemMessage(systemPrompt));
        
        foreach (var (role, content) in messages)
        {
            if (role == "user")
                options.Messages.Add(new ChatRequestUserMessage(content));
            else if (role == "assistant")
                options.Messages.Add(new ChatRequestAssistantMessage(content));
        }

        var response = await _openAIClient.GetChatCompletionsAsync(options, cancellationToken);
        var responseContent = response.Value.Choices[0].Message.Content;
        var tokens = response.Value.Usage.TotalTokens;

        return (responseContent, tokens);
    }

    /// <summary>
    /// Extract JSON from markdown code blocks
    /// </summary>
    protected string ExtractJson(string response)
    {
        if (response.Contains("```json"))
        {
            var start = response.IndexOf("```json", StringComparison.Ordinal) + 7;
            var end = response.IndexOf("```", start, StringComparison.Ordinal);
            if (end > start)
                return response.Substring(start, end - start).Trim();
        }
        else if (response.Contains("```"))
        {
            var start = response.IndexOf("```", StringComparison.Ordinal) + 3;
            var end = response.IndexOf("```", start, StringComparison.Ordinal);
            if (end > start)
                return response.Substring(start, end - start).Trim();
        }
        return response.Trim();
    }

    /// <summary>
    /// Extract code from markdown code blocks
    /// </summary>
    protected string ExtractCode(string response, string language = "")
    {
        var marker = string.IsNullOrEmpty(language) ? "```" : $"```{language}";
        
        if (response.Contains(marker))
        {
            var start = response.IndexOf(marker, StringComparison.Ordinal) + marker.Length;
            // Skip to next line if marker was followed by language
            if (!string.IsNullOrEmpty(language))
            {
                var newline = response.IndexOf('\n', start);
                if (newline > start)
                    start = newline + 1;
            }
            var end = response.IndexOf("```", start, StringComparison.Ordinal);
            if (end > start)
                return response.Substring(start, end - start).Trim();
        }
        else if (response.Contains("```"))
        {
            var start = response.IndexOf("```", StringComparison.Ordinal) + 3;
            var newline = response.IndexOf('\n', start);
            if (newline > start)
                start = newline + 1;
            var end = response.IndexOf("```", start, StringComparison.Ordinal);
            if (end > start)
                return response.Substring(start, end - start).Trim();
        }
        
        return response.Trim();
    }

    /// <summary>
    /// Log an action to project history
    /// </summary>
    protected void LogAction(ProjectState? state, string action, string input, string output)
    {
        state?.History.Add(new AgentAction
        {
            AgentName = Name,
            Action = action,
            Input = input.Length > 500 ? input[..500] + "..." : input,
            Output = output.Length > 500 ? output[..500] + "..." : output
        });
    }

    /// <summary>
    /// Build the full system prompt including coding standards if available
    /// </summary>
    protected string BuildSystemPromptWithStandards(ProjectState? state)
    {
        var basePrompt = GetSystemPrompt();
        
        if (state?.CodingStandards == null)
            return basePrompt;

        return $@"{basePrompt}

═══════════════════════════════════════════════════════════════════════════════
CODING STANDARDS (MUST FOLLOW)
═══════════════════════════════════════════════════════════════════════════════
{state.CodingStandards}

IMPORTANT: You MUST follow all coding standards above. Pay special attention to:
- Framework version and language constraints
- Testing framework (NUnit, not MSTest)
- Assertion library (FluentAssertions)
- Naming conventions
- Prohibited patterns and required patterns";
    }
}
