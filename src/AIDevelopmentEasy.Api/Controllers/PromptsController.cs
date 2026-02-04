using Microsoft.AspNetCore.Mvc;
using AIDevelopmentEasy.Core.Services;

namespace AIDevelopmentEasy.Api.Controllers;

/// <summary>
/// API for listing and editing agent prompt files. No add/delete; view and update only.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class PromptsController : ControllerBase
{
    private static readonly HashSet<string> AllowedCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        "coder", "debugger", "planner", "requirement", "reviewer"
    };

    private readonly ILogger<PromptsController> _logger;

    public PromptsController(ILogger<PromptsController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// List all prompts grouped by category.
    /// </summary>
    [HttpGet]
    public ActionResult<Dictionary<string, List<string>>> List(CancellationToken cancellationToken = default)
    {
        var loader = PromptLoader.Instance;
        var baseDir = loader.PromptsDirectory;
        var result = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        if (!Directory.Exists(baseDir))
            return Ok(result);

        foreach (var categoryDir in Directory.EnumerateDirectories(baseDir))
        {
            var category = Path.GetFileName(categoryDir);
            if (string.IsNullOrEmpty(category) || !AllowedCategories.Contains(category))
                continue;

            var names = new List<string>();
            foreach (var file in Directory.EnumerateFiles(categoryDir, "*.md"))
            {
                var name = Path.GetFileNameWithoutExtension(file);
                if (!string.IsNullOrEmpty(name) && name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0)
                    names.Add(name);
            }

            names.Sort(StringComparer.OrdinalIgnoreCase);
            result[category] = names;
        }

        return Ok(result);
    }

    /// <summary>
    /// Get a single prompt's raw content (for viewing/editing).
    /// </summary>
    [HttpGet("{category}/{name}")]
    public ActionResult<PromptContentDto> Get(string category, string name, CancellationToken cancellationToken = default)
    {
        if (!AllowedCategories.Contains(category) || !IsSafeName(name))
            return NotFound();

        var loader = PromptLoader.Instance;
        var path = Path.Combine(loader.PromptsDirectory, category, $"{name}.md");
        if (!System.IO.File.Exists(path))
            return NotFound();

        try
        {
            var content = System.IO.File.ReadAllText(path);
            return Ok(new PromptContentDto { Category = category, Name = name, Content = content });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read prompt {Category}/{Name}", category, name);
            return StatusCode(500, new { error = "Failed to read prompt file" });
        }
    }

    /// <summary>
    /// Update an existing prompt's content. No new files; only updates existing.
    /// </summary>
    [HttpPut("{category}/{name}")]
    public async Task<ActionResult> Update(string category, string name, [FromBody] UpdatePromptRequest request, CancellationToken cancellationToken = default)
    {
        if (request == null || request.Content == null)
            return BadRequest(new { error = "Content is required" });
        if (!AllowedCategories.Contains(category) || !IsSafeName(name))
            return NotFound();

        var loader = PromptLoader.Instance;
        var path = Path.Combine(loader.PromptsDirectory, category, $"{name}.md");
        if (!System.IO.File.Exists(path))
            return NotFound();

        try
        {
            await System.IO.File.WriteAllTextAsync(path, request.Content, cancellationToken);
            loader.ClearCache();
            _logger.LogInformation("Updated prompt {Category}/{Name}", category, name);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write prompt {Category}/{Name}", category, name);
            return StatusCode(500, new { error = "Failed to write prompt file" });
        }
    }

    private static bool IsSafeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return false;
        if (name.Contains("..")) return false;
        return true;
    }
}

public class PromptContentDto
{
    public string Category { get; set; } = "";
    public string Name { get; set; } = "";
    public string Content { get; set; } = "";
}

public class UpdatePromptRequest
{
    public string Content { get; set; } = "";
}
