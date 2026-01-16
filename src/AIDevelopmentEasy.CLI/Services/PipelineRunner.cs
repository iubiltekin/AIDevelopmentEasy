using System.Text;
using System.Text.Json;
using AIDevelopmentEasy.CLI.Configuration;
using AIDevelopmentEasy.CLI.Models;
using AIDevelopmentEasy.CLI.Services.Interfaces;
using AIDevelopmentEasy.Core.Agents;
using AIDevelopmentEasy.Core.Agents.Base;
using AIDevelopmentEasy.Core.Models;

namespace AIDevelopmentEasy.CLI.Services;

/// <summary>
/// Runs the development pipeline with step-by-step user confirmation
/// </summary>
public class PipelineRunner : IPipelineRunner
{
    private readonly ResolvedPaths _paths;
    private readonly IConsoleUI _console;
    private readonly IRequirementLoader _requirementLoader;
    private readonly PlannerAgent _plannerAgent;
    private readonly MultiProjectPlannerAgent _multiPlannerAgent;
    private readonly CoderAgent _coderAgent;
    private readonly DebuggerAgent _debuggerAgent;
    private readonly ReviewerAgent _reviewerAgent;
    
    private static readonly JsonSerializerOptions JsonOptions = new() 
    { 
        WriteIndented = true, 
        PropertyNameCaseInsensitive = true 
    };

    public PipelineRunner(
        ResolvedPaths paths,
        IConsoleUI console,
        IRequirementLoader requirementLoader,
        PlannerAgent plannerAgent,
        MultiProjectPlannerAgent multiPlannerAgent,
        CoderAgent coderAgent,
        DebuggerAgent debuggerAgent,
        ReviewerAgent reviewerAgent)
    {
        _paths = paths;
        _console = console;
        _requirementLoader = requirementLoader;
        _plannerAgent = plannerAgent;
        _multiPlannerAgent = multiPlannerAgent;
        _coderAgent = coderAgent;
        _debuggerAgent = debuggerAgent;
        _reviewerAgent = reviewerAgent;
    }

    public async Task ProcessAsync(RequirementInfo requirement)
    {
        _console.ShowSectionHeader($"Processing: {requirement.FileName}");
        requirement.MarkAsInProgress();

        if (requirement.Type == RequirementType.Single)
            await ProcessSingleProjectAsync(requirement);
        else
            await ProcessMultiProjectAsync(requirement);
    }

    #region Single Project Pipeline

    private async Task ProcessSingleProjectAsync(RequirementInfo requirement)
    {
        var reqContent = await _requirementLoader.LoadSingleProjectRequirementAsync(requirement.FilePath);
        var tasksDir = Path.Combine(requirement.WorkingDirectory, "tasks");
        var approvedFile = Path.Combine(requirement.WorkingDirectory, "_approved.json");

        // Show requirement content
        _console.ShowSubSection("Requirement");
        _console.ShowInfo(reqContent.Length > 500 ? reqContent[..500] + "..." : reqContent);

        // PHASE 1: Planning
        if (requirement.Status < ProcessingStatus.Planned || !Directory.Exists(tasksDir))
        {
            if (!await DoPlanningPhaseAsync(reqContent, tasksDir))
                return;
            requirement.MarkAsPlanned();
        }
        else
        {
            _console.ShowSuccess("  ✓ Tasks already planned");
        }

        // Confirm plan approval
        if (requirement.Status < ProcessingStatus.Approved)
        {
            if (!_console.ConfirmPlanApproval(tasksDir))
            {
                _console.ShowWarning("  Skipped by user");
                return;
            }
            await File.WriteAllTextAsync(approvedFile, JsonSerializer.Serialize(new { ApprovedAt = DateTime.Now }));
            requirement.MarkAsApproved();
        }
        else
        {
            _console.ShowSuccess("  ✓ Plan already approved");
        }

        // PHASE 2: Coding
        if (!_console.ConfirmCodingStart())
        {
            _console.ShowWarning("  Coding skipped by user");
            return;
        }

        var projectState = await DoCodingPhaseAsync(reqContent, tasksDir);
        if (projectState == null) return;

        // PHASE 3: Debugging
        if (!_console.ConfirmDebugStart())
        {
            _console.ShowWarning("  Debugging skipped by user");
            return;
        }

        var debugSuccess = await DoDebuggingPhaseAsync(projectState);
        
        // PHASE 4: Testing (informational for now - actual test run depends on project setup)
        if (_console.ConfirmTestStart())
        {
            await DoTestingPhaseAsync(projectState);
        }

        // PHASE 5: Review
        if (!_console.ConfirmReviewStart())
        {
            _console.ShowWarning("  Review skipped by user");
            await SaveOutputAsync(requirement.Name, projectState, null);
            return;
        }

        var reviewOutput = await DoReviewPhaseAsync(reqContent, projectState);

        // Save output
        var outputPath = await SaveOutputAsync(requirement.Name, projectState, reviewOutput);
        
        // Mark as completed
        var completedFile = Path.Combine(requirement.WorkingDirectory, "_completed.json");
        await File.WriteAllTextAsync(completedFile, JsonSerializer.Serialize(new { CompletedAt = DateTime.Now }));
        requirement.MarkAsCompleted();

        _console.ShowFinalSummary(outputPath, projectState.Codebase.Count);
    }

    private async Task<bool> DoPlanningPhaseAsync(string requirement, string tasksDir)
    {
        _console.ShowPhase("PLANNING", 1);
        _console.ShowProgress("Analyzing requirements and generating tasks...");

        var projectState = new ProjectState
        {
            Requirement = requirement,
            CodingStandards = _paths.CodingStandards
        };

        var planRequest = new AgentRequest { Input = requirement, ProjectState = projectState };
        var planResponse = await _plannerAgent.RunAsync(planRequest);

        if (!planResponse.Success)
        {
            _console.ShowError($"  Planning failed: {planResponse.Error}");
            return false;
        }

        var tasks = projectState.Plan;
        Directory.CreateDirectory(tasksDir);

        _console.ShowSubSection("Generated Tasks");
        foreach (var task in tasks)
        {
            var taskFile = Path.Combine(tasksDir, $"task-{task.Index:D2}.json");
            var taskJson = JsonSerializer.Serialize(task, JsonOptions);
            await File.WriteAllTextAsync(taskFile, taskJson);

            _console.ShowTask(task.Index, task.Title, task.TargetFiles);
        }

        _console.ShowPhaseComplete("Planning");
        return true;
    }

    private async Task<ProjectState?> DoCodingPhaseAsync(string requirement, string tasksDir)
    {
        _console.ShowPhase("CODE GENERATION", 2);

        // Load approved tasks
        var taskFiles = Directory.GetFiles(tasksDir, "task-*.json").OrderBy(f => f).ToList();
        var tasks = new List<SubTask>();

        foreach (var taskFile in taskFiles)
        {
            var json = await File.ReadAllTextAsync(taskFile);
            var task = JsonSerializer.Deserialize<SubTask>(json, JsonOptions);
            if (task != null) tasks.Add(task);
        }

        if (tasks.Count == 0)
        {
            _console.ShowError("  No tasks found!");
            return null;
        }

        var projectState = new ProjectState
        {
            Requirement = requirement,
            CodingStandards = _paths.CodingStandards,
            Plan = tasks,
            CurrentPhase = PipelinePhase.Coding
        };

        // Group by target file
        var tasksByFile = tasks
            .SelectMany(t => t.TargetFiles.Select(f => (File: f, Task: t)))
            .GroupBy(x => x.File)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Task).ToList());

        var sortedFiles = tasksByFile.Keys
            .OrderBy(f =>
            {
                var fn = Path.GetFileName(f).ToLower();
                if (fn == "program.cs") return 3;
                if (fn.Contains("test")) return 2;
                return 1;
            })
            .ToList();

        _console.ShowSubSection("Generating Code");
        foreach (var targetFile in sortedFiles)
        {
            var fileTasks = tasksByFile[targetFile];
            _console.ShowTaskProgress(fileTasks.First().Index, targetFile);

            var combinedDescription = string.Join("\n\n",
                fileTasks.Select(t => $"### Task {t.Index}: {t.Title}\n{t.Description}"));

            var coderRequest = new AgentRequest
            {
                Input = combinedDescription,
                ProjectState = projectState,
                Context = new Dictionary<string, string> { ["target_file"] = targetFile }
            };

            var coderResponse = await _coderAgent.RunAsync(coderRequest);
            _console.ShowTaskResult(coderResponse.Success, coderResponse.Success ? "✓ Generated" : coderResponse.Error);
        }

        _console.ShowPhaseComplete("Code Generation");
        return projectState;
    }

    private async Task<bool> DoDebuggingPhaseAsync(ProjectState projectState)
    {
        _console.ShowPhase("COMPILATION CHECK", 3);
        _console.ShowProgress("Running compiler/debugger analysis...");

        var allCode = new StringBuilder();
        foreach (var (filename, code) in projectState.Codebase)
        {
            allCode.AppendLine($"// === {filename} ===");
            allCode.AppendLine(code);
            allCode.AppendLine();
        }

        var debugRequest = new AgentRequest
        {
            Input = allCode.ToString(),
            ProjectState = projectState,
            Context = new Dictionary<string, string> { ["filename"] = "Program.cs" }
        };

        var debugResponse = await _debuggerAgent.RunAsync(debugRequest);
        
        _console.ShowCompilationResult(debugResponse.Success, debugResponse.Output);
        _console.ShowPhaseComplete("Compilation Check");

        return debugResponse.Success;
    }

    private async Task DoTestingPhaseAsync(ProjectState projectState)
    {
        _console.ShowPhase("UNIT TESTING", 4);
        _console.ShowProgress("Analyzing test coverage...");

        // For now, show informational - actual test execution would require project build
        var testFiles = projectState.Codebase.Keys.Where(f => f.ToLower().Contains("test")).ToList();
        
        if (testFiles.Count > 0)
        {
            _console.ShowTestResults(true, testFiles.Count, 0, 
                $"  Test files generated: {string.Join(", ", testFiles)}");
        }
        else
        {
            _console.ShowWarning("  No test files found in generated code");
        }

        _console.ShowPhaseComplete("Unit Testing");
        await Task.CompletedTask;
    }

    private async Task<string?> DoReviewPhaseAsync(string requirement, ProjectState projectState)
    {
        _console.ShowPhase("CODE REVIEW", 5);
        _console.ShowProgress("Running code review analysis...");

        var reviewRequest = new AgentRequest { Input = requirement, ProjectState = projectState };
        var reviewResponse = await _reviewerAgent.RunAsync(reviewRequest);

        var verdict = reviewResponse.Data?.GetValueOrDefault("verdict")?.ToString() ?? "Unknown";
        var approved = reviewResponse.Data?.GetValueOrDefault("approved") is bool b && b;

        _console.ShowReviewResults(verdict, approved, reviewResponse.Output);
        _console.ShowPhaseComplete("Code Review");

        return reviewResponse.Output;
    }

    private async Task<string> SaveOutputAsync(string reqName, ProjectState projectState, string? reviewOutput)
    {
        var outputDir = Path.Combine(_paths.OutputPath, $"{DateTime.Now:yyyyMMdd_HHmmss}_{reqName}");
        Directory.CreateDirectory(outputDir);

        foreach (var (filename, code) in projectState.Codebase)
        {
            var filePath = Path.Combine(outputDir, filename);
            var fileDir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(fileDir))
                Directory.CreateDirectory(fileDir);
            await File.WriteAllTextAsync(filePath, code);
        }

        if (!string.IsNullOrEmpty(reviewOutput))
        {
            await File.WriteAllTextAsync(Path.Combine(outputDir, "review_report.md"), reviewOutput);
        }

        return outputDir;
    }

    #endregion

    #region Multi Project Pipeline

    private async Task ProcessMultiProjectAsync(RequirementInfo requirement)
    {
        var mpReq = await _requirementLoader.LoadMultiProjectRequirementAsync(requirement.FilePath);

        if (mpReq == null)
        {
            _console.ShowError("  Failed to parse multi-project requirement JSON");
            return;
        }

        if (mpReq.AffectedProjects.Count == 0)
        {
            _console.ShowWarning("  No affected projects defined in requirement");
            return;
        }

        _console.ShowMultiProjectInfo(mpReq);

        var projectsDir = Path.Combine(requirement.WorkingDirectory, "projects");
        var approvedFile = Path.Combine(requirement.WorkingDirectory, "_approved.json");

        // PHASE 1: Planning
        if (requirement.Status < ProcessingStatus.Planned || !Directory.Exists(projectsDir))
        {
            if (!await DoMultiProjectPlanningAsync(mpReq, requirement.WorkingDirectory))
                return;
            requirement.MarkAsPlanned();
        }
        else
        {
            _console.ShowSuccess("  ✓ Tasks already planned");
        }

        // Confirm plan approval
        if (requirement.Status < ProcessingStatus.Approved)
        {
            if (!_console.ConfirmPlanApproval(projectsDir))
            {
                _console.ShowWarning("  Skipped by user");
                return;
            }
            await File.WriteAllTextAsync(approvedFile, JsonSerializer.Serialize(new { ApprovedAt = DateTime.Now }));
            requirement.MarkAsApproved();
        }
        else
        {
            _console.ShowSuccess("  ✓ Plan already approved");
        }

        // PHASE 2: Coding
        if (!_console.ConfirmCodingStart())
        {
            _console.ShowWarning("  Coding skipped by user");
            return;
        }

        var (allProjectCode, combinedProjectState) = await DoMultiProjectCodingAsync(mpReq, projectsDir);
        if (allProjectCode == null || combinedProjectState == null) return;

        // PHASE 3: Debugging
        if (!_console.ConfirmDebugStart())
        {
            _console.ShowWarning("  Debugging skipped by user");
            await SaveMultiProjectOutputAsync(requirement.Name, allProjectCode);
            return;
        }

        var debugSuccess = await DoDebuggingPhaseAsync(combinedProjectState);

        // PHASE 4: Testing
        if (_console.ConfirmTestStart())
        {
            await DoTestingPhaseAsync(combinedProjectState);
        }

        // PHASE 5: Review
        if (!_console.ConfirmReviewStart())
        {
            _console.ShowWarning("  Review skipped by user");
            await SaveMultiProjectOutputAsync(requirement.Name, allProjectCode);
            return;
        }

        var reviewOutput = await DoReviewPhaseAsync($"{mpReq.Title}\n\n{mpReq.Description}", combinedProjectState);

        // Save output
        var outputDir = await SaveMultiProjectOutputAsync(requirement.Name, allProjectCode, reviewOutput);

        // Mark as completed
        var completedFile = Path.Combine(requirement.WorkingDirectory, "_completed.json");
        await File.WriteAllTextAsync(completedFile, JsonSerializer.Serialize(new { CompletedAt = DateTime.Now }));
        requirement.MarkAsCompleted();

        _console.ShowFinalSummary(outputDir, allProjectCode.Values.Sum(d => d.Count));
    }

    private async Task<bool> DoMultiProjectPlanningAsync(MultiProjectRequirement mpReq, string workingDir)
    {
        _console.ShowPhase("MULTI-PROJECT PLANNING", 1);
        _console.ShowProgress("Analyzing requirements for all projects...");

        var projectState = new ProjectState
        {
            Requirement = $"{mpReq.Title}\n\n{mpReq.Description}",
            CodingStandards = _paths.CodingStandards
        };

        var planResponse = await _multiPlannerAgent.PlanMultiProjectAsync(mpReq, projectState);

        if (!planResponse.Success)
        {
            _console.ShowError($"  Planning failed: {planResponse.Error}");
            return false;
        }

        var phases = planResponse.Data?["phases"] as List<ExecutionPhase> ?? new();

        foreach (var execPhase in phases)
        {
            _console.ShowSubSection($"Phase {execPhase.Order}: {execPhase.Name}");

            var tasksByProject = execPhase.Tasks.GroupBy(t => t.ProjectName);
            foreach (var projectTasks in tasksByProject)
            {
                var projectDir = Path.Combine(workingDir, "projects", projectTasks.Key, "tasks");
                Directory.CreateDirectory(projectDir);

                _console.ShowInfo($"    {projectTasks.Key}:");

                foreach (var task in projectTasks)
                {
                    var taskFile = Path.Combine(projectDir, $"task-{task.Index:D2}.json");
                    var taskJson = JsonSerializer.Serialize(task, JsonOptions);
                    await File.WriteAllTextAsync(taskFile, taskJson);

                    _console.ShowTask(task.Index, task.Title, task.TargetFiles);
                }
            }
        }

        _console.ShowPhaseComplete("Multi-Project Planning");
        return true;
    }

    private async Task<(Dictionary<string, Dictionary<string, string>>?, ProjectState?)> DoMultiProjectCodingAsync(
        MultiProjectRequirement mpReq, 
        string projectsDir)
    {
        _console.ShowPhase("MULTI-PROJECT CODE GENERATION", 2);

        var projectsByPhase = mpReq.AffectedProjects
            .GroupBy(p => p.Order)
            .OrderBy(g => g.Key);

        var allProjectCode = new Dictionary<string, Dictionary<string, string>>();
        
        // Combined project state for debug/review phases
        var combinedProjectState = new ProjectState
        {
            Requirement = $"{mpReq.Title}\n\n{mpReq.Description}",
            CodingStandards = _paths.CodingStandards,
            CurrentPhase = PipelinePhase.Coding
        };

        foreach (var phase in projectsByPhase)
        {
            _console.ShowSubSection($"Phase {phase.Key}");
            var sortedProjects = phase.OrderBy(p => p.IsTestProject ? 1 : 0);

            foreach (var project in sortedProjects)
            {
                var projectTasksDir = Path.Combine(projectsDir, project.Name, "tasks");
                if (!Directory.Exists(projectTasksDir))
                {
                    _console.ShowWarning($"  Skipping {project.Name} (no tasks)");
                    continue;
                }

                _console.ShowProjectHeader(project.Name, project.Role);

                var tasks = await LoadProjectTasksAsync(projectTasksDir);
                if (tasks.Count == 0)
                {
                    _console.ShowInfo("  │  No tasks found");
                    continue;
                }

                var projectState = CreateProjectState(project, mpReq, allProjectCode);

                await GenerateProjectCodeAsync(project, tasks, projectState);

                allProjectCode[project.Name] = new Dictionary<string, string>(projectState.Codebase);
                
                // Add to combined state for debug/review (with project prefix)
                foreach (var (filename, code) in projectState.Codebase)
                {
                    if (!filename.StartsWith("_"))
                    {
                        combinedProjectState.Codebase[$"{project.Name}/{filename}"] = code;
                    }
                }

                var fileCount = projectState.Codebase.Count -
                    (projectState.Codebase.ContainsKey("_dependencies.cs") ? 1 : 0);
                _console.ShowProjectFooter(fileCount);
            }
        }

        _console.ShowPhaseComplete("Multi-Project Code Generation");
        return (allProjectCode, combinedProjectState);
    }

    private async Task<List<ProjectSubTask>> LoadProjectTasksAsync(string projectTasksDir)
    {
        var taskFiles = Directory.GetFiles(projectTasksDir, "task-*.json").OrderBy(f => f).ToList();
        var tasks = new List<ProjectSubTask>();

        foreach (var taskFile in taskFiles)
        {
            var json = await File.ReadAllTextAsync(taskFile);
            var task = JsonSerializer.Deserialize<ProjectSubTask>(json, JsonOptions);
            if (task != null) tasks.Add(task);
        }

        return tasks;
    }

    private ProjectState CreateProjectState(
        AffectedProject project,
        MultiProjectRequirement mpReq,
        Dictionary<string, Dictionary<string, string>> allProjectCode)
    {
        var dependencyContext = new StringBuilder();
        foreach (var depName in project.DependsOn)
        {
            if (allProjectCode.TryGetValue(depName, out var depCode))
            {
                dependencyContext.AppendLine($"\n// === Code from {depName} ===");
                foreach (var (fn, code) in depCode)
                {
                    dependencyContext.AppendLine($"// --- {fn} ---");
                    dependencyContext.AppendLine(code);
                }
            }
        }

        var projectState = new ProjectState
        {
            ProjectName = project.Name,
            Requirement = $"{mpReq.Title} - {project.Name}",
            CodingStandards = _paths.CodingStandards,
            CurrentPhase = PipelinePhase.Coding
        };

        if (dependencyContext.Length > 0)
        {
            projectState.Codebase["_dependencies.cs"] = dependencyContext.ToString();
        }

        return projectState;
    }

    private async Task GenerateProjectCodeAsync(
        AffectedProject project,
        List<ProjectSubTask> tasks,
        ProjectState projectState)
    {
        foreach (var task in tasks)
        {
            _console.ShowTaskProgress(task.Index, task.Title);

            var contextInfo = task.UsesClasses.Count > 0
                ? $"\n\nThis code should use the following classes from dependencies: {string.Join(", ", task.UsesClasses)}"
                : "";

            var coderRequest = new AgentRequest
            {
                Input = $"Project: {project.Name}\nTask: {task.Title}\n\n{task.Description}{contextInfo}",
                ProjectState = projectState,
                Context = new Dictionary<string, string>
                {
                    ["target_file"] = task.TargetFiles.FirstOrDefault() ?? $"{task.Title}.cs",
                    ["project"] = project.Name,
                    ["is_test"] = project.IsTestProject.ToString()
                }
            };

            var coderResponse = await _coderAgent.RunAsync(coderRequest);
            _console.ShowTaskResult(coderResponse.Success, 
                coderResponse.Success ? "✓" : coderResponse.Error);
        }
    }

    private async Task<string> SaveMultiProjectOutputAsync(
        string reqName,
        Dictionary<string, Dictionary<string, string>> allProjectCode,
        string? reviewOutput = null)
    {
        var outputDir = Path.Combine(_paths.OutputPath, $"{DateTime.Now:yyyyMMdd_HHmmss}_{reqName}");
        Directory.CreateDirectory(outputDir);

        foreach (var (projectName, codebase) in allProjectCode)
        {
            var projectOutputDir = Path.Combine(outputDir, projectName);
            Directory.CreateDirectory(projectOutputDir);

            foreach (var (fn, code) in codebase)
            {
                if (!fn.StartsWith("_"))
                {
                    var filePath = Path.Combine(projectOutputDir, fn);
                    var fileDir = Path.GetDirectoryName(filePath);
                    if (!string.IsNullOrEmpty(fileDir))
                        Directory.CreateDirectory(fileDir);
                    await File.WriteAllTextAsync(filePath, code);
                }
            }
        }

        if (!string.IsNullOrEmpty(reviewOutput))
        {
            await File.WriteAllTextAsync(Path.Combine(outputDir, "review_report.md"), reviewOutput);
        }

        return outputDir;
    }

    #endregion
}
