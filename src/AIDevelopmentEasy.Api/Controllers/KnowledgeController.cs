using AIDevelopmentEasy.Api.Models;
using AIDevelopmentEasy.Api.Repositories.Interfaces;
using AIDevelopmentEasy.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace AIDevelopmentEasy.Api.Controllers;

/// <summary>
/// API endpoints for Knowledge Base operations.
/// Manages patterns, common errors, templates, and agent insights.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class KnowledgeController : ControllerBase
{
    private readonly IKnowledgeRepository _knowledgeRepository;
    private readonly ILogger<KnowledgeController> _logger;

    public KnowledgeController(
        IKnowledgeRepository knowledgeRepository,
        ILogger<KnowledgeController> logger)
    {
        _knowledgeRepository = knowledgeRepository;
        _logger = logger;
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // General CRUD
    // ═══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Get all knowledge entries
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<KnowledgeEntryDto>>> GetAll(
        [FromQuery] KnowledgeCategoryDto? category = null,
        CancellationToken cancellationToken = default)
    {
        IEnumerable<KnowledgeEntry> entries;

        if (category.HasValue)
        {
            entries = await _knowledgeRepository.GetByCategoryAsync(
                (KnowledgeCategory)category.Value, cancellationToken);
        }
        else
        {
            entries = await _knowledgeRepository.GetAllAsync(cancellationToken);
        }

        return Ok(entries.Select(e => e.ToDto()));
    }

    /// <summary>
    /// Get a single knowledge entry by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<KnowledgeEntryDto>> GetById(string id, CancellationToken cancellationToken)
    {
        var entry = await _knowledgeRepository.GetByIdAsync(id, cancellationToken);

        if (entry == null)
            return NotFound(new { error = $"Knowledge entry not found: {id}" });

        return Ok(entry.ToDto());
    }

    /// <summary>
    /// Delete a knowledge entry
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        var deleted = await _knowledgeRepository.DeleteAsync(id, cancellationToken);

        if (!deleted)
            return NotFound(new { error = $"Knowledge entry not found: {id}" });

        _logger.LogInformation("Deleted knowledge entry: {Id}", id);
        return NoContent();
    }

    /// <summary>
    /// Update a knowledge entry
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<KnowledgeEntryDto>> Update(
        string id,
        [FromBody] UpdateKnowledgeRequest request,
        CancellationToken cancellationToken)
    {
        var entry = await _knowledgeRepository.GetByIdAsync(id, cancellationToken);

        if (entry == null)
            return NotFound(new { error = $"Knowledge entry not found: {id}" });

        entry.Title = request.Title;
        entry.Description = request.Description;
        entry.Tags = request.Tags;
        entry.Context = request.Context;

        var updated = await _knowledgeRepository.UpdateAsync(entry, cancellationToken);

        if (!updated)
            return BadRequest(new { error = "Failed to update entry" });

        return Ok(entry.ToDto());
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // Patterns
    // ═══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Get all patterns
    /// </summary>
    [HttpGet("patterns")]
    public async Task<ActionResult<IEnumerable<SuccessfulPatternDto>>> GetPatterns(
        [FromQuery] PatternSubcategoryDto? subcategory = null,
        CancellationToken cancellationToken = default)
    {
        IEnumerable<SuccessfulPattern> patterns;

        if (subcategory.HasValue)
        {
            patterns = await _knowledgeRepository.GetPatternsBySubcategoryAsync(
                (PatternSubcategory)subcategory.Value, cancellationToken);
        }
        else
        {
            patterns = await _knowledgeRepository.GetPatternsAsync(cancellationToken);
        }

        return Ok(patterns.Select(p => p.ToDto()));
    }

    /// <summary>
    /// Get a pattern by ID
    /// </summary>
    [HttpGet("patterns/{id}")]
    public async Task<ActionResult<SuccessfulPatternDto>> GetPattern(string id, CancellationToken cancellationToken)
    {
        var pattern = await _knowledgeRepository.GetPatternByIdAsync(id, cancellationToken);

        if (pattern == null)
            return NotFound(new { error = $"Pattern not found: {id}" });

        return Ok(pattern.ToDto());
    }

    /// <summary>
    /// Create a new pattern
    /// </summary>
    [HttpPost("patterns")]
    public async Task<ActionResult<SuccessfulPatternDto>> CreatePattern(
        [FromBody] CreatePatternRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return BadRequest(new { error = "Title is required" });

        if (string.IsNullOrWhiteSpace(request.SolutionCode))
            return BadRequest(new { error = "Solution code is required" });

        var pattern = await _knowledgeRepository.CreatePatternAsync(
            request.ToCoreRequest(), cancellationToken);

        _logger.LogInformation("Created pattern: {Id} - {Title}", pattern.Id, pattern.Title);

        return CreatedAtAction(nameof(GetPattern), new { id = pattern.Id }, pattern.ToDto());
    }

    /// <summary>
    /// Find patterns similar to a problem description
    /// </summary>
    [HttpPost("patterns/search")]
    public async Task<ActionResult<PatternSearchResultDto>> FindSimilarPatterns(
        [FromBody] FindPatternsRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ProblemDescription))
            return BadRequest(new { error = "Problem description is required" });

        var result = await _knowledgeRepository.FindSimilarPatternsAsync(
            request.ProblemDescription, request.Limit, cancellationToken);

        return Ok(new PatternSearchResultDto
        {
            Patterns = result.Patterns.Select(p => p.ToDto()).ToList(),
            RelevanceScores = result.RelevanceScores
        });
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // Errors
    // ═══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Get all error entries
    /// </summary>
    [HttpGet("errors")]
    public async Task<ActionResult<IEnumerable<CommonErrorDto>>> GetErrors(
        [FromQuery] ErrorTypeDto? errorType = null,
        CancellationToken cancellationToken = default)
    {
        IEnumerable<CommonError> errors;

        if (errorType.HasValue)
        {
            errors = await _knowledgeRepository.GetErrorsByTypeAsync(
                (ErrorType)errorType.Value, cancellationToken);
        }
        else
        {
            errors = await _knowledgeRepository.GetErrorsAsync(cancellationToken);
        }

        return Ok(errors.Select(e => e.ToDto()));
    }

    /// <summary>
    /// Get an error by ID
    /// </summary>
    [HttpGet("errors/{id}")]
    public async Task<ActionResult<CommonErrorDto>> GetError(string id, CancellationToken cancellationToken)
    {
        var error = await _knowledgeRepository.GetErrorByIdAsync(id, cancellationToken);

        if (error == null)
            return NotFound(new { error = $"Error entry not found: {id}" });

        return Ok(error.ToDto());
    }

    /// <summary>
    /// Create a new error entry
    /// </summary>
    [HttpPost("errors")]
    public async Task<ActionResult<CommonErrorDto>> CreateError(
        [FromBody] CreateErrorRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return BadRequest(new { error = "Title is required" });

        if (string.IsNullOrWhiteSpace(request.FixDescription))
            return BadRequest(new { error = "Fix description is required" });

        var error = await _knowledgeRepository.CreateErrorAsync(
            request.ToCoreRequest(), cancellationToken);

        _logger.LogInformation("Created error entry: {Id} - {Title}", error.Id, error.Title);

        return CreatedAtAction(nameof(GetError), new { id = error.Id }, error.ToDto());
    }

    /// <summary>
    /// Find a matching error for the given error message
    /// </summary>
    [HttpPost("errors/match")]
    public async Task<ActionResult<ErrorMatchResultDto>> FindMatchingError(
        [FromBody] FindErrorRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ErrorMessage))
            return BadRequest(new { error = "Error message is required" });

        var result = await _knowledgeRepository.FindMatchingErrorAsync(
            request.ErrorMessage,
            request.ErrorType.HasValue ? (ErrorType)request.ErrorType.Value : null,
            cancellationToken);

        return Ok(new ErrorMatchResultDto
        {
            Found = result.Found,
            Error = result.Error?.ToDto(),
            MatchScore = result.MatchScore,
            MatchedOn = result.MatchedOn
        });
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // Templates
    // ═══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Get all project templates
    /// </summary>
    [HttpGet("templates")]
    public async Task<ActionResult<IEnumerable<ProjectTemplateDto>>> GetTemplates(
        [FromQuery] string? type = null,
        CancellationToken cancellationToken = default)
    {
        IEnumerable<ProjectTemplate> templates;

        if (!string.IsNullOrEmpty(type))
        {
            templates = await _knowledgeRepository.GetTemplatesByTypeAsync(type, cancellationToken);
        }
        else
        {
            templates = await _knowledgeRepository.GetTemplatesAsync(cancellationToken);
        }

        return Ok(templates.Select(t => t.ToDto()));
    }

    /// <summary>
    /// Get a template by ID
    /// </summary>
    [HttpGet("templates/{id}")]
    public async Task<ActionResult<ProjectTemplateDto>> GetTemplate(string id, CancellationToken cancellationToken)
    {
        var template = await _knowledgeRepository.GetTemplateByIdAsync(id, cancellationToken);

        if (template == null)
            return NotFound(new { error = $"Template not found: {id}" });

        return Ok(template.ToDto());
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // Search
    // ═══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Search knowledge base
    /// </summary>
    [HttpPost("search")]
    public async Task<ActionResult<IEnumerable<KnowledgeEntryDto>>> Search(
        [FromBody] SearchKnowledgeRequest request,
        CancellationToken cancellationToken)
    {
        var entries = await _knowledgeRepository.SearchAsync(
            request.ToCoreParams(), cancellationToken);

        return Ok(entries.Select(e => e.ToDto()));
    }

    /// <summary>
    /// Get all unique tags
    /// </summary>
    [HttpGet("tags")]
    public async Task<ActionResult<IEnumerable<string>>> GetTags(CancellationToken cancellationToken)
    {
        var tags = await _knowledgeRepository.GetAllTagsAsync(cancellationToken);
        return Ok(tags);
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // Usage Tracking
    // ═══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Record usage of a knowledge entry
    /// </summary>
    [HttpPost("{id}/usage")]
    public async Task<ActionResult> RecordUsage(string id, CancellationToken cancellationToken)
    {
        if (!await _knowledgeRepository.ExistsAsync(id, cancellationToken))
            return NotFound(new { error = $"Knowledge entry not found: {id}" });

        await _knowledgeRepository.RecordUsageAsync(id, cancellationToken);
        return Ok(new { message = "Usage recorded" });
    }

    /// <summary>
    /// Update success rate of a knowledge entry
    /// </summary>
    [HttpPost("{id}/success")]
    public async Task<ActionResult> UpdateSuccess(
        string id,
        [FromQuery] bool wasSuccessful,
        CancellationToken cancellationToken)
    {
        if (!await _knowledgeRepository.ExistsAsync(id, cancellationToken))
            return NotFound(new { error = $"Knowledge entry not found: {id}" });

        await _knowledgeRepository.UpdateSuccessRateAsync(id, wasSuccessful, cancellationToken);
        return Ok(new { message = "Success rate updated" });
    }

    /// <summary>
    /// Mark an entry as verified
    /// </summary>
    [HttpPost("{id}/verify")]
    public async Task<ActionResult> Verify(string id, CancellationToken cancellationToken)
    {
        if (!await _knowledgeRepository.ExistsAsync(id, cancellationToken))
            return NotFound(new { error = $"Knowledge entry not found: {id}" });

        await _knowledgeRepository.MarkAsVerifiedAsync(id, cancellationToken);
        return Ok(new { message = "Entry verified" });
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // Statistics
    // ═══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Get knowledge base statistics
    /// </summary>
    [HttpGet("stats")]
    public async Task<ActionResult<KnowledgeStatsDto>> GetStats(CancellationToken cancellationToken)
    {
        var stats = await _knowledgeRepository.GetStatsAsync(cancellationToken);
        return Ok(stats.ToDto());
    }
}
