using System.Text.Json;
using AIDevelopmentEasy.Core.Agents.Base;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Logging;

namespace AIDevelopmentEasy.Core.Agents;

/// <summary>
/// Planner Agent - Responsible for requirement analysis and task decomposition.
/// Takes user's high-level request and breaks it down into concrete subtasks.
/// </summary>
public class PlannerAgent : BaseAgent
{
    public override string Name => "Planner";
    public override string Role => "Software Project Planner - Decomposes requirements into development tasks";
    protected override string? PromptFileName => "planner";

    public PlannerAgent(OpenAIClient openAIClient, string deploymentName, ILogger<PlannerAgent>? logger = null)
        : base(openAIClient, deploymentName, logger)
    {
    }

    protected override string GetFallbackPrompt()
    {
        return @"You are a Software Project Planner Agent specializing in C# and .NET Framework 4.6.2 development. Your job is to analyze user requirements and decompose them into concrete, implementable development tasks.

Your responsibilities:
1. Understand the high-level requirement thoroughly
2. Break it down into small, manageable subtasks
3. Order tasks by dependency (what needs to be done first)
4. Each task should be specific enough for a developer to implement

Guidelines:
- Each subtask should be completable in 1-2 hours of coding
- Include both implementation and testing tasks
- Consider edge cases and error handling
- Think about the class/file structure (.cs files)
- Use .NET Framework 4.6.2 compatible approaches

.NET Framework 4.6.2 Considerations:
- Use Console Application template for CLI apps
- Use Newtonsoft.Json for JSON serialization
- Use System.IO for file operations
- Use Task-based async patterns
- Consider separating concerns into different classes

Testing Strategy (as defined in coding standards):
- Use NUnit framework for unit tests (NUnit.Framework)
- Use FluentAssertions for readable assertions
- Create test classes with [TestFixture] attribute
- Create test methods with [Test] attribute
- Use [TestCase] for parameterized tests
- Use Arrange-Act-Assert pattern
- Method naming: MethodName_Scenario_ExpectedResult
- Keep task count reasonable (5-8 tasks max)
- Last task should be a console demo application (Program.cs with Main method)

Output Format (JSON):
{
    ""project_name"": ""Short project name"",
    ""summary"": ""Brief summary of what will be built"",
    ""tasks"": [
        {
            ""index"": 1,
            ""title"": ""Short descriptive title"",
            ""description"": ""Detailed description of what to implement"",
            ""target_files"": [""ClassName.cs""]
        }
    ]
}

IMPORTANT: Output ONLY valid JSON, no explanations before or after.";
    }

    protected override string GetSystemPrompt()
    {
        return base.GetSystemPrompt();
    }

    public override async Task<AgentResponse> RunAsync(AgentRequest request, CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("[Planner] Starting task decomposition for: {Input}",
            request.Input.Length > 100 ? request.Input[..100] + "..." : request.Input);

        try
        {
            var userPrompt = $@"Please analyze the following requirement and create a development plan:

REQUIREMENT:
{request.Input}

Break this down into specific development tasks. Consider:
- What data structures/models are needed?
- What functions/methods need to be implemented?
- What error handling is required?
- What tests should be written?

Output the plan as JSON.";

            var (content, tokens) = await CallLLMAsync(
                BuildSystemPromptWithStandards(request.ProjectState),
                userPrompt,
                temperature: 0.3f,
                maxTokens: 3000,
                cancellationToken);

            _logger?.LogInformation("[Planner] Raw response:\n{Content}", content);

            // Parse the response
            var json = ExtractJson(content);
            var plan = JsonDocument.Parse(json);
            var root = plan.RootElement;

            var projectName = root.TryGetProperty("project_name", out var pn) ? pn.GetString() ?? "Project" : "Project";
            var summary = root.TryGetProperty("summary", out var s) ? s.GetString() ?? "" : "";

            var tasks = new List<SubTask>();
            if (root.TryGetProperty("tasks", out var tasksArray))
            {
                foreach (var taskEl in tasksArray.EnumerateArray())
                {
                    var targetFiles = new List<string>();
                    if (taskEl.TryGetProperty("target_files", out var files))
                    {
                        foreach (var f in files.EnumerateArray())
                        {
                            var fileName = f.GetString();
                            if (!string.IsNullOrEmpty(fileName))
                                targetFiles.Add(fileName);
                        }
                    }

                    tasks.Add(new SubTask
                    {
                        Index = taskEl.TryGetProperty("index", out var idx) ? idx.GetInt32() : tasks.Count + 1,
                        Title = taskEl.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "",
                        Description = taskEl.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "",
                        TargetFiles = targetFiles
                    });
                }
            }

            // Update project state if provided
            if (request.ProjectState != null)
            {
                request.ProjectState.ProjectName = projectName;
                request.ProjectState.Plan = tasks;
                request.ProjectState.CurrentPhase = PipelinePhase.Coding;
            }

            LogAction(request.ProjectState, "TaskDecomposition", request.Input, $"{tasks.Count} tasks created");

            _logger?.LogInformation("[Planner] Created {Count} tasks for project: {Name}", tasks.Count, projectName);

            return new AgentResponse
            {
                Success = true,
                Output = content,
                Data = new Dictionary<string, object>
                {
                    ["project_name"] = projectName,
                    ["summary"] = summary,
                    ["tasks"] = tasks
                },
                TokensUsed = tokens
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[Planner] Error during task decomposition");
            return new AgentResponse
            {
                Success = false,
                Error = ex.Message
            };
        }
    }
}
