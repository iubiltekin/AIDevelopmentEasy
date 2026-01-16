using System.Text.Json;
using AIDevelopmentEasy.Core.Agents.Base;
using AIDevelopmentEasy.Core.Models;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Logging;

namespace AIDevelopmentEasy.Core.Agents;

/// <summary>
/// Multi-Project Planner Agent - Creates tasks for requirements affecting multiple projects.
/// Handles dependency ordering and phase-based execution.
/// </summary>
public class MultiProjectPlannerAgent : BaseAgent
{
    public override string Name => "MultiProjectPlanner";
    public override string Role => "Multi-Project Planner - Plans tasks across multiple projects with dependencies";
    protected override string? PromptFileName => "multi-project-planner";

    public MultiProjectPlannerAgent(OpenAIClient openAIClient, string deploymentName, ILogger<MultiProjectPlannerAgent>? logger = null)
        : base(openAIClient, deploymentName, logger)
    {
    }

    protected override string GetFallbackPrompt()
    {
        return @"You are a Multi-Project Software Planner Agent specializing in C# and .NET Framework 4.6.2 development.
Your job is to create development tasks for a requirement that affects MULTIPLE projects.

IMPORTANT RULES:
1. Each project has its OWN test project (e.g., Picus.Common has Picus.Common.Tests)
2. Core/library projects are implemented FIRST (Phase 1)
3. Consumer projects are implemented AFTER their dependencies (Phase 2+)
4. Test projects are implemented AFTER their target project within the same phase
5. Integration/final build is the LAST phase

For each task, specify:
- project: Which project this task belongs to
- phase: Execution phase (1 = core, 2 = consumers, 3 = integration)
- depends_on_projects: List of projects that must be completed first
- uses_classes: Classes from other projects that this code will use

Output Format (JSON):
{
    ""phases"": [
        {
            ""phase"": 1,
            ""name"": ""Core Implementation"",
            ""projects"": [""Picus.Common"", ""Picus.Common.Tests""],
            ""tasks"": [
                {
                    ""index"": 1,
                    ""project"": ""Picus.Common"",
                    ""title"": ""Implement LogRotator class"",
                    ""description"": ""Create the core log rotation functionality..."",
                    ""target_files"": [""Helpers/LogRotator.cs""],
                    ""depends_on_projects"": [],
                    ""uses_classes"": []
                },
                {
                    ""index"": 2,
                    ""project"": ""Picus.Common.Tests"",
                    ""title"": ""Write LogRotator unit tests"",
                    ""description"": ""Create comprehensive unit tests..."",
                    ""target_files"": [""Helpers/LogRotatorTests.cs""],
                    ""depends_on_projects"": [""Picus.Common""],
                    ""uses_classes"": [""LogRotator""]
                }
            ]
        },
        {
            ""phase"": 2,
            ""name"": ""Consumer Implementation"",
            ""projects"": [""Picus.Agent"", ""Picus.Agent.Tests""],
            ""tasks"": [...]
        }
    ]
}

IMPORTANT: Output ONLY valid JSON, no explanations before or after.";
    }

    protected override string GetSystemPrompt()
    {
        return base.GetSystemPrompt();
    }

    /// <summary>
    /// Create tasks for a multi-project requirement
    /// </summary>
    public async Task<AgentResponse> PlanMultiProjectAsync(
        MultiProjectRequirement requirement,
        ProjectState? projectState,
        CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("[MultiProjectPlanner] Planning for: {Title}", requirement.Title);

        try
        {
            // Build the prompt with project structure info
            var projectInfo = BuildProjectInfo(requirement);
            
            var userPrompt = $@"Please create a detailed development plan for the following multi-project requirement:

REQUIREMENT:
{requirement.Title}

{requirement.Description}

AFFECTED PROJECTS AND THEIR STRUCTURE:
{projectInfo}

Create tasks organized by phases. Remember:
1. Core projects (role: 'core') go in Phase 1
2. Their test projects go in Phase 1 too, but AFTER the implementation
3. Consumer projects (role: 'consumer') go in Phase 2
4. Their test projects go in Phase 2 too, but AFTER the implementation
5. Each test project tests ONLY its corresponding main project

Output the plan as JSON with phases and tasks.";

            var (content, tokens) = await CallLLMAsync(
                BuildSystemPromptWithStandards(projectState),
                userPrompt,
                temperature: 0.3f,
                maxTokens: 4000,
                cancellationToken);

            _logger?.LogInformation("[MultiProjectPlanner] Raw response:\n{Content}", content);

            // Parse the response
            var json = ExtractJson(content);
            var phases = ParsePhases(json);

            _logger?.LogInformation("[MultiProjectPlanner] Created {PhaseCount} phases with {TaskCount} total tasks",
                phases.Count, phases.Sum(p => p.Tasks.Count));

            return new AgentResponse
            {
                Success = true,
                Output = content,
                Data = new Dictionary<string, object>
                {
                    ["phases"] = phases,
                    ["requirement"] = requirement
                },
                TokensUsed = tokens
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[MultiProjectPlanner] Error during planning");
            return new AgentResponse
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    private string BuildProjectInfo(MultiProjectRequirement requirement)
    {
        var sb = new System.Text.StringBuilder();

        // Group by order/phase
        var grouped = requirement.AffectedProjects
            .GroupBy(p => p.Order)
            .OrderBy(g => g.Key);

        foreach (var group in grouped)
        {
            sb.AppendLine($"Phase {group.Key}:");
            foreach (var project in group)
            {
                sb.AppendLine($"  - {project.Name}");
                sb.AppendLine($"    Role: {project.Role}");
                sb.AppendLine($"    Type: {project.Type}");
                if (project.DependsOn.Count > 0)
                    sb.AppendLine($"    Depends on: {string.Join(", ", project.DependsOn)}");
                if (!string.IsNullOrEmpty(project.TestProject))
                    sb.AppendLine($"    Test project: {project.TestProject}");
                if (project.Outputs.Count > 0)
                {
                    sb.AppendLine($"    Expected outputs:");
                    foreach (var output in project.Outputs)
                    {
                        sb.AppendLine($"      - {output.File} ({output.Type})");
                        if (output.Uses.Count > 0)
                            sb.AppendLine($"        Uses: {string.Join(", ", output.Uses)}");
                    }
                }
            }
        }

        return sb.ToString();
    }

    private List<ExecutionPhase> ParsePhases(string json)
    {
        var phases = new List<ExecutionPhase>();
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.TryGetProperty("phases", out var phasesArray))
        {
            foreach (var phaseEl in phasesArray.EnumerateArray())
            {
                var phase = new ExecutionPhase
                {
                    Order = phaseEl.TryGetProperty("phase", out var p) ? p.GetInt32() : phases.Count + 1,
                    Name = phaseEl.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "",
                    Description = phaseEl.TryGetProperty("description", out var d) ? d.GetString() ?? "" : ""
                };

                if (phaseEl.TryGetProperty("tasks", out var tasksArray))
                {
                    foreach (var taskEl in tasksArray.EnumerateArray())
                    {
                        var task = new ProjectSubTask
                        {
                            Index = taskEl.TryGetProperty("index", out var idx) ? idx.GetInt32() : phase.Tasks.Count + 1,
                            ProjectName = taskEl.TryGetProperty("project", out var proj) ? proj.GetString() ?? "" : "",
                            Title = taskEl.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "",
                            Description = taskEl.TryGetProperty("description", out var desc) ? desc.GetString() ?? "" : "",
                            Phase = phase.Order
                        };

                        // Parse target files
                        if (taskEl.TryGetProperty("target_files", out var files))
                        {
                            foreach (var f in files.EnumerateArray())
                            {
                                var fileName = f.GetString();
                                if (!string.IsNullOrEmpty(fileName))
                                    task.TargetFiles.Add(fileName);
                            }
                        }

                        // Parse depends on projects
                        if (taskEl.TryGetProperty("depends_on_projects", out var deps))
                        {
                            foreach (var dep in deps.EnumerateArray())
                            {
                                var depName = dep.GetString();
                                if (!string.IsNullOrEmpty(depName))
                                    task.DependsOnProjects.Add(depName);
                            }
                        }

                        // Parse uses classes
                        if (taskEl.TryGetProperty("uses_classes", out var uses))
                        {
                            foreach (var use in uses.EnumerateArray())
                            {
                                var className = use.GetString();
                                if (!string.IsNullOrEmpty(className))
                                    task.UsesClasses.Add(className);
                            }
                        }

                        phase.Tasks.Add(task);
                    }
                }

                phases.Add(phase);
            }
        }

        return phases;
    }

    public override Task<AgentResponse> RunAsync(AgentRequest request, CancellationToken cancellationToken = default)
    {
        // For backward compatibility, delegate to single-project planning
        throw new NotSupportedException("Use PlanMultiProjectAsync for multi-project requirements");
    }
}
