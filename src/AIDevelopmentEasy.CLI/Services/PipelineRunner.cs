using System.Text;
using System.Text.Json;
using AIDevelopmentEasy.CLI.Configuration;
using AIDevelopmentEasy.CLI.Models;
using AIDevelopmentEasy.CLI.Services.Interfaces;
using AIDevelopmentEasy.Core.Agents;
using AIDevelopmentEasy.Core.Agents.Base;

namespace AIDevelopmentEasy.CLI.Services;

/// <summary>
/// Runs the development pipeline with step-by-step user confirmation.
/// Based on AgentMesh framework (arXiv:2507.19902):
/// Planner → Coder → Debugger → Reviewer
/// </summary>
public class PipelineRunner : IPipelineRunner
{
    private readonly ResolvedPaths _paths;
    private readonly IConsoleUI _console;
    private readonly IRequirementLoader _requirementLoader;
    private readonly PlannerAgent _plannerAgent;
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
        CoderAgent coderAgent,
        DebuggerAgent debuggerAgent,
        ReviewerAgent reviewerAgent)
    {
        _paths = paths;
        _console = console;
        _requirementLoader = requirementLoader;
        _plannerAgent = plannerAgent;
        _coderAgent = coderAgent;
        _debuggerAgent = debuggerAgent;
        _reviewerAgent = reviewerAgent;
    }

    public async Task ProcessAsync(RequirementInfo requirement)
    {
        _console.ShowSectionHeader($"Processing: {requirement.FileName}");
        requirement.MarkAsInProgress();

        var reqContent = await _requirementLoader.LoadRequirementAsync(requirement.FilePath);
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
        
        // PHASE 4: Testing (informational for now)
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

        // Show informational - actual test execution would require project build
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
}
