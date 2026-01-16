using AIDevelopmentEasy.Api.Models;
using AIDevelopmentEasy.Api.Repositories.Interfaces;
using AIDevelopmentEasy.Core.Agents;
using AIDevelopmentEasy.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace AIDevelopmentEasy.Api.Controllers;

/// <summary>
/// API endpoints for managing codebases
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class CodebasesController : ControllerBase
{
    private readonly ICodebaseRepository _codebaseRepository;
    private readonly CodeAnalysisAgent _codeAnalysisAgent;
    private readonly ILogger<CodebasesController> _logger;

    public CodebasesController(
        ICodebaseRepository codebaseRepository,
        CodeAnalysisAgent codeAnalysisAgent,
        ILogger<CodebasesController> logger)
    {
        _codebaseRepository = codebaseRepository;
        _codeAnalysisAgent = codeAnalysisAgent;
        _logger = logger;
    }

    /// <summary>
    /// Get all registered codebases
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<CodebaseDto>>> GetAll(CancellationToken cancellationToken)
    {
        var codebases = await _codebaseRepository.GetAllAsync(cancellationToken);
        return Ok(codebases);
    }

    /// <summary>
    /// Get a codebase by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<CodebaseDto>> GetById(string id, CancellationToken cancellationToken)
    {
        var codebase = await _codebaseRepository.GetByIdAsync(id, cancellationToken);

        if (codebase == null)
            return NotFound();

        return Ok(codebase);
    }

    /// <summary>
    /// Get the full analysis for a codebase
    /// </summary>
    [HttpGet("{id}/analysis")]
    public async Task<ActionResult<CodebaseAnalysis>> GetAnalysis(string id, CancellationToken cancellationToken)
    {
        var codebase = await _codebaseRepository.GetByIdAsync(id, cancellationToken);
        if (codebase == null)
            return NotFound();

        var analysis = await _codebaseRepository.GetAnalysisAsync(id, cancellationToken);
        if (analysis == null)
            return NotFound("Analysis not available. Run /analyze first.");

        return Ok(analysis);
    }

    /// <summary>
    /// Get analysis as context string (for prompts)
    /// </summary>
    [HttpGet("{id}/context")]
    public async Task<ActionResult<string>> GetContext(string id, CancellationToken cancellationToken)
    {
        var analysis = await _codebaseRepository.GetAnalysisAsync(id, cancellationToken);
        if (analysis == null)
            return NotFound("Analysis not available. Run /analyze first.");

        var context = _codeAnalysisAgent.GenerateContextForPrompt(analysis);
        return Ok(context);
    }

    /// <summary>
    /// Register a new codebase and start analysis
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<CodebaseDto>> Create(
        [FromBody] CreateCodebaseRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Path))
        {
            return BadRequest("Name and path are required");
        }

        if (!Directory.Exists(request.Path))
        {
            return BadRequest($"Path does not exist: {request.Path}");
        }

        // Create codebase entry
        var codebase = await _codebaseRepository.CreateAsync(request.Name, request.Path, cancellationToken);

        // Start analysis in background
        _ = Task.Run(async () =>
        {
            try
            {
                await _codebaseRepository.UpdateStatusAsync(codebase.Id, CodebaseStatus.Analyzing);

                var analysis = await _codeAnalysisAgent.AnalyzeAsync(request.Path, request.Name);
                await _codebaseRepository.SaveAnalysisAsync(codebase.Id, analysis);

                _logger.LogInformation("Codebase analysis completed: {Id}", codebase.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing codebase: {Id}", codebase.Id);
                await _codebaseRepository.UpdateStatusAsync(codebase.Id, CodebaseStatus.Failed);
            }
        }, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = codebase.Id }, codebase);
    }

    /// <summary>
    /// Re-analyze an existing codebase
    /// </summary>
    [HttpPost("{id}/analyze")]
    public async Task<ActionResult> Analyze(string id, CancellationToken cancellationToken)
    {
        var codebase = await _codebaseRepository.GetByIdAsync(id, cancellationToken);
        if (codebase == null)
            return NotFound();

        if (!Directory.Exists(codebase.Path))
        {
            return BadRequest($"Codebase path no longer exists: {codebase.Path}");
        }

        // Update status and start analysis in background
        await _codebaseRepository.UpdateStatusAsync(id, CodebaseStatus.Analyzing, cancellationToken);

        _ = Task.Run(async () =>
        {
            try
            {
                var analysis = await _codeAnalysisAgent.AnalyzeAsync(codebase.Path, codebase.Name);
                await _codebaseRepository.SaveAnalysisAsync(id, analysis);

                _logger.LogInformation("Codebase re-analysis completed: {Id}", id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error re-analyzing codebase: {Id}", id);
                await _codebaseRepository.UpdateStatusAsync(id, CodebaseStatus.Failed);
            }
        }, cancellationToken);

        return Accepted(new { message = "Analysis started", id });
    }

    /// <summary>
    /// Delete a codebase
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        var deleted = await _codebaseRepository.DeleteAsync(id, cancellationToken);

        if (!deleted)
            return NotFound();

        return NoContent();
    }

    /// <summary>
    /// Get projects in a codebase
    /// </summary>
    [HttpGet("{id}/projects")]
    public async Task<ActionResult<IEnumerable<ProjectSummaryDto>>> GetProjects(string id, CancellationToken cancellationToken)
    {
        var analysis = await _codebaseRepository.GetAnalysisAsync(id, cancellationToken);
        if (analysis == null)
            return NotFound("Analysis not available");

        var projects = analysis.Projects.Select(p => new ProjectSummaryDto
        {
            Name = p.Name,
            RelativePath = p.RelativePath,
            TargetFramework = p.TargetFramework,
            OutputType = p.OutputType,
            IsTestProject = p.IsTestProject,
            ClassCount = p.Classes.Count,
            InterfaceCount = p.Interfaces.Count,
            DetectedPatterns = p.DetectedPatterns,
            ProjectReferences = p.ProjectReferences
        });

        return Ok(projects);
    }
}

/// <summary>
/// Summary of a project within a codebase
/// </summary>
public class ProjectSummaryDto
{
    public string Name { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public string TargetFramework { get; set; } = string.Empty;
    public string OutputType { get; set; } = string.Empty;
    public bool IsTestProject { get; set; }
    public int ClassCount { get; set; }
    public int InterfaceCount { get; set; }
    public List<string> DetectedPatterns { get; set; } = new();
    public List<string> ProjectReferences { get; set; } = new();
}
