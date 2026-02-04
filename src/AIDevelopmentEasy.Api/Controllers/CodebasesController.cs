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
    /// Get analysis as context string (for prompts) - DEPRECATED: Use /context/requirement or /context/pipeline
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
    /// Get lightweight context for Requirements Wizard (optimized for lower token usage)
    /// </summary>
    [HttpGet("{id}/context/requirement")]
    public async Task<ActionResult<RequirementContextDto>> GetRequirementContext(string id, CancellationToken cancellationToken)
    {
        var analysis = await _codebaseRepository.GetAnalysisAsync(id, cancellationToken);
        if (analysis == null)
            return NotFound("Analysis not available. Run /analyze first.");

        return Ok(new RequirementContextDto
        {
            SummaryText = analysis.RequirementContext.SummaryText,
            TokenEstimate = analysis.RequirementContext.TokenEstimate,
            Projects = analysis.RequirementContext.Projects.Select(p => new ProjectBriefDto
            {
                Name = p.Name,
                Type = p.Type,
                Purpose = p.Purpose,
                KeyNamespaces = p.KeyNamespaces
            }).ToList(),
            Architecture = analysis.RequirementContext.Architecture,
            Technologies = analysis.RequirementContext.Technologies,
            ExtensionPoints = analysis.RequirementContext.ExtensionPoints.Select(e => new ExtensionPointDto
            {
                Layer = e.Layer,
                Project = e.Project,
                Namespace = e.Namespace,
                Pattern = e.Pattern
            }).ToList()
        });
    }

    /// <summary>
    /// Get full context for Pipeline operations (detailed for code generation)
    /// </summary>
    [HttpGet("{id}/context/pipeline")]
    public async Task<ActionResult<PipelineContextDto>> GetPipelineContext(string id, CancellationToken cancellationToken)
    {
        var analysis = await _codebaseRepository.GetAnalysisAsync(id, cancellationToken);
        if (analysis == null)
            return NotFound("Analysis not available. Run /analyze first.");

        return Ok(new PipelineContextDto
        {
            FullContextText = analysis.PipelineContext.FullContextText,
            TokenEstimate = analysis.PipelineContext.TokenEstimate,
            ProjectCount = analysis.PipelineContext.ProjectDetails.Count,
            ClassCount = analysis.PipelineContext.ProjectDetails.Sum(p => p.Classes.Count),
            InterfaceCount = analysis.PipelineContext.ProjectDetails.Sum(p => p.Interfaces.Count)
        });
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
        try
        {
            var deleted = await _codebaseRepository.DeleteAsync(id, cancellationToken);

            if (!deleted)
                return NotFound();

            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Delete forbidden for codebase: {Id}", id);
            return Problem(
                detail: "Silme yetkisi yok. Uygulamayı yönetici olarak çalıştırmayı deneyin veya ProgramData klasörünün izinlerini kontrol edin.",
                statusCode: 403,
                title: "Silme başarısız");
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "Delete IO error for codebase: {Id}", id);
            return Problem(
                detail: "Dosya veya klasör kullanımda olabilir. Uygulamayı kapatıp tekrar deneyin veya bilgisayarı yeniden başlatın.",
                statusCode: 409,
                title: "Silme başarısız");
        }
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
            ProjectReferences = p.ProjectReferences,
            LanguageId = p.LanguageId ?? string.Empty,
            Role = p.Role ?? string.Empty,
            RootPath = p.RootPath ?? string.Empty
        }).ToList();

        _logger.LogInformation("[GetProjects] Codebase {Id}: returning {Count} project(s): {Names}",
            id, projects.Count, string.Join(", ", projects.Select(x => x.Name)));

        return Ok(projects);
    }

    /// <summary>
    /// Get files in a specific project (for target selection dropdowns)
    /// </summary>
    [HttpGet("{id}/projects/{projectName}/files")]
    public async Task<ActionResult<IEnumerable<FileInfoDto>>> GetProjectFiles(string id, string projectName, CancellationToken cancellationToken)
    {
        var analysis = await _codebaseRepository.GetAnalysisAsync(id, cancellationToken);
        if (analysis == null)
            return NotFound("Analysis not available");

        var project = analysis.Projects.FirstOrDefault(p =>
            p.Name.Equals(projectName, StringComparison.OrdinalIgnoreCase));

        if (project == null)
            return NotFound($"Project '{projectName}' not found");

        // Extract unique files from classes and interfaces
        var allTypes = project.Classes.Concat(project.Interfaces);
        var fileGroups = allTypes
            .Where(t => !string.IsNullOrEmpty(t.FilePath))
            .GroupBy(t => t.FilePath)
            .Select(g => new FileInfoDto
            {
                Path = g.Key,
                Name = Path.GetFileName(g.Key),
                Namespace = g.First().Namespace,
                ClassCount = g.Count(t => !t.Name.StartsWith("I") || t.BaseTypes.Count > 0), // Rough class detection
                InterfaceCount = g.Count(t => t.Name.StartsWith("I") && t.BaseTypes.Count == 0)
            })
            .OrderBy(f => f.Path);

        return Ok(fileGroups);
    }

    /// <summary>
    /// Get classes in a specific project (for target selection dropdowns)
    /// </summary>
    [HttpGet("{id}/projects/{projectName}/classes")]
    public async Task<ActionResult<IEnumerable<ClassInfoDto>>> GetProjectClasses(string id, string projectName, CancellationToken cancellationToken)
    {
        var analysis = await _codebaseRepository.GetAnalysisAsync(id, cancellationToken);
        if (analysis == null)
            return NotFound("Analysis not available");

        var project = analysis.Projects.FirstOrDefault(p =>
            p.Name.Equals(projectName, StringComparison.OrdinalIgnoreCase));

        if (project == null)
            return NotFound($"Project '{projectName}' not found");

        var classes = project.Classes.Select(c => new ClassInfoDto
        {
            Name = c.Name,
            Namespace = c.Namespace,
            FilePath = c.FilePath,
            BaseTypes = c.BaseTypes,
            Pattern = c.DetectedPattern,
            Methods = c.Members
                .Where(m => m.Kind == "Method")
                .Select(m => m.Name)
                .ToList()
        });

        return Ok(classes);
    }

    /// <summary>
    /// Get methods of a specific class (for target selection dropdowns)
    /// </summary>
    [HttpGet("{id}/projects/{projectName}/classes/{className}/methods")]
    public async Task<ActionResult<IEnumerable<MethodInfoDto>>> GetClassMethods(
        string id, string projectName, string className, CancellationToken cancellationToken)
    {
        var analysis = await _codebaseRepository.GetAnalysisAsync(id, cancellationToken);
        if (analysis == null)
            return NotFound("Analysis not available");

        var project = analysis.Projects.FirstOrDefault(p =>
            p.Name.Equals(projectName, StringComparison.OrdinalIgnoreCase));

        if (project == null)
            return NotFound($"Project '{projectName}' not found");

        var classInfo = project.Classes.FirstOrDefault(c =>
            c.Name.Equals(className, StringComparison.OrdinalIgnoreCase));

        if (classInfo == null)
            return NotFound($"Class '{className}' not found in project '{projectName}'");

        var methods = classInfo.Members
            .Where(m => m.Kind == "Method")
            .Select(m => new MethodInfoDto
            {
                Name = m.Name,
                ReturnType = m.ReturnType ?? "void",
                Parameters = m.Parameters,
                IsPublic = m.Modifiers.Contains("public"),
                IsAsync = m.Modifiers.Contains("async")
            });

        return Ok(methods);
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// Target Selection DTOs
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// File information for target selection
/// </summary>
public class FileInfoDto
{
    public string Path { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Namespace { get; set; } = string.Empty;
    public int ClassCount { get; set; }
    public int InterfaceCount { get; set; }
}

/// <summary>
/// Class information for target selection
/// </summary>
public class ClassInfoDto
{
    public string Name { get; set; } = string.Empty;
    public string Namespace { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public List<string> BaseTypes { get; set; } = new();
    public string? Pattern { get; set; }
    public List<string> Methods { get; set; } = new();
}

/// <summary>
/// Method information for target selection
/// </summary>
public class MethodInfoDto
{
    public string Name { get; set; } = string.Empty;
    public string ReturnType { get; set; } = string.Empty;
    public List<string> Parameters { get; set; } = new();
    public bool IsPublic { get; set; }
    public bool IsAsync { get; set; }
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
    /// <summary>Language of this project (e.g. csharp, go, typescript).</summary>
    public string LanguageId { get; set; } = string.Empty;
    /// <summary>Role: Backend, Frontend, or empty.</summary>
    public string Role { get; set; } = string.Empty;
    /// <summary>Root path of project relative to codebase.</summary>
    public string RootPath { get; set; } = string.Empty;
}

// ═══════════════════════════════════════════════════════════════════════════════
// Context DTOs (Two-Level LLM Optimization)
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Lightweight context for Requirements Wizard
/// </summary>
public class RequirementContextDto
{
    public string SummaryText { get; set; } = string.Empty;
    public int TokenEstimate { get; set; }
    public List<ProjectBriefDto> Projects { get; set; } = new();
    public List<string> Architecture { get; set; } = new();
    public List<string> Technologies { get; set; } = new();
    public List<ExtensionPointDto> ExtensionPoints { get; set; } = new();
}

/// <summary>
/// Brief project info
/// </summary>
public class ProjectBriefDto
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public List<string> KeyNamespaces { get; set; } = new();
}

/// <summary>
/// Extension point info
/// </summary>
public class ExtensionPointDto
{
    public string Layer { get; set; } = string.Empty;
    public string Project { get; set; } = string.Empty;
    public string Namespace { get; set; } = string.Empty;
    public string? Pattern { get; set; }
}

/// <summary>
/// Full context for Pipeline operations
/// </summary>
public class PipelineContextDto
{
    public string FullContextText { get; set; } = string.Empty;
    public int TokenEstimate { get; set; }
    public int ProjectCount { get; set; }
    public int ClassCount { get; set; }
    public int InterfaceCount { get; set; }
}
