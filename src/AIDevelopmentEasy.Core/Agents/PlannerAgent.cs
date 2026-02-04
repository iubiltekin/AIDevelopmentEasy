using System.Collections.Generic;
using System.Text.Json;
using AIDevelopmentEasy.Core.Agents.Base;
using AIDevelopmentEasy.Core.Models;
using AIDevelopmentEasy.Core.Services;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Logging;

namespace AIDevelopmentEasy.Core.Agents;

/// <summary>
/// Planner Agent - Responsible for requirement analysis and task decomposition.
/// Takes user's high-level request and breaks it down into concrete subtasks.
/// 
/// Based on AgentMesh framework (arXiv:2507.19902):
/// "The Planner is responsible for requirement analysis and task decomposition. 
/// It takes the user's high-level request and breaks it down into a structured set of subtasks."
/// 
/// Enhanced with CodebaseAnalysis support for existing codebase context.
/// </summary>
public class PlannerAgent : BaseAgent
{
    public override string Name => "Planner";
    public override string Role => "Software Project Planner - Decomposes requirements into development tasks";
    protected override string? PromptFileName => "planner";

    private readonly CodeAnalysisAgent _codeAnalysisAgent;

    public PlannerAgent(OpenAIClient openAIClient, string deploymentName, CodeAnalysisAgent codeAnalysisAgent, ILogger<PlannerAgent>? logger = null)
        : base(openAIClient, deploymentName, logger)
    {
        _codeAnalysisAgent = codeAnalysisAgent;
    }

    /// <summary>
    /// Plan tasks with codebase analysis context.
    /// This is the preferred method when working with existing codebases.
    /// </summary>
    /// <param name="requirement">The high-level requirement text</param>
    /// <param name="codebaseAnalysis">Analysis of the existing codebase (from CodeAnalysisAgent)</param>
    /// <param name="projectState">Optional project state for coding standards</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>AgentResponse with task list</returns>
    public async Task<AgentResponse> PlanWithCodebaseAsync(
        string requirement,
        CodebaseAnalysis codebaseAnalysis,
        ProjectState? projectState = null,
        CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("[Planner] Planning with codebase context: {Codebase}", codebaseAnalysis.CodebaseName);

        // Generate context string from analysis
        var codebaseContext = _codeAnalysisAgent.GenerateContextForPrompt(codebaseAnalysis);

        var languages = codebaseAnalysis.Summary.Languages ?? new List<string>();
        var languagesStr = languages.Count > 0 ? string.Join(", ", languages) : "csharp";

        var request = new AgentRequest
        {
            Input = requirement,
            ProjectState = projectState,
            Context = new Dictionary<string, string>
            {
                ["codebase_context"] = codebaseContext,
                ["codebase_name"] = codebaseAnalysis.CodebaseName,
                ["primary_framework"] = codebaseAnalysis.Summary.PrimaryFramework,
                ["languages"] = languagesStr,
                ["test_framework"] = codebaseAnalysis.Conventions.TestFramework ?? "NUnit",
                ["private_field_prefix"] = codebaseAnalysis.Conventions.PrivateFieldPrefix
            }
        };

        _logger?.LogInformation("[Planner] Codebase languages: {Languages} – plan tasks with matching file extensions", languagesStr);

        return await RunAsync(request, cancellationToken);
    }

    /// <summary>
    /// Analyze a codebase path and then plan tasks.
    /// Convenience method that combines CodeAnalysisAgent and planning.
    /// </summary>
    public async Task<AgentResponse> AnalyzeAndPlanAsync(
        string requirement,
        string codebasePath,
        string codebaseName,
        ProjectState? projectState = null,
        CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("[Planner] Analyzing codebase at: {Path}", codebasePath);

        // First, analyze the codebase
        var analysis = await _codeAnalysisAgent.AnalyzeAsync(codebasePath, codebaseName, cancellationToken);

        // Then plan with the analysis
        return await PlanWithCodebaseAsync(requirement, analysis, projectState, cancellationToken);
    }

    public override async Task<AgentResponse> RunAsync(AgentRequest request, CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("[Planner] Starting task decomposition for: {Input}",
            request.Input.Length > 100 ? request.Input[..100] + "..." : request.Input);

        try
        {
            // Check if we have codebase context
            var codebaseContext = "";
            var hasCodebaseContext = false;

            if (request.Context?.TryGetValue("codebase_context", out var ctx) == true && !string.IsNullOrEmpty(ctx))
            {
                hasCodebaseContext = true;
                var codebaseName = request.Context.TryGetValue("codebase_name", out var name) ? name : "existing codebase";
                var testFramework = request.Context.TryGetValue("test_framework", out var tf) ? tf : "NUnit";
                var fieldPrefix = request.Context.TryGetValue("private_field_prefix", out var fp) ? fp : "_";
                codebaseContext = PromptLoader.Instance.LoadPromptRequired("planner-codebase-context", new Dictionary<string, string>
                {
                    ["CODEBASE_NAME"] = codebaseName,
                    ["CONTEXT"] = ctx,
                    ["TEST_FRAMEWORK"] = testFramework,
                    ["PRIVATE_FIELD_PREFIX"] = fieldPrefix
                });
                _logger?.LogInformation("[Planner] Using codebase context for planning: {Name}", codebaseName);
            }

            var userPrompt = hasCodebaseContext
                ? BuildUserPromptCodebase(request.Input, codebaseContext)
                : BuildUserPromptStandalone(request.Input);

            var baseSystemPrompt = PromptLoader.Instance.LoadPromptRequired("planner");
            if (hasCodebaseContext && request.Context?.TryGetValue("languages", out var languagesStr) == true && !string.IsNullOrEmpty(languagesStr))
            {
                var primaryLang = languagesStr.Split(',')[0].Trim().ToLowerInvariant();
                if (primaryLang is "c#" or "csharp") primaryLang = "csharp";
                if (primaryLang == "typescript") primaryLang = "react"; // React/TS frontend uses planner-react
                if (PromptLoader.Instance.PromptExists($"planner-{primaryLang}"))
                    baseSystemPrompt += "\n\n" + PromptLoader.Instance.LoadPromptRequired($"planner-{primaryLang}");
            }

            // When codebase has a DB migrator (e.g. psqlmigrations, scripts/migrator), add migration planner rules
            var hasMigrator = request.Context?.TryGetValue("has_migrator", out var migratorVal) == true && migratorVal == "true";
            if (!hasMigrator && hasCodebaseContext && request.Context?.TryGetValue("codebase_context", out var ctxRaw) == true && !string.IsNullOrEmpty(ctxRaw))
                hasMigrator = ctxRaw.Contains("migrator", StringComparison.OrdinalIgnoreCase) &&
                    (ctxRaw.Contains("psqlmigrations", StringComparison.OrdinalIgnoreCase) ||
                     ctxRaw.Contains("migration_path", StringComparison.OrdinalIgnoreCase) ||
                     ctxRaw.Contains("/migrations/", StringComparison.OrdinalIgnoreCase));
            if (hasMigrator && PromptLoader.Instance.PromptExists("planner-migrator"))
                baseSystemPrompt += "\n\n" + PromptLoader.Instance.LoadPromptRequired("planner-migrator");

            var (content, tokens) = await CallLLMAsync(
                BuildSystemPromptWithStandards(request.ProjectState, baseSystemPrompt),
                userPrompt,
                temperature: 0.3f,
                maxTokens: 4000,
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

                    // Parse project name for multi-project support
                    var taskProject = taskEl.TryGetProperty("project", out var proj) ? proj.GetString() : null;

                    // Parse dependencies
                    var dependsOn = new List<int>();
                    if (taskEl.TryGetProperty("depends_on", out var deps))
                    {
                        foreach (var dep in deps.EnumerateArray())
                        {
                            if (dep.TryGetInt32(out var depIdx))
                                dependsOn.Add(depIdx);
                        }
                    }

                    // Parse existing classes to use
                    var usesExisting = new List<string>();
                    if (taskEl.TryGetProperty("uses_existing", out var uses))
                    {
                        foreach (var use in uses.EnumerateArray())
                        {
                            var useName = use.GetString();
                            if (!string.IsNullOrEmpty(useName))
                                usesExisting.Add(useName);
                        }
                    }

                    // Parse namespace (CRITICAL for correct code generation)
                    var taskNamespace = taskEl.TryGetProperty("namespace", out var ns) ? ns.GetString() : null;

                    // Parse modification_type (create vs modify) - LLM infers from requirement and codebase context
                    var modType = taskEl.TryGetProperty("modification_type", out var mt) ? mt.GetString() : null;
                    var isModification = string.Equals(modType, "modify", StringComparison.OrdinalIgnoreCase);

                    tasks.Add(new SubTask
                    {
                        Index = taskEl.TryGetProperty("index", out var idx) ? idx.GetInt32() : tasks.Count + 1,
                        Title = taskEl.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "",
                        Description = taskEl.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "",
                        TargetFiles = targetFiles,
                        ProjectName = taskProject,
                        DependsOn = dependsOn,
                        UsesExisting = usesExisting,
                        Namespace = taskNamespace,
                        IsModification = isModification
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

    private string BuildUserPromptCodebase(string requirement, string codebaseContext)
    {
        return PromptLoader.Instance.LoadPromptRequired("planner-user-codebase", new Dictionary<string, string>
        {
            ["REQUIREMENT"] = requirement,
            ["CODEBASE_CONTEXT"] = codebaseContext
        });
    }

    private string BuildUserPromptStandalone(string requirement)
    {
        return PromptLoader.Instance.LoadPromptRequired("planner-user-standalone", new Dictionary<string, string>
        {
            ["REQUIREMENT"] = requirement
        });
    }

    /// <summary>
    /// Plan modifications to existing files based on class and reference analysis.
    /// This is used when modifying an existing class and updating all its references.
    /// </summary>
    public async Task<AgentResponse> PlanModificationAsync(
        string requirement,
        ClassSearchResult classResult,
        ReferenceSearchResult referenceResult,
        List<FileModificationTask> modificationTasks,
        CodebaseAnalysis? codebaseAnalysis = null,
        CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("[Planner] Planning modifications for class: {ClassName} with {RefCount} references",
            classResult.ClassName, referenceResult.References.Count);

        var modificationContext = _codeAnalysisAgent.GenerateModificationContext(classResult, referenceResult, modificationTasks);

        // Add codebase context if available
        var codebaseContext = codebaseAnalysis != null
            ? _codeAnalysisAgent.GenerateContextForPrompt(codebaseAnalysis)
            : "";

        var codebaseSection = string.IsNullOrEmpty(codebaseContext)
            ? ""
            : "\n# CODEBASE CONTEXT\n\n" + codebaseContext + "\n\n";
        var userPrompt = PromptLoader.Instance.LoadPromptRequired("planner-modification-user", new Dictionary<string, string>
        {
            ["REQUIREMENT"] = requirement,
            ["MODIFICATION_CONTEXT"] = modificationContext,
            ["CODEBASE_SECTION"] = codebaseSection
        });

        var (content, tokens) = await CallLLMAsync(
            PromptLoader.Instance.LoadPromptRequired("planner-modification"),
            userPrompt,
            temperature: 0.3f,
            maxTokens: 8000,
            cancellationToken);

        _logger?.LogInformation("[Planner] Modification plan response:\n{Content}", content);

        try
        {
            var json = ExtractJson(content);
            var plan = JsonDocument.Parse(json);
            var root = plan.RootElement;

            var projectName = root.TryGetProperty("project_name", out var pn) ? pn.GetString() ?? "Modification" : "Modification";
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

                    var taskProject = taskEl.TryGetProperty("project", out var proj) ? proj.GetString() : null;
                    var modType = taskEl.TryGetProperty("modification_type", out var mt) ? mt.GetString() : "modify";

                    var dependsOn = new List<int>();
                    if (taskEl.TryGetProperty("depends_on", out var deps))
                    {
                        foreach (var dep in deps.EnumerateArray())
                        {
                            if (dep.TryGetInt32(out var depIdx))
                                dependsOn.Add(depIdx);
                        }
                    }

                    var usesExisting = new List<string>();
                    if (taskEl.TryGetProperty("uses_existing", out var uses))
                    {
                        foreach (var use in uses.EnumerateArray())
                        {
                            var useName = use.GetString();
                            if (!string.IsNullOrEmpty(useName))
                                usesExisting.Add(useName);
                        }
                    }

                    // Parse namespace (CRITICAL for correct code generation)
                    var taskNamespace = taskEl.TryGetProperty("namespace", out var ns) ? ns.GetString() : null;

                    tasks.Add(new SubTask
                    {
                        Index = taskEl.TryGetProperty("index", out var idx) ? idx.GetInt32() : tasks.Count + 1,
                        Title = taskEl.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "",
                        Description = taskEl.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "",
                        TargetFiles = targetFiles,
                        ProjectName = taskProject,
                        DependsOn = dependsOn,
                        UsesExisting = usesExisting,
                        IsModification = modType?.ToLower() == "modify",
                        Namespace = taskNamespace
                    });
                }
            }

            LogAction(null, "ModificationPlanning", requirement, $"{tasks.Count} modification tasks created");

            _logger?.LogInformation("[Planner] Created {Count} modification tasks", tasks.Count);

            return new AgentResponse
            {
                Success = true,
                Output = content,
                Data = new Dictionary<string, object>
                {
                    ["project_name"] = projectName,
                    ["summary"] = summary,
                    ["tasks"] = tasks,
                    ["is_modification_plan"] = true,
                    ["class_result"] = classResult,
                    ["reference_result"] = referenceResult,
                    ["modification_tasks"] = modificationTasks
                },
                TokensUsed = tokens
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[Planner] Error parsing modification plan");
            return new AgentResponse
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

}
