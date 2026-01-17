using AIDevelopmentEasy.Api.Models;
using Microsoft.AspNetCore.SignalR;

namespace AIDevelopmentEasy.Api.Hubs;

/// <summary>
/// SignalR Hub for real-time pipeline updates.
/// Clients can subscribe to specific requirements or receive all updates.
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
    /// Subscribe to updates for a specific requirement
    /// </summary>
    public async Task SubscribeToRequirement(string requirementId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"requirement_{requirementId}");
        _logger.LogInformation("Client {ConnectionId} subscribed to requirement: {RequirementId}", 
            Context.ConnectionId, requirementId);
    }

    /// <summary>
    /// Unsubscribe from a specific requirement
    /// </summary>
    public async Task UnsubscribeFromRequirement(string requirementId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"requirement_{requirementId}");
        _logger.LogInformation("Client {ConnectionId} unsubscribed from requirement: {RequirementId}", 
            Context.ConnectionId, requirementId);
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
    Task NotifyPhaseStartedAsync(string requirementId, PipelinePhase phase, string message);
    Task NotifyPhaseCompletedAsync(string requirementId, PipelinePhase phase, string message, object? result = null);
    Task NotifyPhasePendingApprovalAsync(string requirementId, PipelinePhase phase, string message, object? data = null);
    Task NotifyPhaseFailedAsync(string requirementId, PipelinePhase phase, string error);
    Task NotifyProgressAsync(string requirementId, string message, int? progress = null);
    Task NotifyPipelineCompletedAsync(string requirementId, string outputPath);
    Task NotifyRequirementListChangedAsync();
    
    // Retry and Fix Task notifications
    Task NotifyRetryRequiredAsync(string requirementId, PipelinePhase failedPhase, RetryInfoDto retryInfo);
    Task NotifyFixTasksGeneratedAsync(string requirementId, List<FixTaskDto> fixTasks);
    Task NotifyTestResultsAsync(string requirementId, TestSummaryDto testSummary);
    Task NotifyRetryStartingAsync(string requirementId, int attempt, int maxAttempts, PipelinePhase targetPhase);
    
    // LLM call notifications
    Task NotifyLLMCallStartingAsync(string requirementId, LLMCallInfo callInfo);
    Task NotifyLLMCallCompletedAsync(string requirementId, LLMCallResult result);
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

    public async Task NotifyPhaseStartedAsync(string requirementId, PipelinePhase phase, string message)
    {
        var update = CreateUpdate(requirementId, "PhaseStarted", phase, message);
        await SendToRequirementGroupAsync(requirementId, "PipelineUpdate", update);
        _logger.LogInformation("[{RequirementId}] Phase started: {Phase} - {Message}", requirementId, phase, message);
    }

    public async Task NotifyPhaseCompletedAsync(string requirementId, PipelinePhase phase, string message, object? result = null)
    {
        var update = CreateUpdate(requirementId, "PhaseCompleted", phase, message, result);
        await SendToRequirementGroupAsync(requirementId, "PipelineUpdate", update);
        _logger.LogInformation("[{RequirementId}] Phase completed: {Phase} - {Message}", requirementId, phase, message);
    }

    public async Task NotifyPhasePendingApprovalAsync(string requirementId, PipelinePhase phase, string message, object? data = null)
    {
        var update = CreateUpdate(requirementId, "PhasePendingApproval", phase, message, data);
        await SendToRequirementGroupAsync(requirementId, "PipelineUpdate", update);
        _logger.LogInformation("[{RequirementId}] Phase pending approval: {Phase} - {Message}", requirementId, phase, message);
    }

    public async Task NotifyPhaseFailedAsync(string requirementId, PipelinePhase phase, string error)
    {
        var update = CreateUpdate(requirementId, "PhaseFailed", phase, error);
        await SendToRequirementGroupAsync(requirementId, "PipelineUpdate", update);
        _logger.LogError("[{RequirementId}] Phase failed: {Phase} - {Error}", requirementId, phase, error);
    }

    public async Task NotifyProgressAsync(string requirementId, string message, int? progress = null)
    {
        var update = new PipelineUpdateMessage
        {
            RequirementId = requirementId,
            UpdateType = "Progress",
            Phase = PipelinePhase.None,
            Message = message,
            Data = progress.HasValue ? new { Progress = progress.Value } : null
        };
        await SendToRequirementGroupAsync(requirementId, "PipelineUpdate", update);
    }

    public async Task NotifyPipelineCompletedAsync(string requirementId, string outputPath)
    {
        var update = CreateUpdate(requirementId, "PipelineCompleted", PipelinePhase.Completed, 
            "Pipeline completed successfully", new { OutputPath = outputPath });
        await SendToRequirementGroupAsync(requirementId, "PipelineUpdate", update);
        await _hubContext.Clients.Group("all_updates").SendAsync("RequirementCompleted", requirementId);
        _logger.LogInformation("[{RequirementId}] Pipeline completed. Output: {OutputPath}", requirementId, outputPath);
    }

    public async Task NotifyRequirementListChangedAsync()
    {
        await _hubContext.Clients.Group("all_updates").SendAsync("RequirementListChanged");
    }

    public async Task NotifyRetryRequiredAsync(string requirementId, PipelinePhase failedPhase, RetryInfoDto retryInfo)
    {
        var update = CreateUpdate(requirementId, "RetryRequired", failedPhase, 
            $"Retry required: {retryInfo.Reason}. Attempt {retryInfo.CurrentAttempt}/{retryInfo.MaxAttempts}",
            retryInfo);
        await SendToRequirementGroupAsync(requirementId, "PipelineUpdate", update);
        _logger.LogInformation("[{RequirementId}] Retry required for phase {Phase}: {Reason}", 
            requirementId, failedPhase, retryInfo.Reason);
    }

    public async Task NotifyFixTasksGeneratedAsync(string requirementId, List<FixTaskDto> fixTasks)
    {
        var update = new PipelineUpdateMessage
        {
            RequirementId = requirementId,
            UpdateType = "FixTasksGenerated",
            Phase = PipelinePhase.None,
            Message = $"{fixTasks.Count} fix task(s) generated",
            Data = fixTasks
        };
        await SendToRequirementGroupAsync(requirementId, "PipelineUpdate", update);
        _logger.LogInformation("[{RequirementId}] {Count} fix tasks generated", requirementId, fixTasks.Count);
    }

    public async Task NotifyTestResultsAsync(string requirementId, TestSummaryDto testSummary)
    {
        var update = new PipelineUpdateMessage
        {
            RequirementId = requirementId,
            UpdateType = "TestResults",
            Phase = PipelinePhase.UnitTesting,
            Message = $"Tests: {testSummary.Passed}/{testSummary.TotalTests} passed" +
                     (testSummary.IsBreakingChange ? " ⚠️ BREAKING CHANGE!" : ""),
            Data = testSummary
        };
        await SendToRequirementGroupAsync(requirementId, "PipelineUpdate", update);
        _logger.LogInformation("[{RequirementId}] Test results: {Passed}/{Total} passed, {Failed} failed", 
            requirementId, testSummary.Passed, testSummary.TotalTests, testSummary.Failed);
    }

    public async Task NotifyRetryStartingAsync(string requirementId, int attempt, int maxAttempts, PipelinePhase targetPhase)
    {
        var update = new PipelineUpdateMessage
        {
            RequirementId = requirementId,
            UpdateType = "RetryStarting",
            Phase = targetPhase,
            Message = $"Starting retry attempt {attempt}/{maxAttempts}, returning to {targetPhase}",
            Data = new { Attempt = attempt, MaxAttempts = maxAttempts, TargetPhase = targetPhase }
        };
        await SendToRequirementGroupAsync(requirementId, "PipelineUpdate", update);
        _logger.LogInformation("[{RequirementId}] Starting retry attempt {Attempt}/{Max} → {Phase}", 
            requirementId, attempt, maxAttempts, targetPhase);
    }

    public async Task NotifyLLMCallStartingAsync(string requirementId, LLMCallInfo callInfo)
    {
        var costStr = callInfo.EstimatedCostUSD > 0 
            ? $", Est. Cost: ${callInfo.EstimatedCostUSD:F4}" 
            : "";
        
        var update = new PipelineUpdateMessage
        {
            RequirementId = requirementId,
            UpdateType = "LLMCallStarting",
            Phase = PipelinePhase.None,
            Message = $"🤖 LLM Call: {callInfo.AgentName} | ~{callInfo.EstimatedInputTokens:N0} tokens (~{callInfo.EstimatedInputKB} KB){costStr}",
            Data = callInfo
        };
        await SendToRequirementGroupAsync(requirementId, "PipelineUpdate", update);
        _logger.LogInformation("[{RequirementId}] LLM Call Starting: {Agent} | ~{Tokens} tokens (~{KB} KB){Cost}", 
            requirementId, callInfo.AgentName, callInfo.EstimatedInputTokens, callInfo.EstimatedInputKB, costStr);
    }

    public async Task NotifyLLMCallCompletedAsync(string requirementId, LLMCallResult result)
    {
        var update = new PipelineUpdateMessage
        {
            RequirementId = requirementId,
            UpdateType = "LLMCallCompleted",
            Phase = PipelinePhase.None,
            Message = $"✅ LLM Complete: {result.AgentName} | {result.TotalTokens:N0} tokens (in:{result.PromptTokens:N0}, out:{result.CompletionTokens:N0}) | Cost: ${result.ActualCostUSD:F4} | {result.Duration.TotalSeconds:F1}s",
            Data = result
        };
        await SendToRequirementGroupAsync(requirementId, "PipelineUpdate", update);
        _logger.LogInformation("[{RequirementId}] LLM Complete: {Agent} | Total: {Total} tokens | Cost: ${Cost:F4} | Duration: {Duration}s", 
            requirementId, result.AgentName, result.TotalTokens, result.ActualCostUSD, result.Duration.TotalSeconds);
    }

    private PipelineUpdateMessage CreateUpdate(string requirementId, string updateType, PipelinePhase phase, string message, object? data = null)
    {
        return new PipelineUpdateMessage
        {
            RequirementId = requirementId,
            UpdateType = updateType,
            Phase = phase,
            Message = message,
            Data = data
        };
    }

    private async Task SendToRequirementGroupAsync(string requirementId, string method, object message)
    {
        // Send to specific requirement subscribers
        await _hubContext.Clients.Group($"requirement_{requirementId}").SendAsync(method, message);
        
        // Also send to "all updates" subscribers
        await _hubContext.Clients.Group("all_updates").SendAsync(method, message);
    }
}
