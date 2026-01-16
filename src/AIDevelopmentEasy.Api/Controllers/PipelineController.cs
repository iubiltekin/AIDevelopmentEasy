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
    /// Start processing a requirement
    /// </summary>
    [HttpPost("{requirementId}/start")]
    public async Task<ActionResult<PipelineStatusDto>> Start(
        string requirementId,
        [FromBody] ProcessRequirementRequest? request,
        CancellationToken cancellationToken)
    {
        try
        {
            var status = await _pipelineService.StartAsync(
                requirementId,
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
    /// Get pipeline status for a requirement
    /// </summary>
    [HttpGet("{requirementId}/status")]
    public async Task<ActionResult<PipelineStatusDto>> GetStatus(string requirementId, CancellationToken cancellationToken)
    {
        var status = await _pipelineService.GetStatusAsync(requirementId, cancellationToken);
        return Ok(status);
    }

    /// <summary>
    /// Approve a phase
    /// </summary>
    [HttpPost("{requirementId}/approve/{phase}")]
    public async Task<ActionResult> ApprovePhase(string requirementId, PipelinePhase phase, CancellationToken cancellationToken)
    {
        var approved = await _pipelineService.ApprovePhaseAsync(requirementId, phase, cancellationToken);
        
        if (!approved)
            return BadRequest("Cannot approve phase - pipeline not waiting for approval at this phase");

        return Ok();
    }

    /// <summary>
    /// Reject a phase
    /// </summary>
    [HttpPost("{requirementId}/reject/{phase}")]
    public async Task<ActionResult> RejectPhase(
        string requirementId,
        PipelinePhase phase,
        [FromBody] RejectPhaseRequest? request,
        CancellationToken cancellationToken)
    {
        var rejected = await _pipelineService.RejectPhaseAsync(
            requirementId, phase, request?.Reason, cancellationToken);
        
        if (!rejected)
            return BadRequest("Cannot reject phase - pipeline not waiting for approval at this phase");

        return Ok();
    }

    /// <summary>
    /// Cancel a running pipeline
    /// </summary>
    [HttpPost("{requirementId}/cancel")]
    public async Task<ActionResult> Cancel(string requirementId, CancellationToken cancellationToken)
    {
        await _pipelineService.CancelAsync(requirementId, cancellationToken);
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
    /// Get generated files for a requirement
    /// </summary>
    [HttpGet("{requirementId}/output")]
    public async Task<ActionResult<Dictionary<string, string>>> GetOutput(string requirementId, CancellationToken cancellationToken)
    {
        var files = await _outputRepository.GetGeneratedFilesAsync(requirementId, cancellationToken);
        return Ok(files);
    }

    /// <summary>
    /// Get review report for a requirement
    /// </summary>
    [HttpGet("{requirementId}/review")]
    public async Task<ActionResult<string>> GetReviewReport(string requirementId, CancellationToken cancellationToken)
    {
        var report = await _outputRepository.GetReviewReportAsync(requirementId, cancellationToken);
        
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
}

public class RejectPhaseRequest
{
    public string? Reason { get; set; }
}
