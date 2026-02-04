using AIDevelopmentEasy.Api.Models;
using AIDevelopmentEasy.Api.Repositories.Interfaces;
using AIDevelopmentEasy.Api.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AIDevelopmentEasy.Api.Controllers;

/// <summary>
/// API endpoints for managing stories
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class StoriesController : ControllerBase
{
    private readonly IStoryRepository _storyRepository;
    private readonly ITaskRepository _taskRepository;
    private readonly IApprovalRepository _approvalRepository;
    private readonly IStoryNameGenerator _storyNameGenerator;
    private readonly ILogger<StoriesController> _logger;

    public StoriesController(
        IStoryRepository storyRepository,
        ITaskRepository taskRepository,
        IApprovalRepository approvalRepository,
        IStoryNameGenerator storyNameGenerator,
        ILogger<StoriesController> logger)
    {
        _storyRepository = storyRepository;
        _taskRepository = taskRepository;
        _approvalRepository = approvalRepository;
        _storyNameGenerator = storyNameGenerator;
        _logger = logger;
    }

    /// <summary>
    /// Get all stories
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<StoryDto>>> GetAll(CancellationToken cancellationToken)
    {
        var stories = await _storyRepository.GetAllAsync(cancellationToken);
        return Ok(stories);
    }

    /// <summary>
    /// Get a single story by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<StoryDto>> GetById(string id, CancellationToken cancellationToken)
    {
        var story = await _storyRepository.GetByIdAsync(id, cancellationToken);

        if (story == null)
            return NotFound();

        return Ok(story);
    }

    /// <summary>
    /// Get story content (raw text/JSON)
    /// </summary>
    [HttpGet("{id}/content")]
    public async Task<ActionResult<string>> GetContent(string id, CancellationToken cancellationToken)
    {
        var content = await _storyRepository.GetContentAsync(id, cancellationToken);

        if (content == null)
            return NotFound();

        return Ok(content);
    }

    /// <summary>
    /// Update story content and/or name (only allowed when status is NotStarted/Draft)
    /// </summary>
    [HttpPut("{id}/content")]
    public async Task<ActionResult> UpdateContent(string id, [FromBody] UpdateContentRequest request, CancellationToken cancellationToken)
    {
        var story = await _storyRepository.GetByIdAsync(id, cancellationToken);

        if (story == null)
            return NotFound();

        // Only allow editing if not started (Draft state)
        if (story.Status != StoryStatus.NotStarted)
        {
            return BadRequest(new
            {
                error = "Cannot edit content",
                message = "Story must be reset before editing. Current status: " + story.Status
            });
        }

        // Update name if provided
        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            await _storyRepository.UpdateNameAsync(id, request.Name, cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(request.Content))
        {
            return BadRequest(new { error = "Content cannot be empty" });
        }

        var updated = await _storyRepository.UpdateContentAsync(id, request.Content, cancellationToken);

        if (!updated)
            return NotFound();

        _logger.LogInformation("Updated content for story: {Id}", id);
        return Ok(new { message = "Content updated successfully" });
    }

    /// <summary>
    /// Create a new story. Name is optional; when empty, a title is generated from content via LLM.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<StoryDto>> Create([FromBody] CreateStoryRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
            return BadRequest("Content is required");

        var name = request.Name?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            name = await _storyNameGenerator.GenerateStoryNameAsync(request.Content, cancellationToken);
            _logger.LogInformation("Generated story name from content: {Name}", name);
        }

        var createRequest = new CreateStoryRequest
        {
            Name = name,
            Content = request.Content,
            Type = request.Type,
            CodebaseId = request.CodebaseId,
            RequirementId = request.RequirementId,
            TargetProject = request.TargetProject,
            TargetFile = request.TargetFile,
            TargetClass = request.TargetClass,
            TargetMethod = request.TargetMethod,
            ChangeType = request.ChangeType,
            TargetTestProject = request.TargetTestProject,
            TargetTestFile = request.TargetTestFile,
            TargetTestClass = request.TargetTestClass
        };

        var story = await _storyRepository.CreateAsync(createRequest, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = story.Id }, story);
    }

    /// <summary>
    /// Update story target information (project, file, class, method)
    /// Use this before starting the pipeline to specify where changes should be made
    /// </summary>
    [HttpPut("{id}/target")]
    public async Task<ActionResult> UpdateTarget(string id, [FromBody] UpdateStoryTargetRequest request, CancellationToken cancellationToken)
    {
        var story = await _storyRepository.GetByIdAsync(id, cancellationToken);

        if (story == null)
            return NotFound();

        // Only allow updating target if story hasn't started processing
        if (story.Status != StoryStatus.NotStarted && story.Status != StoryStatus.Planned)
        {
            return BadRequest(new
            {
                error = "Cannot update target",
                message = "Story must be reset before updating target info. Current status: " + story.Status
            });
        }

        var updated = await _storyRepository.UpdateTargetAsync(id, request, cancellationToken);

        if (!updated)
            return NotFound();

        _logger.LogInformation("Updated target for story: {Id} → {Project}/{File}", id, request.TargetProject, request.TargetFile);
        return Ok(new { message = "Target information updated successfully" });
    }

    /// <summary>
    /// Delete a story
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        var deleted = await _storyRepository.DeleteAsync(id, cancellationToken);

        if (!deleted)
            return NotFound();

        return NoContent();
    }

    /// <summary>
    /// Get tasks for a story
    /// </summary>
    [HttpGet("{id}/tasks")]
    public async Task<ActionResult<IEnumerable<TaskDto>>> GetTasks(string id, CancellationToken cancellationToken)
    {
        var exists = await _storyRepository.ExistsAsync(id, cancellationToken);
        if (!exists)
            return NotFound();

        var tasks = await _taskRepository.GetByStoryAsync(id, cancellationToken);
        return Ok(tasks);
    }

    /// <summary>
    /// Update a task
    /// </summary>
    [HttpPut("{id}/tasks/{taskIndex}")]
    public async Task<ActionResult> UpdateTask(string id, int taskIndex, [FromBody] TaskDto task, CancellationToken cancellationToken)
    {
        var exists = await _storyRepository.ExistsAsync(id, cancellationToken);
        if (!exists)
            return NotFound();

        task.Index = taskIndex;
        await _taskRepository.UpdateTaskAsync(id, task, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Reset story (clear tasks and approval state)
    /// </summary>
    [HttpPost("{id}/reset")]
    public async Task<ActionResult> Reset(string id, [FromQuery] bool clearTasks = true, CancellationToken cancellationToken = default)
    {
        var exists = await _storyRepository.ExistsAsync(id, cancellationToken);
        if (!exists)
            return NotFound();

        // Reset all status flags including failed status
        await _storyRepository.UpdateStatusAsync(id, StoryStatus.NotStarted, cancellationToken);

        if (clearTasks)
        {
            await _taskRepository.DeleteAllAsync(id, cancellationToken);
        }

        _logger.LogInformation("Reset story: {Id}", id);
        return NoContent();
    }
}

public class UpdateContentRequest
{
    public string? Name { get; set; }
    public string Content { get; set; } = string.Empty;
}
