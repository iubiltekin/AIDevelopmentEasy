using System.Text;
using AIDevelopmentEasy.Core.Agents.Base;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Logging;

namespace AIDevelopmentEasy.Core.Agents;

/// <summary>
/// Reviewer Agent - Final quality assurance and validation.
/// Examines the entire codebase for correctness, quality, and requirement compliance.
/// </summary>
public class ReviewerAgent : BaseAgent
{
    public override string Name => "Reviewer";
    public override string Role => "Senior Code Reviewer - Validates code quality and requirement compliance";
    protected override string? PromptFileName => "reviewer";

    public ReviewerAgent(
        OpenAIClient openAIClient,
        string deploymentName,
        ILogger<ReviewerAgent>? logger = null)
        : base(openAIClient, deploymentName, logger)
    {
    }

    protected override string GetFallbackPrompt()
    {
        return @"You are a Senior C# Code Reviewer Agent specializing in .NET Framework 4.6.2. Your job is to perform final quality assurance on the generated codebase.

Your responsibilities:
1. Verify all requirements are implemented
2. Check code quality and maintainability
3. Identify potential bugs or edge cases
4. Suggest improvements (optional, not blocking)
5. Provide final approval or rejection

Review Criteria:
- Correctness: Does the code do what it's supposed to do?
- Completeness: Are all requirements addressed?
- Code Quality: Is the code clean, readable, and maintainable?
- Error Handling: Are exceptions handled appropriately?
- .NET Framework 4.6.2 Compatibility: Does it use only compatible APIs?
- Security: Any obvious security issues?
- Performance: Any obvious performance issues?

C# Specific Checks:
- Proper disposal of IDisposable resources
- Null reference safety
- Proper async/await usage
- Thread safety if applicable
- Correct use of access modifiers

Output Format (JSON):
{
    ""approved"": true/false,
    ""summary"": ""Brief overall assessment"",
    ""requirements_met"": [""List of requirements that are implemented""],
    ""issues"": [
        {
            ""severity"": ""critical/major/minor"",
            ""file"": ""filename"",
            ""description"": ""What the issue is"",
            ""suggestion"": ""How to fix it""
        }
    ],
    ""improvements"": [""Optional suggestions for future improvements""],
    ""final_verdict"": ""Ready for use / Needs fixes / Major rework needed""
}

Be thorough but fair. Minor style issues shouldn't block approval if the code works correctly.";
    }

    protected override string GetSystemPrompt()
    {
        return base.GetSystemPrompt();
    }

    public override async Task<AgentResponse> RunAsync(AgentRequest request, CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("[Reviewer] Starting code review");

        try
        {
            // Build the codebase summary
            var codebaseSection = new StringBuilder();
            
            if (request.ProjectState?.Codebase.Count > 0)
            {
                foreach (var (filename, code) in request.ProjectState.Codebase)
                {
                    codebaseSection.AppendLine($"\n=== {filename} ===");
                    codebaseSection.AppendLine($"```csharp");
                    codebaseSection.AppendLine(code);
                    codebaseSection.AppendLine("```");
                }
            }
            else if (!string.IsNullOrEmpty(request.Input))
            {
                codebaseSection.AppendLine("```csharp");
                codebaseSection.AppendLine(request.Input);
                codebaseSection.AppendLine("```");
            }

            // Get original story
            var story = request.ProjectState?.Story ?? 
                              request.Context.GetValueOrDefault("story", "Not provided");

            // Get plan summary
            var planSummary = "";
            if (request.ProjectState?.Plan.Count > 0)
            {
                planSummary = "\n\nPLANNED TASKS:\n";
                foreach (var task in request.ProjectState.Plan)
                {
                    var status = task.Status == SubTaskStatus.Completed ? "✓" : "○";
                    planSummary += $"{status} {task.Index}. {task.Title}\n";
                }
            }

            var userPrompt = $@"Please review the following codebase:

ORIGINAL STORY:
{story}
{planSummary}
GENERATED CODEBASE:
{codebaseSection}

Perform a thorough code review and provide your assessment in JSON format.
Check if all requirements are met and identify any issues.";

            var (content, tokens) = await CallLLMAsync(
                BuildSystemPromptWithStandards(request.ProjectState),
                userPrompt,
                temperature: 0.3f,
                maxTokens: 3000,
                cancellationToken);

            _logger?.LogInformation("[Reviewer] Raw response:\n{Content}", content);

            // Parse the review
            var json = ExtractJson(content);
            var review = System.Text.Json.JsonDocument.Parse(json);
            var root = review.RootElement;

            var approved = root.TryGetProperty("approved", out var ap) && ap.GetBoolean();
            var summary = root.TryGetProperty("summary", out var s) ? s.GetString() ?? "" : "";
            var verdict = root.TryGetProperty("final_verdict", out var v) ? v.GetString() ?? "" : "";

            // Count issues by severity
            var criticalCount = 0;
            var majorCount = 0;
            var minorCount = 0;

            if (root.TryGetProperty("issues", out var issues))
            {
                foreach (var issue in issues.EnumerateArray())
                {
                    var severity = issue.TryGetProperty("severity", out var sev) ? sev.GetString() : "";
                    switch (severity?.ToLower())
                    {
                        case "critical": criticalCount++; break;
                        case "major": majorCount++; break;
                        case "minor": minorCount++; break;
                    }
                }
            }

            // Update project state
            if (request.ProjectState != null)
            {
                request.ProjectState.ReviewReport = content;
                request.ProjectState.CurrentPhase = PipelinePhase.Completed;
            }

            LogAction(request.ProjectState, "CodeReview", 
                $"Files: {request.ProjectState?.Codebase.Count ?? 0}", 
                $"Approved: {approved}, Critical: {criticalCount}, Major: {majorCount}");

            _logger?.LogInformation("[Reviewer] Review complete. Approved: {Approved}, Issues: {Critical} critical, {Major} major, {Minor} minor",
                approved, criticalCount, majorCount, minorCount);

            return new AgentResponse
            {
                Success = true,
                Output = content,
                Data = new Dictionary<string, object>
                {
                    ["approved"] = approved,
                    ["summary"] = summary,
                    ["verdict"] = verdict,
                    ["critical_issues"] = criticalCount,
                    ["major_issues"] = majorCount,
                    ["minor_issues"] = minorCount
                },
                TokensUsed = tokens
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[Reviewer] Error during code review");
            return new AgentResponse
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    /// <summary>
    /// Generate a human-readable review report
    /// </summary>
    public string GenerateReport(AgentResponse reviewResponse)
    {
        var sb = new StringBuilder();
        sb.AppendLine("═══════════════════════════════════════════════════════════");
        sb.AppendLine("                    CODE REVIEW REPORT                      ");
        sb.AppendLine("═══════════════════════════════════════════════════════════");
        sb.AppendLine();

        if (reviewResponse.Data.TryGetValue("approved", out var approved))
        {
            var status = (bool)approved ? "✅ APPROVED" : "❌ NOT APPROVED";
            sb.AppendLine($"Status: {status}");
        }

        if (reviewResponse.Data.TryGetValue("verdict", out var verdict))
        {
            sb.AppendLine($"Verdict: {verdict}");
        }

        sb.AppendLine();

        if (reviewResponse.Data.TryGetValue("summary", out var summary))
        {
            sb.AppendLine("Summary:");
            sb.AppendLine($"  {summary}");
        }

        sb.AppendLine();
        sb.AppendLine("Issue Count:");
        sb.AppendLine($"  Critical: {reviewResponse.Data.GetValueOrDefault("critical_issues", 0)}");
        sb.AppendLine($"  Major:    {reviewResponse.Data.GetValueOrDefault("major_issues", 0)}");
        sb.AppendLine($"  Minor:    {reviewResponse.Data.GetValueOrDefault("minor_issues", 0)}");

        sb.AppendLine();
        sb.AppendLine("═══════════════════════════════════════════════════════════");

        return sb.ToString();
    }
}
