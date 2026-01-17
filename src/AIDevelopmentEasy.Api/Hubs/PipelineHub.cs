using AIDevelopmentEasy.Api.Models;
using Microsoft.AspNetCore.SignalR;

namespace AIDevelopmentEasy.Api.Hubs;

/// <summary>
/// SignalR Hub for real-time pipeline updates.
/// Clients can subscribe to specific storys or receive all updates.
/// </summary>
public class PipelineHub : Hub
{
    private readonly ILogger<PipelineHub> _logger;

    public PipelineHub(ILogger<PipelineHub> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Called when a client connects
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Called when a client disconnects
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Subscribe to updates for a specific story
    /// </summary>
    public async Task SubscribeToStory(string storyId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"story_{storyId}");
        _logger.LogInformation("Client {ConnectionId} subscribed to story: {StoryId}", 
            Context.ConnectionId, storyId);
    }

    /// <summary>
    /// Unsubscribe from a specific story
    /// </summary>
    public async Task UnsubscribeFromStory(string storyId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"story_{storyId}");
        _logger.LogInformation("Client {ConnectionId} unsubscribed from story: {StoryId}", 
            Context.ConnectionId, storyId);
    }

    /// <summary>
    /// Subscribe to all pipeline updates
    /// </summary>
    public async Task SubscribeToAll()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "all_updates");
        _logger.LogInformation("Client {ConnectionId} subscribed to all updates", Context.ConnectionId);
    }

    /// <summary>
    /// Unsubscribe from all updates
    /// </summary>
    public async Task UnsubscribeFromAll()
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "all_updates");
        _logger.LogInformation("Client {ConnectionId} unsubscribed from all updates", Context.ConnectionId);
    }
}

/// <summary>
/// Service for sending updates through the SignalR hub
/// </summary>
public interface IPipelineNotificationService
{
    Task NotifyPhaseStartedAsync(string storyId, PipelinePhase phase, string message);
    Task NotifyPhaseCompletedAsync(string storyId, PipelinePhase phase, string message, object? result = null);
    Task NotifyPhasePendingApprovalAsync(string storyId, PipelinePhase phase, string message, object? data = null);
    Task NotifyPhaseFailedAsync(string storyId, PipelinePhase phase, string error);
    Task NotifyProgressAsync(string storyId, string message, int? progress = null);
    Task NotifyPipelineCompletedAsync(string storyId, string outputPath);
    Task NotifyStoryListChangedAsync();
    
    // Retry and Fix Task notifications
    Task NotifyRetryRequiredAsync(string storyId, PipelinePhase failedPhase, RetryInfoDto retryInfo);
    Task NotifyFixTasksGeneratedAsync(string storyId, List<FixTaskDto> fixTasks);
    Task NotifyTestResultsAsync(string storyId, TestSummaryDto testSummary);
    Task NotifyRetryStartingAsync(string storyId, int attempt, int maxAttempts, PipelinePhase targetPhase);
    
    // LLM call notifications
    Task NotifyLLMCallStartingAsync(string storyId, LLMCallInfo callInfo);
    Task NotifyLLMCallCompletedAsync(string storyId, LLMCallResult result);
}

/// <summary>
/// Implementation of pipeline notification service using SignalR
/// </summary>
public class SignalRPipelineNotificationService : IPipelineNotificationService
{
    private readonly IHubContext<PipelineHub> _hubContext;
    private readonly ILogger<SignalRPipelineNotificationService> _logger;

    public SignalRPipelineNotificationService(
        IHubContext<PipelineHub> hubContext,
        ILogger<SignalRPipelineNotificationService> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task NotifyPhaseStartedAsync(string storyId, PipelinePhase phase, string message)
    {
        var update = CreateUpdate(storyId, "PhaseStarted", phase, message);
        await SendToStoryGroupAsync(storyId, "PipelineUpdate", update);
        _logger.LogInformation("[{StoryId}] Phase started: {Phase} - {Message}", storyId, phase, message);
    }

    public async Task NotifyPhaseCompletedAsync(string storyId, PipelinePhase phase, string message, object? result = null)
    {
        var update = CreateUpdate(storyId, "PhaseCompleted", phase, message, result);
        await SendToStoryGroupAsync(storyId, "PipelineUpdate", update);
        _logger.LogInformation("[{StoryId}] Phase completed: {Phase} - {Message}", storyId, phase, message);
    }

    public async Task NotifyPhasePendingApprovalAsync(string storyId, PipelinePhase phase, string message, object? data = null)
    {
        var update = CreateUpdate(storyId, "PhasePendingApproval", phase, message, data);
        await SendToStoryGroupAsync(storyId, "PipelineUpdate", update);
        _logger.LogInformation("[{StoryId}] Phase pending approval: {Phase} - {Message}", storyId, phase, message);
    }

    public async Task NotifyPhaseFailedAsync(string storyId, PipelinePhase phase, string error)
    {
        var update = CreateUpdate(storyId, "PhaseFailed", phase, error);
        await SendToStoryGroupAsync(storyId, "PipelineUpdate", update);
        _logger.LogError("[{StoryId}] Phase failed: {Phase} - {Error}", storyId, phase, error);
    }

    public async Task NotifyProgressAsync(string storyId, string message, int? progress = null)
    {
        var update = new PipelineUpdateMessage
        {
            StoryId = storyId,
            UpdateType = "Progress",
            Phase = PipelinePhase.None,
            Message = message,
            Data = progress.HasValue ? new { Progress = progress.Value } : null
        };
        await SendToStoryGroupAsync(storyId, "PipelineUpdate", update);
    }

    public async Task NotifyPipelineCompletedAsync(string storyId, string outputPath)
    {
        var update = CreateUpdate(storyId, "PipelineCompleted", PipelinePhase.Completed, 
            "Pipeline completed successfully", new { OutputPath = outputPath });
        await SendToStoryGroupAsync(storyId, "PipelineUpdate", update);
        await _hubContext.Clients.Group("all_updates").SendAsync("StoryCompleted", storyId);
        _logger.LogInformation("[{StoryId}] Pipeline completed. Output: {OutputPath}", storyId, outputPath);
    }

    public async Task NotifyStoryListChangedAsync()
    {
        await _hubContext.Clients.Group("all_updates").SendAsync("StoryListChanged");
    }

    public async Task NotifyRetryRequiredAsync(string storyId, PipelinePhase failedPhase, RetryInfoDto retryInfo)
    {
        var update = CreateUpdate(storyId, "RetryRequired", failedPhase, 
            $"Retry required: {retryInfo.Reason}. Attempt {retryInfo.CurrentAttempt}/{retryInfo.MaxAttempts}",
            retryInfo);
        await SendToStoryGroupAsync(storyId, "PipelineUpdate", update);
        _logger.LogInformation("[{StoryId}] Retry required for phase {Phase}: {Reason}", 
            storyId, failedPhase, retryInfo.Reason);
    }

    public async Task NotifyFixTasksGeneratedAsync(string storyId, List<FixTaskDto> fixTasks)
    {
        var update = new PipelineUpdateMessage
        {
            StoryId = storyId,
            UpdateType = "FixTasksGenerated",
            Phase = PipelinePhase.None,
            Message = $"{fixTasks.Count} fix task(s) generated",
            Data = fixTasks
        };
        await SendToStoryGroupAsync(storyId, "PipelineUpdate", update);
        _logger.LogInformation("[{StoryId}] {Count} fix tasks generated", storyId, fixTasks.Count);
    }

    public async Task NotifyTestResultsAsync(string storyId, TestSummaryDto testSummary)
    {
        var update = new PipelineUpdateMessage
        {
            StoryId = storyId,
            UpdateType = "TestResults",
            Phase = PipelinePhase.UnitTesting,
            Message = $"Tests: {testSummary.Passed}/{testSummary.TotalTests} passed" +
                     (testSummary.IsBreakingChange ? " ⚠️ BREAKING CHANGE!" : ""),
            Data = testSummary
        };
        await SendToStoryGroupAsync(storyId, "PipelineUpdate", update);
        _logger.LogInformation("[{StoryId}] Test results: {Passed}/{Total} passed, {Failed} failed", 
            storyId, testSummary.Passed, testSummary.TotalTests, testSummary.Failed);
    }

    public async Task NotifyRetryStartingAsync(string storyId, int attempt, int maxAttempts, PipelinePhase targetPhase)
    {
        var update = new PipelineUpdateMessage
        {
            StoryId = storyId,
            UpdateType = "RetryStarting",
            Phase = targetPhase,
            Message = $"Starting retry attempt {attempt}/{maxAttempts}, returning to {targetPhase}",
            Data = new { Attempt = attempt, MaxAttempts = maxAttempts, TargetPhase = targetPhase }
        };
        await SendToStoryGroupAsync(storyId, "PipelineUpdate", update);
        _logger.LogInformation("[{StoryId}] Starting retry attempt {Attempt}/{Max} → {Phase}", 
            storyId, attempt, maxAttempts, targetPhase);
    }

    public async Task NotifyLLMCallStartingAsync(string storyId, LLMCallInfo callInfo)
    {
        var costStr = callInfo.EstimatedCostUSD > 0 
            ? $", Est. Cost: ${callInfo.EstimatedCostUSD:F4}" 
            : "";
        
        var update = new PipelineUpdateMessage
        {
            StoryId = storyId,
            UpdateType = "LLMCallStarting",
            Phase = PipelinePhase.None,
            Message = $"🤖 LLM Call: {callInfo.AgentName} | ~{callInfo.EstimatedInputTokens:N0} tokens (~{callInfo.EstimatedInputKB} KB){costStr}",
            Data = callInfo
        };
        await SendToStoryGroupAsync(storyId, "PipelineUpdate", update);
        _logger.LogInformation("[{StoryId}] LLM Call Starting: {Agent} | ~{Tokens} tokens (~{KB} KB){Cost}", 
            storyId, callInfo.AgentName, callInfo.EstimatedInputTokens, callInfo.EstimatedInputKB, costStr);
    }

    public async Task NotifyLLMCallCompletedAsync(string storyId, LLMCallResult result)
    {
        var update = new PipelineUpdateMessage
        {
            StoryId = storyId,
            UpdateType = "LLMCallCompleted",
            Phase = PipelinePhase.None,
            Message = $"✅ LLM Complete: {result.AgentName} | {result.TotalTokens:N0} tokens (in:{result.PromptTokens:N0}, out:{result.CompletionTokens:N0}) | Cost: ${result.ActualCostUSD:F4} | {result.Duration.TotalSeconds:F1}s",
            Data = result
        };
        await SendToStoryGroupAsync(storyId, "PipelineUpdate", update);
        _logger.LogInformation("[{StoryId}] LLM Complete: {Agent} | Total: {Total} tokens | Cost: ${Cost:F4} | Duration: {Duration}s", 
            storyId, result.AgentName, result.TotalTokens, result.ActualCostUSD, result.Duration.TotalSeconds);
    }

    private PipelineUpdateMessage CreateUpdate(string storyId, string updateType, PipelinePhase phase, string message, object? data = null)
    {
        return new PipelineUpdateMessage
        {
            StoryId = storyId,
            UpdateType = updateType,
            Phase = phase,
            Message = message,
            Data = data
        };
    }

    private async Task SendToStoryGroupAsync(string storyId, string method, object message)
    {
        // Send to specific story subscribers
        await _hubContext.Clients.Group($"story_{storyId}").SendAsync(method, message);
        
        // Also send to "all updates" subscribers
        await _hubContext.Clients.Group("all_updates").SendAsync(method, message);
    }
}
