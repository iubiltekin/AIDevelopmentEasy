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

    // Static settings that can be updated at runtime
    private static int _maxPromptTokens = 8000;
    private static int _maxCompletionTokens = 4000;
    private static bool _showPromptInfo = true;
    private static decimal _costPer1KInput = 0.01m;
    private static decimal _costPer1KOutput = 0.03m;

    /// <summary>
    /// Callback to record LLM call statistics (set by API layer)
    /// </summary>
    public static Action<LLMCallInfo>? OnLLMCallCompleted { get; set; }

    /// <summary>
    /// Update LLM settings at runtime (called from API when settings change)
    /// </summary>
    public static void UpdateLLMSettings(int maxPromptTokens, int maxCompletionTokens, bool showPromptInfo, decimal costPer1KInput, decimal costPer1KOutput)
    {
        _maxPromptTokens = maxPromptTokens;
        _maxCompletionTokens = maxCompletionTokens;
        _showPromptInfo = showPromptInfo;
        _costPer1KInput = costPer1KInput;
        _costPer1KOutput = costPer1KOutput;
    }

    /// <summary>
    /// Estimate token count from text (1 token ≈ 3.5 characters)
    /// </summary>
    protected static int EstimateTokens(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        return (int)Math.Ceiling(text.Length / 3.5);
    }

    /// <summary>
    /// Calculate estimated cost
    /// </summary>
    protected static decimal CalculateCost(int inputTokens, int outputTokens)
    {
        var inputCost = (inputTokens / 1000m) * _costPer1KInput;
        var outputCost = (outputTokens / 1000m) * _costPer1KOutput;
        return Math.Round(inputCost + outputCost, 6);
    }

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
        var startTime = DateTime.UtcNow;
        
        // Estimate input tokens
        var estimatedInputTokens = EstimateTokens(systemPrompt) + EstimateTokens(userPrompt);
        var effectiveMaxTokens = Math.Min(maxTokens, _maxCompletionTokens);
        var estimatedCost = CalculateCost(estimatedInputTokens, effectiveMaxTokens / 2); // Assume ~50% completion

        // Log prompt info if enabled
        if (_showPromptInfo)
        {
            var estimatedKB = (int)Math.Ceiling(estimatedInputTokens * 4.0 / 1024);
            _logger?.LogInformation("[{Agent}] 🤖 LLM Call Starting | Est. Input: ~{Tokens:N0} tokens (~{KB} KB) | Max Output: {MaxTokens:N0} | Est. Cost: ${Cost:F4}",
                Name, estimatedInputTokens, estimatedKB, effectiveMaxTokens, estimatedCost);
        }

        // Check if prompt exceeds limit
        if (estimatedInputTokens > _maxPromptTokens)
        {
            _logger?.LogWarning("[{Agent}] ⚠️ Prompt size ({Tokens:N0} tokens) exceeds limit ({Limit:N0}). Proceeding anyway but may be truncated.",
                Name, estimatedInputTokens, _maxPromptTokens);
        }

        var options = new ChatCompletionsOptions
        {
            DeploymentName = _deploymentName,
            Messages =
            {
                new ChatRequestSystemMessage(systemPrompt),
                new ChatRequestUserMessage(userPrompt)
            },
            Temperature = temperature,
            MaxTokens = effectiveMaxTokens
        };

        var response = await _openAIClient.GetChatCompletionsAsync(options, cancellationToken);
        var content = response.Value.Choices[0].Message.Content;
        var promptTokens = response.Value.Usage.PromptTokens;
        var completionTokens = response.Value.Usage.CompletionTokens;
        var totalTokens = response.Value.Usage.TotalTokens;
        var duration = DateTime.UtcNow - startTime;

        // Calculate actual cost
        var actualCost = CalculateCost(promptTokens, completionTokens);

        // Log completion info
        if (_showPromptInfo)
        {
            _logger?.LogInformation("[{Agent}] ✅ LLM Complete | Total: {Total:N0} tokens (in:{In:N0}, out:{Out:N0}) | Cost: ${Cost:F4} | Duration: {Duration:F1}s",
                Name, totalTokens, promptTokens, completionTokens, actualCost, duration.TotalSeconds);
        }

        // Log raw LLM request/response for debugging
        _logger?.LogDebug("[{Agent}] 📤 LLM REQUEST - System Prompt:\n{SystemPrompt}", Name, systemPrompt);
        _logger?.LogDebug("[{Agent}] 📤 LLM REQUEST - User Prompt:\n{UserPrompt}", Name, userPrompt);
        _logger?.LogDebug("[{Agent}] 📥 LLM RESPONSE (raw):\n{Response}", Name, content);

        // Record stats via callback (if registered)
        OnLLMCallCompleted?.Invoke(new LLMCallInfo
        {
            AgentName = Name,
            PromptTokens = promptTokens,
            CompletionTokens = completionTokens,
            TotalTokens = totalTokens,
            ActualCostUSD = actualCost,
            Duration = duration,
            Timestamp = DateTime.UtcNow
        });

        return (content, totalTokens);
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
        var startTime = DateTime.UtcNow;
        var effectiveMaxTokens = Math.Min(maxTokens, _maxCompletionTokens);
        
        var options = new ChatCompletionsOptions
        {
            DeploymentName = _deploymentName,
            Temperature = temperature,
            MaxTokens = effectiveMaxTokens
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
        var promptTokens = response.Value.Usage.PromptTokens;
        var completionTokens = response.Value.Usage.CompletionTokens;
        var totalTokens = response.Value.Usage.TotalTokens;
        var duration = DateTime.UtcNow - startTime;
        var actualCost = CalculateCost(promptTokens, completionTokens);

        // Log completion info
        if (_showPromptInfo)
        {
            _logger?.LogInformation("[{Agent}] ✅ LLM Complete (history) | Total: {Total:N0} tokens (in:{In:N0}, out:{Out:N0}) | Cost: ${Cost:F4} | Duration: {Duration:F1}s",
                Name, totalTokens, promptTokens, completionTokens, actualCost, duration.TotalSeconds);
        }

        // Log raw LLM request/response for debugging
        _logger?.LogDebug("[{Agent}] 📤 LLM REQUEST (history) - System Prompt:\n{SystemPrompt}", Name, systemPrompt);
        _logger?.LogDebug("[{Agent}] 📤 LLM REQUEST (history) - Messages: {Count} messages", Name, messages.Count);
        _logger?.LogDebug("[{Agent}] 📥 LLM RESPONSE (raw):\n{Response}", Name, responseContent);

        // Record stats via callback (if registered)
        OnLLMCallCompleted?.Invoke(new LLMCallInfo
        {
            AgentName = Name,
            PromptTokens = promptTokens,
            CompletionTokens = completionTokens,
            TotalTokens = totalTokens,
            ActualCostUSD = actualCost,
            Duration = duration,
            Timestamp = DateTime.UtcNow
        });

        return (responseContent, totalTokens);
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

/// <summary>
/// Information about an LLM call for tracking purposes
/// </summary>
public class LLMCallInfo
{
    public string AgentName { get; set; } = "";
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int TotalTokens { get; set; }
    public decimal ActualCostUSD { get; set; }
    public TimeSpan Duration { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
