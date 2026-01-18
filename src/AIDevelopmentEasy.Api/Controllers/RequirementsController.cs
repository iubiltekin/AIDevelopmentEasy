using AIDevelopmentEasy.Api.Models;
using AIDevelopmentEasy.Api.Repositories.Interfaces;
using AIDevelopmentEasy.Api.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AIDevelopmentEasy.Api.Controllers;

/// <summary>
/// API endpoints for managing requirements and the requirement wizard
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class RequirementsController : ControllerBase
{
    private readonly IRequirementRepository _requirementRepository;
    private readonly IRequirementWizardService _wizardService;
    private readonly ILogger<RequirementsController> _logger;

    public RequirementsController(
        IRequirementRepository requirementRepository,
        IRequirementWizardService wizardService,
        ILogger<RequirementsController> logger)
    {
        _requirementRepository = requirementRepository;
        _wizardService = wizardService;
        _logger = logger;
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // Basic CRUD Operations
    // ═══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Get all requirements
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<RequirementDto>>> GetAll(CancellationToken cancellationToken)
    {
        var requirements = await _requirementRepository.GetAllAsync(cancellationToken);
        return Ok(requirements);
    }

    /// <summary>
    /// Get a single requirement by ID (with full details)
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<RequirementDetailDto>> GetById(string id, CancellationToken cancellationToken)
    {
        var requirement = await _requirementRepository.GetByIdAsync(id, cancellationToken);

        if (requirement == null)
            return NotFound();

        return Ok(requirement);
    }

    /// <summary>
    /// Create a new requirement
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<RequirementDto>> Create([FromBody] CreateRequirementRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RawContent))
        {
            return BadRequest(new { error = "Raw content is required" });
        }

        var requirement = await _requirementRepository.CreateAsync(request, cancellationToken);

        _logger.LogInformation("Created requirement: {Id} - {Title}", requirement.Id, requirement.Title);

        return CreatedAtAction(nameof(GetById), new { id = requirement.Id }, requirement);
    }

    /// <summary>
    /// Update a requirement (only allowed in Draft status)
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<RequirementDto>> Update(string id, [FromBody] UpdateRequirementRequest request, CancellationToken cancellationToken)
    {
        var requirement = await _requirementRepository.UpdateAsync(id, request, cancellationToken);

        if (requirement == null)
            return NotFound();

        _logger.LogInformation("Updated requirement: {Id}", id);

        return Ok(requirement);
    }

    /// <summary>
    /// Delete a requirement
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        // Check if wizard is running
        if (await _wizardService.IsRunningAsync(id, cancellationToken))
        {
            return BadRequest(new { error = "Cannot delete while wizard is running" });
        }

        var deleted = await _requirementRepository.DeleteAsync(id, cancellationToken);

        if (!deleted)
            return NotFound();

        _logger.LogInformation("Deleted requirement: {Id}", id);

        return NoContent();
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // Wizard Operations
    // ═══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Start the wizard for a requirement
    /// </summary>
    [HttpPost("{id}/wizard/start")]
    public async Task<ActionResult<WizardStatusDto>> StartWizard(string id, [FromBody] StartWizardRequest? request, CancellationToken cancellationToken)
    {
        try
        {
            var status = await _wizardService.StartAsync(id, request?.AutoApproveAll ?? false, cancellationToken);
            return Ok(status);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get wizard status
    /// </summary>
    [HttpGet("{id}/wizard/status")]
    public async Task<ActionResult<WizardStatusDto>> GetWizardStatus(string id, CancellationToken cancellationToken)
    {
        var status = await _wizardService.GetStatusAsync(id, cancellationToken);

        if (status == null)
            return NotFound(new { error = "No wizard status found" });

        return Ok(status);
    }

    /// <summary>
    /// Approve current wizard phase
    /// </summary>
    [HttpPost("{id}/wizard/approve")]
    public async Task<ActionResult<WizardStatusDto>> ApprovePhase(string id, [FromBody] ApproveWizardPhaseRequest? request, CancellationToken cancellationToken)
    {
        try
        {
            var status = await _wizardService.ApprovePhaseAsync(
                id,
                request?.Approved ?? true,
                request?.Comment,
                cancellationToken);
            return Ok(status);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Submit answers to questions
    /// </summary>
    [HttpPost("{id}/wizard/answers")]
    public async Task<ActionResult<WizardStatusDto>> SubmitAnswers(string id, [FromBody] SubmitAnswersRequest request, CancellationToken cancellationToken)
    {
        if (request.Answers == null || !request.Answers.Any())
        {
            return BadRequest(new { error = "Answers are required" });
        }

        try
        {
            var status = await _wizardService.SubmitAnswersAsync(id, request, cancellationToken);
            return Ok(status);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Create stories from selected definitions
    /// </summary>
    [HttpPost("{id}/wizard/stories")]
    public async Task<ActionResult<WizardStatusDto>> CreateStories(string id, [FromBody] CreateStoriesRequest request, CancellationToken cancellationToken)
    {
        if (request.SelectedStoryIds == null || !request.SelectedStoryIds.Any())
        {
            return BadRequest(new { error = "At least one story must be selected" });
        }

        try
        {
            var status = await _wizardService.CreateStoriesAsync(id, request, cancellationToken);
            return Ok(status);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Cancel the wizard
    /// </summary>
    [HttpPost("{id}/wizard/cancel")]
    public async Task<ActionResult<WizardStatusDto>> CancelWizard(string id, CancellationToken cancellationToken)
    {
        try
        {
            var status = await _wizardService.CancelAsync(id, cancellationToken);
            return Ok(status);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // Related Stories
    // ═══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Get stories created from this requirement
    /// </summary>
    [HttpGet("{id}/stories")]
    public async Task<ActionResult<IEnumerable<string>>> GetStories(string id, CancellationToken cancellationToken)
    {
        var exists = await _requirementRepository.ExistsAsync(id, cancellationToken);
        if (!exists)
            return NotFound();

        var storyIds = await _requirementRepository.GetCreatedStoryIdsAsync(id, cancellationToken);
        return Ok(storyIds);
    }
}
