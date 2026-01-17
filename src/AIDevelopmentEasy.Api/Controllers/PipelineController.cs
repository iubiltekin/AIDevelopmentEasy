using AIDevelopmentEasy.Api.Models;
using AIDevelopmentEasy.Api.Repositories.Interfaces;
using AIDevelopmentEasy.Api.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AIDevelopmentEasy.Api.Controllers;

/// <summary>
/// API endpoints for pipeline management
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class PipelineController : ControllerBase
{
    private readonly IPipelineService _pipelineService;
    private readonly IOutputRepository _outputRepository;
    private readonly ILogger<PipelineController> _logger;

    public PipelineController(
        IPipelineService pipelineService,
        IOutputRepository outputRepository,
        ILogger<PipelineController> logger)
    {
        _pipelineService = pipelineService;
        _outputRepository = outputRepository;
        _logger = logger;
    }

    /// <summary>
    /// Start processing a story
    /// </summary>
    [HttpPost("{storyId}/start")]
    public async Task<ActionResult<PipelineStatusDto>> Start(
        string storyId,
        [FromBody] ProcessStoryRequest? request,
        CancellationToken cancellationToken)
    {
        try
        {
            var status = await _pipelineService.StartAsync(
                storyId,
                request?.AutoApproveAll ?? false,
                cancellationToken);

            return Ok(status);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Get pipeline status for a story
    /// </summary>
    [HttpGet("{storyId}/status")]
    public async Task<ActionResult<PipelineStatusDto>> GetStatus(string storyId, CancellationToken cancellationToken)
    {
        var status = await _pipelineService.GetStatusAsync(storyId, cancellationToken);
        return Ok(status);
    }

    /// <summary>
    /// Approve a phase
    /// </summary>
    [HttpPost("{storyId}/approve/{phase}")]
    public async Task<ActionResult> ApprovePhase(string storyId, PipelinePhase phase, CancellationToken cancellationToken)
    {
        var approved = await _pipelineService.ApprovePhaseAsync(storyId, phase, cancellationToken);
        
        if (!approved)
            return BadRequest("Cannot approve phase - pipeline not waiting for approval at this phase");

        return Ok();
    }

    /// <summary>
    /// Reject a phase
    /// </summary>
    [HttpPost("{storyId}/reject/{phase}")]
    public async Task<ActionResult> RejectPhase(
        string storyId,
        PipelinePhase phase,
        [FromBody] RejectPhaseRequest? request,
        CancellationToken cancellationToken)
    {
        var rejected = await _pipelineService.RejectPhaseAsync(
            storyId, phase, request?.Reason, cancellationToken);
        
        if (!rejected)
            return BadRequest("Cannot reject phase - pipeline not waiting for approval at this phase");

        return Ok();
    }

    /// <summary>
    /// Cancel a running pipeline
    /// </summary>
    [HttpPost("{storyId}/cancel")]
    public async Task<ActionResult> Cancel(string storyId, CancellationToken cancellationToken)
    {
        await _pipelineService.CancelAsync(storyId, cancellationToken);
        return Ok();
    }

    /// <summary>
    /// Get all running pipelines
    /// </summary>
    [HttpGet("running")]
    public async Task<ActionResult<IEnumerable<string>>> GetRunningPipelines(CancellationToken cancellationToken)
    {
        var running = await _pipelineService.GetRunningPipelinesAsync(cancellationToken);
        return Ok(running);
    }

    /// <summary>
    /// Get generated files for a story
    /// </summary>
    [HttpGet("{storyId}/output")]
    public async Task<ActionResult<Dictionary<string, string>>> GetOutput(string storyId, CancellationToken cancellationToken)
    {
        var files = await _outputRepository.GetGeneratedFilesAsync(storyId, cancellationToken);
        return Ok(files);
    }

    /// <summary>
    /// Get review report for a story
    /// </summary>
    [HttpGet("{storyId}/review")]
    public async Task<ActionResult<string>> GetReviewReport(string storyId, CancellationToken cancellationToken)
    {
        var report = await _outputRepository.GetReviewReportAsync(storyId, cancellationToken);
        
        if (report == null)
            return NotFound();

        return Ok(report);
    }

    /// <summary>
    /// List all outputs
    /// </summary>
    [HttpGet("outputs")]
    public async Task<ActionResult<IEnumerable<string>>> ListOutputs(CancellationToken cancellationToken)
    {
        var outputs = await _outputRepository.ListOutputsAsync(cancellationToken);
        return Ok(outputs);
    }

    /// <summary>
    /// Approve retry with specified action
    /// </summary>
    [HttpPost("{storyId}/retry")]
    public async Task<ActionResult> ApproveRetry(
        string storyId,
        [FromBody] ApproveRetryRequest request,
        CancellationToken cancellationToken)
    {
        var approved = await _pipelineService.ApproveRetryAsync(storyId, request.Action, cancellationToken);
        
        if (!approved)
            return BadRequest("Cannot approve retry - no retry pending");

        return Ok(new { Message = $"Retry approved with action: {request.Action}" });
    }

    /// <summary>
    /// Get current retry info if any
    /// </summary>
    [HttpGet("{storyId}/retry")]
    public async Task<ActionResult<RetryInfoDto?>> GetRetryInfo(string storyId, CancellationToken cancellationToken)
    {
        var retryInfo = await _pipelineService.GetRetryInfoAsync(storyId, cancellationToken);
        return Ok(retryInfo);
    }

    /// <summary>
    /// Get pipeline execution history (all phase details from completed pipeline)
    /// </summary>
    [HttpGet("{storyId}/history")]
    public async Task<ActionResult<PipelineStatusDto?>> GetHistory(string storyId, CancellationToken cancellationToken)
    {
        // First try to get from running/memory
        var status = await _pipelineService.GetStatusAsync(storyId, cancellationToken);
        
        if (status != null && status.CurrentPhase == PipelinePhase.Completed)
        {
            return Ok(status);
        }

        // Try to get from disk directly
        var historyJson = await _outputRepository.GetPipelineHistoryAsync(storyId, cancellationToken);
        
        if (string.IsNullOrEmpty(historyJson))
        {
            return NotFound(new { Message = "No pipeline history found for this story" });
        }

        try
        {
            var historyStatus = System.Text.Json.JsonSerializer.Deserialize<PipelineStatusDto>(historyJson, new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            return Ok(historyStatus);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deserialize pipeline history for {StoryId}", storyId);
            return BadRequest(new { Message = "Failed to parse pipeline history" });
        }
    }
}

public class RejectPhaseRequest
{
    public string? Reason { get; set; }
}
