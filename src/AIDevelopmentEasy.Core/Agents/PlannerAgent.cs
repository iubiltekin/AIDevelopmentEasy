using System.Text.Json;
using AIDevelopmentEasy.Core.Agents.Base;
using AIDevelopmentEasy.Core.Models;
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

        var request = new AgentRequest
        {
            Input = requirement,
            ProjectState = projectState,
            Context = new Dictionary<string, string>
            {
                ["codebase_context"] = codebaseContext,
                ["codebase_name"] = codebaseAnalysis.CodebaseName,
                ["primary_framework"] = codebaseAnalysis.Summary.PrimaryFramework,
                ["test_framework"] = codebaseAnalysis.Conventions.TestFramework ?? "NUnit",
                ["private_field_prefix"] = codebaseAnalysis.Conventions.PrivateFieldPrefix
            }
        };

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

    protected override string GetFallbackPrompt()
    {
        return @"You are a **Software Project Planner Agent** based on the AgentMesh framework.

Your role is requirement analysis and task decomposition - taking high-level requirements and breaking them down into concrete, implementable development tasks.

## Core Responsibilities

1. **Requirement Analysis**: Understand the high-level requirement thoroughly
2. **Task Decomposition**: Break it down into small, manageable subtasks
3. **Dependency Ordering**: Order tasks by dependency (what needs to be done first)
4. **Codebase Integration**: When given existing codebase context, plan tasks that integrate properly

## Guidelines

- Each subtask should be completable in 1-2 hours of coding
- Include both implementation and testing tasks
- Consider edge cases and error handling
- Think about the class/file structure
- Keep task count reasonable (5-10 tasks max)

## When Working with Existing Codebases

If codebase context is provided:

1. **Project Placement**: Identify which existing project(s) should contain the new code
2. **Folder Structure**: Use the EXACT folder paths shown in the codebase context
3. **Pattern Consistency**: Use the same patterns detected in the codebase (Repository, Service, Helper, etc.)
4. **Convention Following**: Follow detected naming conventions (field prefixes, async suffixes, etc.)
5. **Namespace Matching**: New classes should use namespaces matching their folder location
6. **Test Structure**: Place tests in the corresponding UnitTest project using the same folder structure

## File Placement Rules (CRITICAL)

When adding new code to an existing codebase:

- **Helper classes**: [ProjectName]/Helpers/[HelperName].cs → namespace [RootNamespace].Helpers
- **Service classes**: [ProjectName]/Services/[ServiceName].cs → namespace [RootNamespace].Services
- **Model classes**: [ProjectName]/Models/[ModelName].cs → namespace [RootNamespace].Models
- **Unit tests**: Tests/[ProjectName].UnitTest/[ClassName]Tests.cs

## Testing Strategy

- Use the test framework detected in the codebase (NUnit, xUnit, or MSTest)
- Use FluentAssertions for readable assertions when available
- Use Arrange-Act-Assert pattern
- Method naming: MethodName_Scenario_ExpectedResult

## Output Format (JSON)

### CRITICAL: Namespace Requirement
- Every task MUST include a ""namespace"" field
- The namespace determines where the file will be placed in the project
- Format: ProjectName.FolderName (e.g., Picus.Common.Helpers)

### For Standalone Projects (no codebase context):

{
    ""project_name"": ""Short project name"",
    ""summary"": ""Brief summary of what will be built"",
    ""tasks"": [
        {
            ""index"": 1,
            ""title"": ""Short descriptive title"",
            ""description"": ""Detailed description of what to implement"",
            ""target_files"": [""ClassName.cs""],
            ""namespace"": ""ProjectName""
        }
    ]
}

### For Existing Codebase Integration:

{
    ""project_name"": ""Feature name"",
    ""summary"": ""Brief description"",
    ""tasks"": [
        {
            ""index"": 1,
            ""project"": ""ProjectName"",
            ""title"": ""Create helper class"",
            ""description"": ""Detailed implementation description"",
            ""target_files"": [""Helpers/HelperName.cs""],
            ""namespace"": ""ProjectName.Helpers"",
            ""depends_on"": [],
            ""uses_existing"": []
        },
        {
            ""index"": 2,
            ""project"": ""ProjectName.UnitTest"",
            ""title"": ""Unit tests"",
            ""description"": ""Write unit tests"",
            ""target_files"": [""Helpers/HelperNameTests.cs""],
            ""namespace"": ""ProjectName.UnitTest.Helpers"",
            ""depends_on"": [1]
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
            // Check if we have codebase context
            var codebaseContext = "";
            var hasCodebaseContext = false;

            if (request.Context?.TryGetValue("codebase_context", out var ctx) == true && !string.IsNullOrEmpty(ctx))
            {
                hasCodebaseContext = true;
                var codebaseName = request.Context.TryGetValue("codebase_name", out var name) ? name : "existing codebase";
                var testFramework = request.Context.TryGetValue("test_framework", out var tf) ? tf : "NUnit";
                var fieldPrefix = request.Context.TryGetValue("private_field_prefix", out var fp) ? fp : "_";

                codebaseContext = $@"

## EXISTING CODEBASE: {codebaseName}

{ctx}

## INTEGRATION GUIDELINES

When creating tasks for this existing codebase:

1. **Project Placement**: Identify which existing project(s) should contain the new code
2. **Namespace Convention**: Follow existing namespace patterns (e.g., ProjectName.SubFolder)
3. **Patterns**: Use detected patterns (Repository, Service, Helper, etc.)
4. **Conventions**: 
   - Private fields: {fieldPrefix}fieldName
   - Test framework: {testFramework}
5. **Dependencies**: Reference existing interfaces and base classes
6. **Test Projects**: Place tests in corresponding *.Tests project

## TASK STRUCTURE FOR MULTI-PROJECT

For each task, specify:
- **project**: Which project this task belongs to
- **target_files**: File paths within that project (e.g., ""Helpers/LogRotator.cs"")
- **depends_on**: Tasks that must complete first (by index)
- **uses_existing**: Existing classes/interfaces to use or extend
";
                _logger?.LogInformation("[Planner] Using codebase context for planning: {Name}", codebaseName);
            }

            var userPrompt = hasCodebaseContext
                ? BuildCodebaseAwarePrompt(request.Input, codebaseContext)
                : BuildStandalonePrompt(request.Input);

            var (content, tokens) = await CallLLMAsync(
                BuildSystemPromptWithStandards(request.ProjectState),
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

                    tasks.Add(new SubTask
                    {
                        Index = taskEl.TryGetProperty("index", out var idx) ? idx.GetInt32() : tasks.Count + 1,
                        Title = taskEl.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "",
                        Description = taskEl.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "",
                        TargetFiles = targetFiles,
                        ProjectName = taskProject,
                        DependsOn = dependsOn,
                        UsesExisting = usesExisting,
                        Namespace = taskNamespace
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

    /// <summary>
    /// Build prompt for planning with existing codebase context
    /// </summary>
    private string BuildCodebaseAwarePrompt(string requirement, string codebaseContext)
    {
        return $@"Please analyze the following requirement and create a development plan that integrates with the existing codebase.

# REQUIREMENT
{requirement}

{codebaseContext}

# INSTRUCTIONS

1. Analyze how this requirement fits into the existing codebase structure
2. Identify which projects need modifications
3. Create tasks that follow existing patterns and conventions
4. Order tasks by dependency (core implementations first, then consumers, then tests)
5. Each task MUST specify the target project, files, AND namespace

## CRITICAL: Namespace Convention
- For each task, you MUST specify the FULL namespace
- The namespace follows the pattern: ProjectName.SubFolder (e.g., Picus.Common.Helpers)
- Look at existing classes in the codebase to determine the correct namespace
- The namespace determines where the file will be placed in the project

Output the plan as JSON with the following structure:
{{
    ""project_name"": ""Feature name or module name"",
    ""summary"": ""Brief description of what will be implemented"",
    ""tasks"": [
        {{
            ""index"": 1,
            ""project"": ""ProjectName"",
            ""title"": ""Task title"",
            ""description"": ""Detailed implementation description"",
            ""target_files"": [""Folder/FileName.cs""],
            ""namespace"": ""ProjectName.Folder"",
            ""depends_on"": [],
            ""uses_existing"": [""ExistingClass"", ""IExistingInterface""]
        }}
    ]
}}

Example for a DateTimeHelper class in Picus.Common project:
{{
    ""index"": 1,
    ""project"": ""Picus.Common"",
    ""title"": ""Create DateTimeHelper class"",
    ""description"": ""Create DateTimeHelper class with AddDays method"",
    ""target_files"": [""Helpers/DateTimeHelper.cs""],
    ""namespace"": ""Picus.Common.Helpers"",
    ""depends_on"": [],
    ""uses_existing"": []
}}";
    }

    /// <summary>
    /// Build prompt for standalone planning (no existing codebase)
    /// </summary>
    private string BuildStandalonePrompt(string requirement)
    {
        return $@"Please analyze the following requirement and create a development plan:

# REQUIREMENT
{requirement}

# INSTRUCTIONS

Break this down into specific development tasks. Consider:
- What data structures/models are needed?
- What functions/methods need to be implemented?
- What error handling is required?
- What tests should be written?

## CRITICAL: Namespace Requirement
- For each task, you MUST specify the full namespace
- The namespace should match the folder structure (e.g., MyProject.Helpers for Helpers/ folder)

Output the plan as JSON with the following structure:
{{
    ""project_name"": ""Short project name"",
    ""summary"": ""Brief summary of what will be built"",
    ""tasks"": [
        {{
            ""index"": 1,
            ""title"": ""Short descriptive title"",
            ""description"": ""Detailed description of what to implement"",
            ""target_files"": [""ClassName.cs""],
            ""namespace"": ""ProjectName.FolderName""
        }}
    ]
}}";
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

        var userPrompt = BuildModificationPrompt(requirement, modificationContext, codebaseContext);

        var (content, tokens) = await CallLLMAsync(
            BuildModificationSystemPrompt(),
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

    private string BuildModificationSystemPrompt()
    {
        return @"You are a **Code Modification Planner Agent** specializing in refactoring and updating existing codebases.

Your job is to analyze a requirement that affects an existing class and create a detailed plan for:
1. Modifying the target class itself
2. Updating all files that reference this class

## Key Principles

1. **Preserve Existing Behavior**: Modifications should not break existing functionality
2. **Incremental Changes**: Order tasks so changes propagate correctly
3. **Reference Awareness**: Understand how changes to one file affect others
4. **File Integrity**: Always output the COMPLETE modified file, not just the changes

## Task Types

- **modify**: Modify an existing file (keep all existing code, add/change specific parts)
- **create**: Create a new file (only when absolutely necessary)

## Modification Task Structure

For each task provide:
- **modification_type**: 'modify' or 'create'
- **target_files**: The file path(s) to modify
- **description**: Detailed description including:
  - What specific changes to make
  - Which methods/properties to update
  - How to handle the existing code

## Output Format (JSON)

```json
{
    ""project_name"": ""Feature/Change name"",
    ""summary"": ""What this modification achieves"",
    ""tasks"": [
        {
            ""index"": 1,
            ""project"": ""ProjectName"",
            ""modification_type"": ""modify"",
            ""title"": ""Modify ClassName - add new method"",
            ""description"": ""Add NewMethod() to ClassName. Keep all existing methods intact. The new method should..."",
            ""target_files"": [""Path/ClassName.cs""],
            ""depends_on"": [],
            ""uses_existing"": [""ExistingClass""]
        },
        {
            ""index"": 2,
            ""project"": ""ProjectName"",
            ""modification_type"": ""modify"",
            ""title"": ""Update ConsumerClass - update method call"",
            ""description"": ""In ConsumerClass.SomeMethod(), update the call to ClassName to use the new method..."",
            ""target_files"": [""Path/ConsumerClass.cs""],
            ""depends_on"": [1],
            ""uses_existing"": [""ClassName""]
        }
    ]
}
```

## Task Ordering

1. **Primary class modification FIRST**: Changes to the main class being modified
2. **Dependent modifications SECOND**: Files that inherit from or heavily depend on the primary class
3. **Reference updates THIRD**: Files that use the class (method calls, instantiation)
4. **Test updates LAST**: Update or add tests for the modifications

**IMPORTANT**: 
- Output ONLY valid JSON, no explanations before or after
- Always specify modification_type for each task
- Provide detailed descriptions so the Coder knows exactly what to change";
    }

    private string BuildModificationPrompt(string requirement, string modificationContext, string codebaseContext)
    {
        var prompt = $@"# MODIFICATION REQUIREMENT

{requirement}

# TARGET CLASS AND REFERENCES

{modificationContext}
";

        if (!string.IsNullOrEmpty(codebaseContext))
        {
            prompt += $@"
# CODEBASE CONTEXT

{codebaseContext}
";
        }

        prompt += @"
# INSTRUCTIONS

Based on the requirement and the class/reference information above:

1. Create tasks to modify the primary class first
2. Create tasks to update all affected files that reference this class
3. Ensure each task has clear, specific instructions for what to change
4. Order tasks by dependency (primary class first, then consumers)
5. Keep existing code intact - only modify what's necessary

For each file that needs changes, create a separate task with:
- Clear description of what to modify
- Which methods/properties are affected
- How the existing code should be preserved

Output your plan as JSON.";

        return prompt;
    }
}
