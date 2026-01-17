using AIDevelopmentEasy.Api.Models;
using AIDevelopmentEasy.Core.Agents.Base;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

// Alias for the Core's LLMCallInfo
using CoreLLMCallInfo = AIDevelopmentEasy.Core.Agents.Base.LLMCallInfo;

namespace AIDevelopmentEasy.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SettingsController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SettingsController> _logger;
    private readonly LLMSettings _llmSettings;
    private static LLMSettings? _runtimeSettings;

    public SettingsController(
        IConfiguration configuration,
        ILogger<SettingsController> logger,
        IOptions<LLMSettings> llmSettings)
    {
        _configuration = configuration;
        _logger = logger;
        _llmSettings = llmSettings.Value;
    }

    /// <summary>
    /// Get current LLM settings
    /// </summary>
    [HttpGet("llm")]
    public ActionResult<LLMSettings> GetLLMSettings()
    {
        // Return runtime settings if available, otherwise return configured settings
        var settings = _runtimeSettings ?? _llmSettings;
        return Ok(settings);
    }

    /// <summary>
    /// Update LLM settings (runtime only - does not persist to file)
    /// </summary>
    [HttpPut("llm")]
    public ActionResult<LLMSettings> UpdateLLMSettings([FromBody] LLMSettings settings)
    {
        if (settings.MaxPromptTokens < 100 || settings.MaxPromptTokens > 128000)
        {
            return BadRequest("MaxPromptTokens must be between 100 and 128000");
        }

        if (settings.MaxCompletionTokens < 100 || settings.MaxCompletionTokens > 16000)
        {
            return BadRequest("MaxCompletionTokens must be between 100 and 16000");
        }

        // Store in runtime (does not persist to appsettings.json)
        _runtimeSettings = settings;

        // Update BaseAgent static settings so all agents use new limits
        BaseAgent.UpdateLLMSettings(
            settings.MaxPromptTokens,
            settings.MaxCompletionTokens,
            settings.ShowPromptInfo,
            settings.EstimatedCostPer1KInputTokens,
            settings.EstimatedCostPer1KOutputTokens);

        _logger.LogInformation("LLM Settings updated: MaxPromptTokens={Max}, MaxCompletionTokens={MaxOut}, ShowPromptInfo={Show}",
            settings.MaxPromptTokens, settings.MaxCompletionTokens, settings.ShowPromptInfo);

        return Ok(settings);
    }

    /// <summary>
    /// Get current LLM usage statistics
    /// </summary>
    [HttpGet("llm/stats")]
    public ActionResult<LLMUsageStats> GetLLMStats()
    {
        // Return stats from the singleton tracker
        return Ok(LLMUsageTracker.Instance.GetStats());
    }

    /// <summary>
    /// Reset LLM usage statistics
    /// </summary>
    [HttpPost("llm/stats/reset")]
    public ActionResult ResetLLMStats()
    {
        LLMUsageTracker.Instance.Reset();
        return Ok(new { message = "LLM usage statistics reset" });
    }

    /// <summary>
    /// Get runtime LLM settings (used by agents)
    /// </summary>
    public static LLMSettings GetRuntimeSettings(LLMSettings configuredSettings)
    {
        return _runtimeSettings ?? configuredSettings;
    }
}

/// <summary>
/// Singleton tracker for LLM usage statistics
/// </summary>
public class LLMUsageTracker
{
    private static readonly Lazy<LLMUsageTracker> _instance = new(() => new LLMUsageTracker());
    public static LLMUsageTracker Instance => _instance.Value;

    private readonly object _lock = new();
    private int _totalCalls;
    private int _totalPromptTokens;
    private int _totalCompletionTokens;
    private decimal _totalCost;
    private DateTime _sessionStart = DateTime.UtcNow;
    private readonly List<LLMCallResult> _recentCalls = new();

    private LLMUsageTracker() 
    {
        // Register callback with BaseAgent to receive LLM call stats
        BaseAgent.OnLLMCallCompleted = OnLLMCallCompleted;
    }

    /// <summary>
    /// Callback handler for LLM calls from BaseAgent
    /// </summary>
    private void OnLLMCallCompleted(CoreLLMCallInfo info)
    {
        var result = new LLMCallResult
        {
            AgentName = info.AgentName,
            PromptTokens = info.PromptTokens,
            CompletionTokens = info.CompletionTokens,
            TotalTokens = info.TotalTokens,
            ActualCostUSD = info.ActualCostUSD,
            Duration = info.Duration,
            Timestamp = info.Timestamp
        };
        RecordCall(result);
    }

    public void RecordCall(LLMCallResult result)
    {
        lock (_lock)
        {
            _totalCalls++;
            _totalPromptTokens += result.PromptTokens;
            _totalCompletionTokens += result.CompletionTokens;
            _totalCost += result.ActualCostUSD;

            _recentCalls.Add(result);
            if (_recentCalls.Count > 100)
            {
                _recentCalls.RemoveAt(0);
            }
        }
    }

    public LLMUsageStats GetStats()
    {
        lock (_lock)
        {
            return new LLMUsageStats
            {
                TotalCalls = _totalCalls,
                TotalPromptTokens = _totalPromptTokens,
                TotalCompletionTokens = _totalCompletionTokens,
                TotalTokens = _totalPromptTokens + _totalCompletionTokens,
                TotalCostUSD = _totalCost,
                SessionStart = _sessionStart,
                SessionDuration = DateTime.UtcNow - _sessionStart,
                RecentCalls = _recentCalls.TakeLast(10).ToList()
            };
        }
    }

    public void Reset()
    {
        lock (_lock)
        {
            _totalCalls = 0;
            _totalPromptTokens = 0;
            _totalCompletionTokens = 0;
            _totalCost = 0;
            _sessionStart = DateTime.UtcNow;
            _recentCalls.Clear();
        }
    }
}

/// <summary>
/// LLM usage statistics
/// </summary>
public class LLMUsageStats
{
    public int TotalCalls { get; set; }
    public int TotalPromptTokens { get; set; }
    public int TotalCompletionTokens { get; set; }
    public int TotalTokens { get; set; }
    public decimal TotalCostUSD { get; set; }
    public DateTime SessionStart { get; set; }
    public TimeSpan SessionDuration { get; set; }
    public List<LLMCallResult> RecentCalls { get; set; } = new();
}

/// <summary>
/// Result of a single LLM call
/// </summary>
public class LLMCallResult
{
    public string AgentName { get; set; } = "";
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int TotalTokens { get; set; }
    public decimal ActualCostUSD { get; set; }
    public TimeSpan Duration { get; set; }
    public DateTime Timestamp { get; set; }
}
