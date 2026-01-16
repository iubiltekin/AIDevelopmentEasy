using AIDevelopmentEasy.Api.Models;
using AIDevelopmentEasy.Api.Repositories.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AIDevelopmentEasy.Api.Controllers;

/// <summary>
/// API endpoints for managing requirements
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class RequirementsController : ControllerBase
{
    private readonly IRequirementRepository _requirementRepository;
    private readonly ITaskRepository _taskRepository;
    private readonly IApprovalRepository _approvalRepository;
    private readonly ILogger<RequirementsController> _logger;

    public RequirementsController(
        IRequirementRepository requirementRepository,
        ITaskRepository taskRepository,
        IApprovalRepository approvalRepository,
        ILogger<RequirementsController> logger)
    {
        _requirementRepository = requirementRepository;
        _taskRepository = taskRepository;
        _approvalRepository = approvalRepository;
        _logger = logger;
    }

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
    /// Get a single requirement by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<RequirementDto>> GetById(string id, CancellationToken cancellationToken)
    {
        var requirement = await _requirementRepository.GetByIdAsync(id, cancellationToken);
        
        if (requirement == null)
            return NotFound();

        return Ok(requirement);
    }

    /// <summary>
    /// Get requirement content (raw text/JSON)
    /// </summary>
    [HttpGet("{id}/content")]
    public async Task<ActionResult<string>> GetContent(string id, CancellationToken cancellationToken)
    {
        var content = await _requirementRepository.GetContentAsync(id, cancellationToken);
        
        if (content == null)
            return NotFound();

        return Ok(content);
    }

    /// <summary>
    /// Create a new requirement
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<RequirementDto>> Create([FromBody] CreateRequirementRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Content))
        {
            return BadRequest("Name and content are required");
        }

        var requirement = await _requirementRepository.CreateAsync(
            request.Name,
            request.Content,
            request.Type,
            request.CodebaseId,
            cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = requirement.Id }, requirement);
    }

    /// <summary>
    /// Delete a requirement
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        var deleted = await _requirementRepository.DeleteAsync(id, cancellationToken);
        
        if (!deleted)
            return NotFound();

        return NoContent();
    }

    /// <summary>
    /// Get tasks for a requirement
    /// </summary>
    [HttpGet("{id}/tasks")]
    public async Task<ActionResult<IEnumerable<TaskDto>>> GetTasks(string id, CancellationToken cancellationToken)
    {
        var exists = await _requirementRepository.ExistsAsync(id, cancellationToken);
        if (!exists)
            return NotFound();

        var tasks = await _taskRepository.GetByRequirementAsync(id, cancellationToken);
        return Ok(tasks);
    }

    /// <summary>
    /// Update a task
    /// </summary>
    [HttpPut("{id}/tasks/{taskIndex}")]
    public async Task<ActionResult> UpdateTask(string id, int taskIndex, [FromBody] TaskDto task, CancellationToken cancellationToken)
    {
        var exists = await _requirementRepository.ExistsAsync(id, cancellationToken);
        if (!exists)
            return NotFound();

        task.Index = taskIndex;
        await _taskRepository.UpdateTaskAsync(id, task, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Reset requirement (clear tasks and approval state)
    /// </summary>
    [HttpPost("{id}/reset")]
    public async Task<ActionResult> Reset(string id, [FromQuery] bool clearTasks = true, CancellationToken cancellationToken = default)
    {
        var exists = await _requirementRepository.ExistsAsync(id, cancellationToken);
        if (!exists)
            return NotFound();

        // Reset all status flags including failed status
        await _requirementRepository.UpdateStatusAsync(id, RequirementStatus.NotStarted, cancellationToken);

        if (clearTasks)
        {
            await _taskRepository.DeleteAllAsync(id, cancellationToken);
        }

        _logger.LogInformation("Reset requirement: {Id}", id);
        return NoContent();
    }
}

public class CreateRequirementRequest
{
    public string Name { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public RequirementType Type { get; set; } = RequirementType.Single;
    public string? CodebaseId { get; set; }
}
