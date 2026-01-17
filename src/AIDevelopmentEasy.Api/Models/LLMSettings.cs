namespace AIDevelopmentEasy.Api.Models;

/// <summary>
/// LLM configuration settings for token limits and cost tracking
/// </summary>
public class LLMSettings
{
    /// <summary>
    /// Maximum tokens allowed for input prompt (default: 8000)
    /// 1 token ≈ 4 characters for English text
    /// </summary>
    public int MaxPromptTokens { get; set; } = 8000;

    /// <summary>
    /// Maximum tokens allowed for LLM response (default: 4000)
    /// </summary>
    public int MaxCompletionTokens { get; set; } = 4000;

    /// <summary>
    /// Whether to show prompt info before LLM calls (default: true)
    /// </summary>
    public bool ShowPromptInfo { get; set; } = true;

    /// <summary>
    /// Estimated cost per 1K input tokens in USD (default: $0.01)
    /// </summary>
    public decimal EstimatedCostPer1KInputTokens { get; set; } = 0.01m;

    /// <summary>
    /// Estimated cost per 1K output tokens in USD (default: $0.03)
    /// </summary>
    public decimal EstimatedCostPer1KOutputTokens { get; set; } = 0.03m;
}

/// <summary>
/// Information about a single LLM call
/// </summary>
public class LLMCallInfo
{
    public string AgentName { get; set; } = string.Empty;
    public string Phase { get; set; } = string.Empty;
    public int EstimatedInputTokens { get; set; }
    public int EstimatedInputKB { get; set; }
    public int MaxCompletionTokens { get; set; }
    public decimal EstimatedCostUSD { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Result of an LLM call including actual token usage
/// </summary>
public class LLMCallResult
{
    public string AgentName { get; set; } = string.Empty;
    public string Phase { get; set; } = string.Empty;
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int TotalTokens { get; set; }
    public decimal ActualCostUSD { get; set; }
    public TimeSpan Duration { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Utility class for token estimation
/// </summary>
public static class TokenEstimator
{
    /// <summary>
    /// Estimate token count from text
    /// Rule of thumb: 1 token ≈ 4 characters for English
    /// For code, it's approximately 1 token per 3.5-4 characters
    /// </summary>
    public static int EstimateTokens(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        
        // Use a conservative estimate: 1 token per 3.5 characters
        return (int)Math.Ceiling(text.Length / 3.5);
    }

    /// <summary>
    /// Convert tokens to approximate KB
    /// </summary>
    public static int TokensToKB(int tokens)
    {
        // 1 token ≈ 4 bytes, 1 KB = 1024 bytes
        return (int)Math.Ceiling(tokens * 4.0 / 1024);
    }

    /// <summary>
    /// Convert KB to approximate tokens
    /// </summary>
    public static int KBToTokens(int kb)
    {
        // 1 KB ≈ 256 tokens (1024 bytes / 4 bytes per token)
        return kb * 256;
    }

    /// <summary>
    /// Calculate estimated cost
    /// </summary>
    public static decimal CalculateCost(int inputTokens, int outputTokens, decimal costPer1KInput, decimal costPer1KOutput)
    {
        var inputCost = (inputTokens / 1000m) * costPer1KInput;
        var outputCost = (outputTokens / 1000m) * costPer1KOutput;
        return Math.Round(inputCost + outputCost, 6);
    }
}
