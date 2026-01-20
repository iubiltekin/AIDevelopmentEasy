using System.Collections.Concurrent;
using System.IO;
using AIDevelopmentEasy.Api.Hubs;
using AIDevelopmentEasy.Api.Models;
using AIDevelopmentEasy.Api.Repositories.Interfaces;
using AIDevelopmentEasy.Api.Services.Interfaces;
using AIDevelopmentEasy.Core.Agents;
using AIDevelopmentEasy.Core.Agents.Base;
using AIDevelopmentEasy.Core.Models;

// Use Api.Models.PipelinePhase to avoid ambiguity with Core.Agents.Base.PipelinePhase
using PipelinePhase = AIDevelopmentEasy.Api.Models.PipelinePhase;
using CorePipelinePhase = AIDevelopmentEasy.Core.Agents.Base.PipelinePhase;

namespace AIDevelopmentEasy.Api.Services;

/// <summary>
/// Pipeline orchestration service based on AgentMesh framework (arXiv:2507.19902).
/// 
/// Manages the execution of the multi-agent pipeline with approval gates:
/// 1. Planning - PlannerAgent decomposes stories into tasks (with codebase context)
/// 2. Coding - CoderAgent generates code for each task
/// 3. Debugging - DebuggerAgent tests and fixes code
/// 4. Testing - Test analysis and execution
/// 5. Reviewing - ReviewerAgent validates final output
/// 
/// When a codebase is associated with the story, CodeAnalysisAgent provides
/// context about existing projects, patterns, and conventions to the PlannerAgent.
/// </summary>
public class PipelineService : IPipelineService
{
    private readonly IStoryRepository _storyRepository;
    private readonly ITaskRepository _taskRepository;
    private readonly IApprovalRepository _approvalRepository;
    private readonly IOutputRepository _outputRepository;
    private readonly ICodebaseRepository _codebaseRepository;
    private readonly IPipelineNotificationService _notificationService;
    private readonly IKnowledgeService _knowledgeService;
    private readonly PlannerAgent _plannerAgent;
    private readonly CoderAgent _coderAgent;
    private readonly DebuggerAgent _debuggerAgent;
    private readonly ReviewerAgent _reviewerAgent;
    private readonly CodeAnalysisAgent _codeAnalysisAgent;
    private readonly DeploymentAgent _deploymentAgent;
    private readonly UnitTestAgent _unitTestAgent;
    private readonly ILogger<PipelineService> _logger;

    // Track running pipelines and their state
    private static readonly ConcurrentDictionary<string, PipelineExecution> _runningPipelines = new();

    // Track completed pipelines for status retrieval (keeps last N completed for memory efficiency)
    private static readonly ConcurrentDictionary<string, PipelineStatusDto> _completedPipelines = new();

    public PipelineService(
        IStoryRepository storyRepository,
        ITaskRepository taskRepository,
        IApprovalRepository approvalRepository,
        IOutputRepository outputRepository,
        ICodebaseRepository codebaseRepository,
        IPipelineNotificationService notificationService,
        IKnowledgeService knowledgeService,
        PlannerAgent plannerAgent,
        CoderAgent coderAgent,
        DebuggerAgent debuggerAgent,
        ReviewerAgent reviewerAgent,
        CodeAnalysisAgent codeAnalysisAgent,
        DeploymentAgent deploymentAgent,
        UnitTestAgent unitTestAgent,
        ILogger<PipelineService> logger)
    {
        _storyRepository = storyRepository;
        _taskRepository = taskRepository;
        _approvalRepository = approvalRepository;
        _outputRepository = outputRepository;
        _codebaseRepository = codebaseRepository;
        _notificationService = notificationService;
        _knowledgeService = knowledgeService;
        _plannerAgent = plannerAgent;
        _coderAgent = coderAgent;
        _debuggerAgent = debuggerAgent;
        _reviewerAgent = reviewerAgent;
        _codeAnalysisAgent = codeAnalysisAgent;
        _deploymentAgent = deploymentAgent;
        _unitTestAgent = unitTestAgent;
        _logger = logger;
    }

    public async Task<PipelineStatusDto> StartAsync(string storyId, bool autoApproveAll = false, CancellationToken cancellationToken = default)
    {
        // Check if already running
        if (_runningPipelines.ContainsKey(storyId))
        {
            throw new InvalidOperationException($"Pipeline already running for story: {storyId}");
        }

        // Get the story
        var story = await _storyRepository.GetByIdAsync(storyId, cancellationToken);
        if (story == null)
        {
            throw new ArgumentException($"Requirement not found: {storyId}");
        }

        // Check if already completed
        if (story.Status == StoryStatus.Completed)
        {
            throw new InvalidOperationException($"Requirement already completed: {storyId}");
        }

        // Create pipeline execution context
        var execution = new PipelineExecution
        {
            StoryId = storyId,
            AutoApproveAll = autoApproveAll,
            Status = CreateInitialStatus(storyId),
            CancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
        };

        _runningPipelines[storyId] = execution;

        // Start pipeline execution in background
        _ = Task.Run(() => ExecutePipelineAsync(execution), execution.CancellationTokenSource.Token);

        return execution.Status;
    }

    public async Task<PipelineStatusDto?> GetStatusAsync(string storyId, CancellationToken cancellationToken = default)
    {
        // Check running pipelines first
        if (_runningPipelines.TryGetValue(storyId, out var execution))
        {
            return execution.Status;
        }

        // Check completed pipelines (in-memory cache)
        if (_completedPipelines.TryGetValue(storyId, out var completedStatus))
        {
            return completedStatus;
        }

        // Try to load from disk (for completed pipelines after restart)
        var historyJson = await _outputRepository.GetPipelineHistoryAsync(storyId, cancellationToken);
        if (!string.IsNullOrEmpty(historyJson))
        {
            try
            {
                var status = System.Text.Json.JsonSerializer.Deserialize<PipelineStatusDto>(historyJson, new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                if (status != null)
                {
                    // Cache it for future requests
                    _completedPipelines[storyId] = status;
                    return status;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize pipeline history for {StoryId}", storyId);
            }
        }

        // Return a "not running" status
        return new PipelineStatusDto
        {
            StoryId = storyId,
            CurrentPhase = PipelinePhase.None,
            IsRunning = false
        };
    }

    public Task<bool> ApprovePhaseAsync(string storyId, PipelinePhase phase, CancellationToken cancellationToken = default)
    {
        if (!_runningPipelines.TryGetValue(storyId, out var execution))
        {
            return Task.FromResult(false);
        }

        if (execution.CurrentPhase != phase || execution.PhaseApprovalTcs == null)
        {
            _logger.LogWarning("Cannot approve phase {Phase} - current phase is {CurrentPhase}", phase, execution.CurrentPhase);
            return Task.FromResult(false);
        }

        execution.PhaseApprovalTcs.TrySetResult(true);
        _logger.LogInformation("[{StoryId}] Phase {Phase} approved", storyId, phase);
        return Task.FromResult(true);
    }

    public async Task<bool> RejectPhaseAsync(string storyId, PipelinePhase phase, string? reason = null, CancellationToken cancellationToken = default)
    {
        if (!_runningPipelines.TryGetValue(storyId, out var execution))
        {
            return false;
        }

        if (execution.CurrentPhase != phase || execution.PhaseApprovalTcs == null)
        {
            return false;
        }

        _logger.LogInformation("[{StoryId}] Phase {Phase} rejected. Reason: {Reason}", storyId, phase, reason);

        // If rejecting UnitTesting or later phases, rollback deployment changes
        if (phase >= PipelinePhase.UnitTesting && execution.LastDeploymentResult != null)
        {
            _logger.LogInformation("[{StoryId}] Rolling back deployment changes...", storyId);

            await _notificationService.NotifyProgressAsync(storyId, "🔄 Rolling back deployment changes...");

            try
            {
                var rollbackResult = await _deploymentAgent.RollbackAsync(execution.LastDeploymentResult, cancellationToken);

                if (rollbackResult.Success)
                {
                    _logger.LogInformation("[{StoryId}] Rollback completed: {Deleted} files deleted, {Reverted} projects reverted",
                        storyId, rollbackResult.DeletedFiles.Count, rollbackResult.RevertedProjects.Count);

                    await _notificationService.NotifyProgressAsync(storyId,
                        $"✅ Rollback completed: {rollbackResult.DeletedFiles.Count} files deleted, {rollbackResult.RevertedProjects.Count} projects reverted");
                }
                else
                {
                    var errorMsg = rollbackResult.Errors.Count > 0 ? string.Join("; ", rollbackResult.Errors) : "Unknown error";
                    _logger.LogWarning("[{StoryId}] Rollback had issues: {Error}", storyId, errorMsg);
                    await _notificationService.NotifyProgressAsync(storyId,
                        $"⚠️ Rollback completed with issues: {errorMsg}");
                }

                // Clear deployment result after rollback
                execution.LastDeploymentResult = null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{StoryId}] Rollback failed", storyId);
                await _notificationService.NotifyProgressAsync(storyId, $"❌ Rollback failed: {ex.Message}");
            }
        }

        execution.RejectionReason = reason;
        execution.PhaseApprovalTcs.TrySetResult(false);
        return true;
    }

    public Task CancelAsync(string storyId, CancellationToken cancellationToken = default)
    {
        if (_runningPipelines.TryGetValue(storyId, out var execution))
        {
            execution.CancellationTokenSource.Cancel();
            _runningPipelines.TryRemove(storyId, out _);
            _logger.LogInformation("[{StoryId}] Pipeline cancelled", storyId);
        }

        return Task.CompletedTask;
    }

    public Task<bool> IsRunningAsync(string storyId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_runningPipelines.ContainsKey(storyId));
    }

    public Task<IEnumerable<string>> GetRunningPipelinesAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IEnumerable<string>>(_runningPipelines.Keys.ToList());
    }

    public Task<bool> ApproveRetryAsync(string storyId, RetryAction action, CancellationToken cancellationToken = default)
    {
        if (!_runningPipelines.TryGetValue(storyId, out var execution))
        {
            return Task.FromResult(false);
        }

        if (execution.RetryApprovalTcs == null)
        {
            _logger.LogWarning("Cannot approve retry - no retry pending for {StoryId}", storyId);
            return Task.FromResult(false);
        }

        execution.RetryApprovalTcs.TrySetResult(action);
        _logger.LogInformation("[{StoryId}] Retry approved with action: {Action}", storyId, action);
        return Task.FromResult(true);
    }

    public Task<RetryInfoDto?> GetRetryInfoAsync(string storyId, CancellationToken cancellationToken = default)
    {
        if (_runningPipelines.TryGetValue(storyId, out var execution))
        {
            return Task.FromResult(execution.CurrentRetryInfo);
        }
        return Task.FromResult<RetryInfoDto?>(null);
    }

    private async Task ExecutePipelineAsync(PipelineExecution execution)
    {
        var storyId = execution.StoryId;
        var ct = execution.CancellationTokenSource.Token;

        try
        {
            execution.Status.IsRunning = true;
            execution.Status.StartedAt = DateTime.UtcNow;

            // Update story status to InProgress
            await _storyRepository.UpdateStatusAsync(storyId, StoryStatus.InProgress, ct);
            await _notificationService.NotifyStoryListChangedAsync();

            // Get story content
            var content = await _storyRepository.GetContentAsync(storyId, ct);
            if (string.IsNullOrEmpty(content))
            {
                throw new InvalidOperationException("Requirement content is empty");
            }

            var story = await _storyRepository.GetByIdAsync(storyId, ct);
            if (story == null)
            {
                throw new InvalidOperationException($"Requirement not found: {storyId}");
            }

            // Phase 1: Analysis (optional - only when codebase is linked)
            Core.Models.CodebaseAnalysis? codebaseAnalysis = null;
            ClassSearchResult? classResult = null;
            ReferenceSearchResult? referenceResult = null;

            if (!string.IsNullOrEmpty(story.CodebaseId))
            {
                var analysisResult = await ExecutePhaseAsync(execution, PipelinePhase.Analysis, async () =>
                {
                    await _notificationService.NotifyPhaseStartedAsync(storyId, PipelinePhase.Analysis,
                        "CodeAnalysisAgent analyzing codebase...");

                    await _notificationService.NotifyProgressAsync(storyId, $"Loading codebase: {story.CodebaseId}");
                    codebaseAnalysis = await _codebaseRepository.GetAnalysisAsync(story.CodebaseId, ct);

                    if (codebaseAnalysis == null)
                    {
                        throw new InvalidOperationException($"Codebase analysis not found: {story.CodebaseId}");
                    }

                    var summary = codebaseAnalysis.Summary;
                    await _notificationService.NotifyProgressAsync(storyId,
                        $"Codebase loaded: {summary.TotalProjects} projects, {summary.TotalClasses} classes");

                    // Use target class from story if provided, otherwise try to extract from content
                    var targetClassName = !string.IsNullOrEmpty(story.TargetClass) 
                        ? story.TargetClass 
                        : ExtractTargetClassName(content);

                    // Read target file content from disk if specified
                    string? targetFileContent = null;
                    string? targetMethodContent = null;
                    if (!string.IsNullOrEmpty(story.TargetFile) && !string.IsNullOrEmpty(codebaseAnalysis.CodebasePath))
                    {
                        var fullPath = Path.Combine(codebaseAnalysis.CodebasePath, story.TargetFile);
                        if (File.Exists(fullPath))
                        {
                            targetFileContent = await File.ReadAllTextAsync(fullPath, ct);
                            await _notificationService.NotifyProgressAsync(storyId,
                                $"Read target file: {story.TargetFile} ({targetFileContent.Length} chars)");

                            // Extract specific method if specified
                            if (!string.IsNullOrEmpty(story.TargetMethod))
                            {
                                targetMethodContent = ExtractMethodContent(targetFileContent, story.TargetMethod);
                                if (!string.IsNullOrEmpty(targetMethodContent))
                                {
                                    await _notificationService.NotifyProgressAsync(storyId,
                                        $"Extracted target method: {story.TargetMethod}");
                                }
                            }
                        }
                        else
                        {
                            _logger.LogWarning("[Analysis] Target file not found: {Path}", fullPath);
                        }
                    }

                    // Read test file content if specified
                    string? targetTestFileContent = null;
                    if (!string.IsNullOrEmpty(story.TargetTestFile) && !string.IsNullOrEmpty(codebaseAnalysis.CodebasePath))
                    {
                        var fullTestPath = Path.Combine(codebaseAnalysis.CodebasePath, story.TargetTestFile);
                        if (File.Exists(fullTestPath))
                        {
                            targetTestFileContent = await File.ReadAllTextAsync(fullTestPath, ct);
                            await _notificationService.NotifyProgressAsync(storyId,
                                $"Read target test file: {story.TargetTestFile} ({targetTestFileContent.Length} chars)");
                        }
                    }

                    // Only search for class if NOT using targeted modification (file/class already specified)
                    var hasExplicitTarget = !string.IsNullOrEmpty(story.TargetFile) && !string.IsNullOrEmpty(story.TargetClass);
                    
                    if (!string.IsNullOrEmpty(targetClassName) && !hasExplicitTarget)
                    {
                        // No explicit target - try to find class in codebase
                        await _notificationService.NotifyProgressAsync(storyId,
                            $"Searching for class: {targetClassName}...");

                        classResult = await _codeAnalysisAgent.FindClassAsync(codebaseAnalysis, targetClassName, includeContent: true);

                        if (classResult.Found)
                        {
                            await _notificationService.NotifyProgressAsync(storyId,
                                $"Found class at: {classResult.FilePath}");

                            referenceResult = await _codeAnalysisAgent.FindReferencesAsync(codebaseAnalysis, targetClassName, ct);

                            await _notificationService.NotifyProgressAsync(storyId,
                                $"Found {referenceResult.References.Count} references in {referenceResult.AffectedFiles.Count} files");
                        }
                        else
                        {
                            await _notificationService.NotifyProgressAsync(storyId,
                                $"Class '{targetClassName}' not found, will create new code");
                        }
                    }
                    else if (hasExplicitTarget)
                    {
                        // Explicit target specified - use it directly, no need to search
                        await _notificationService.NotifyProgressAsync(storyId,
                            $"Using explicit target: {story.TargetClass} in {story.TargetFile}");
                        
                        _logger.LogInformation("[Analysis] Explicit target: {Project}/{File}/{Class}.{Method}",
                            story.TargetProject, story.TargetFile, story.TargetClass, story.TargetMethod);
                    }

                    // Store target info in execution for later use
                    execution.TargetInfo = new TargetInfo
                    {
                        Project = story.TargetProject,
                        File = story.TargetFile,
                        Class = story.TargetClass,
                        Method = story.TargetMethod,
                        ChangeType = story.ChangeType,
                        FileContent = targetFileContent,
                        MethodContent = targetMethodContent,
                        TestProject = story.TargetTestProject,
                        TestFile = story.TargetTestFile,
                        TestClass = story.TargetTestClass,
                        TestFileContent = targetTestFileContent
                    };

                    return new
                    {
                        CodebaseName = codebaseAnalysis.CodebaseName,
                        CodebasePath = codebaseAnalysis.CodebasePath,
                        TotalProjects = summary.TotalProjects,
                        TotalClasses = summary.TotalClasses,
                        TotalInterfaces = summary.TotalInterfaces,
                        Patterns = summary.DetectedPatterns,
                        TargetClass = targetClassName,
                        TargetFile = story.TargetFile,
                        TargetMethod = story.TargetMethod,
                        ChangeType = story.ChangeType.ToString(),
                        ClassFound = classResult?.Found ?? false,
                        ClassFilePath = classResult?.FullPath,
                        ReferenceCount = referenceResult?.References.Count ?? 0,
                        AffectedFiles = referenceResult?.AffectedFiles ?? new List<string>(),
                        HasTargetFileContent = targetFileContent != null,
                        HasTestFileContent = targetTestFileContent != null
                    };
                }, ct);

                if (!analysisResult.Approved) return;
            }
            else
            {
                // Skip analysis phase - no codebase linked
                var analysisPhaseStatus = execution.Status.Phases.FirstOrDefault(p => p.Phase == PipelinePhase.Analysis);
                if (analysisPhaseStatus != null)
                {
                    analysisPhaseStatus.State = PhaseState.Skipped;
                    analysisPhaseStatus.Message = "No codebase linked";
                }
            }

            // Phase 2: Planning (with codebase context if available)
            var planningResult = await ExecutePhaseAsync(execution, PipelinePhase.Planning, async () =>
            {
                await _notificationService.NotifyPhaseStartedAsync(storyId, PipelinePhase.Planning,
                    "PlannerAgent decomposing storys into tasks...");

                var projectState = new ProjectState { Story = content };
                AgentResponse response;

                if (codebaseAnalysis != null)
                {
                    _logger.LogInformation("[Planning] Using codebase-aware planning for: {CodebaseId}", story?.CodebaseId);

                    // Check if we have targeted modification info
                    if (execution.TargetInfo?.HasCodeTarget == true)
                    {
                        // TARGETED PLANNING: User specified exact file/class/method to modify
                        await _notificationService.NotifyProgressAsync(storyId,
                            $"Creating targeted modification plan for: {execution.TargetInfo.Class ?? execution.TargetInfo.File}");

                        response = await PlanTargetedModificationAsync(
                            content, execution.TargetInfo, codebaseAnalysis, ct);

                        await _notificationService.NotifyProgressAsync(storyId,
                            "Targeted modification plan created - will modify specific file(s) only");
                    }
                    else if (classResult?.Found == true && referenceResult != null)
                    {
                        // Modification planning with class/reference context
                        await _notificationService.NotifyProgressAsync(storyId,
                            "Creating modification plan for existing class and references...");

                        var modificationTasks = await _codeAnalysisAgent.CreateModificationTasksAsync(
                            codebaseAnalysis, referenceResult, content, ct);

                        response = await _plannerAgent.PlanModificationAsync(
                            content, classResult, referenceResult, modificationTasks, codebaseAnalysis, ct);

                        await _notificationService.NotifyProgressAsync(storyId,
                            "Modification plan created - will update existing files");
                    }
                    else
                    {
                        // Standard planning with codebase context
                        response = await _plannerAgent.PlanWithCodebaseAsync(content, codebaseAnalysis, projectState, ct);
                    }
                }
                else
                {
                    // Standard planning without codebase context
                    var request = new AgentRequest { Input = content, ProjectState = projectState };
                    response = await _plannerAgent.RunAsync(request, ct);
                }

                if (!response.Success)
                {
                    throw new InvalidOperationException($"Planning failed: {response.Error}");
                }

                // Convert and save tasks (including modification metadata)
                var tasks = ConvertToTaskDtos(response.Data);
                await _taskRepository.SaveTasksAsync(storyId, tasks, ct);

                var modificationCount = tasks.Count(t => t.IsModification);

                return new
                {
                    TaskCount = tasks.Count,
                    ModificationCount = modificationCount,
                    NewFileCount = tasks.Count - modificationCount,
                    Tasks = tasks.Select(t => new { t.Index, t.Title, t.IsModification, t.TargetFiles }).ToList()
                };
            }, ct);

            if (!planningResult.Approved) return;

            // Approve plan
            await _approvalRepository.ApprovePlanAsync(storyId, ct);

            // Phase 2: Coding (with modification support and parallel execution by project)
            var codingResult = await ExecutePhaseAsync(execution, PipelinePhase.Coding, async () =>
            {
                await _notificationService.NotifyPhaseStartedAsync(storyId, PipelinePhase.Coding, "Generating/Modifying code...");

                var allTasks = (await _taskRepository.GetByStoryAsync(storyId, ct)).ToList();

                // Only process PENDING tasks (Original tasks at first run)
                // This ensures we don't re-process tasks on retry
                var tasks = allTasks.Where(t => t.Status == Models.TaskStatus.Pending).ToList();

                var projectState = new ProjectState { Story = content };
                var generatedFiles = new ConcurrentDictionary<string, string>();
                var modifiedFiles = new ConcurrentDictionary<string, string>();

                // Check if any tasks are modifications
                var hasModifications = tasks.Any(t => t.IsModification);
                if (hasModifications)
                {
                    await _notificationService.NotifyProgressAsync(storyId,
                        "Mode: MODIFICATION - will update existing files in the codebase");
                }

                // Group tasks by project
                var projectGroups = GroupTasksByProject(tasks);
                var completedProjects = new ConcurrentDictionary<string, bool>();

                await _notificationService.NotifyProgressAsync(storyId,
                    $"Found {projectGroups.Count} project(s): {string.Join(", ", projectGroups.Select(g => g.ProjectName))}");

                // Process projects in dependency order, parallelize where possible
                var processingLevels = GetProcessingLevels(projectGroups);

                foreach (var level in processingLevels)
                {
                    ct.ThrowIfCancellationRequested();

                    await _notificationService.NotifyProgressAsync(storyId,
                        $"Processing level {level.Key + 1}: {string.Join(", ", level.Value.Select(g => g.ProjectName))} (parallel)");

                    // Process all projects in this level in parallel
                    var levelTasks = level.Value.Select(async projectGroup =>
                    {
                        var projectFiles = new Dictionary<string, string>();

                        foreach (var task in projectGroup.Tasks)
                        {
                            ct.ThrowIfCancellationRequested();

                            var actionType = task.IsModification ? "Modifying" : "Generating";
                            await _notificationService.NotifyProgressAsync(storyId,
                                $"[{projectGroup.ProjectName}] {actionType}: {task.Title}");

                            // Use modification-aware coding if codebase is available
                            AgentResponse response;
                            if (codebaseAnalysis != null && !string.IsNullOrEmpty(story?.CodebaseId))
                            {
                                response = await ExecuteModificationCodingAsync(task, projectState, story.CodebaseId, codebaseAnalysis, ct);
                            }
                            else
                            {
                                response = await ExecuteGenerationCodingAsync(task, projectState, ct);
                            }

                            if (response.Success && response.Data.TryGetValue("filename", out var filename) &&
                                response.Data.TryGetValue("code", out var code))
                            {
                                var filenameStr = filename.ToString()!;

                                // Avoid duplicate project name in path - if filename already includes project name, use it directly
                                string fileKey;
                                if (filenameStr.StartsWith(projectGroup.ProjectName + "/", StringComparison.OrdinalIgnoreCase) ||
                                    filenameStr.StartsWith(projectGroup.ProjectName + "\\", StringComparison.OrdinalIgnoreCase))
                                {
                                    fileKey = filenameStr.Replace("\\", "/");
                                }
                                else
                                {
                                    fileKey = $"{projectGroup.ProjectName}/{filenameStr}";
                                }

                                projectFiles[fileKey] = code.ToString()!;
                                generatedFiles[fileKey] = code.ToString()!;

                                // Track if this was a modification
                                if (response.Data.TryGetValue("is_modification", out var isMod) && (bool)isMod)
                                {
                                    modifiedFiles[fileKey] = code.ToString()!;

                                    // If we have the full path, we could write back to the original file
                                    if (response.Data.TryGetValue("full_path", out var fullPath) && !string.IsNullOrEmpty(fullPath?.ToString()))
                                    {
                                        _logger.LogInformation("[Coding] Modified file ready to write: {FullPath}", fullPath);
                                        // Note: Actual file write should happen after review/approval
                                    }
                                }
                            }

                            // Update task status
                            task.Status = Models.TaskStatus.Completed;
                            await _taskRepository.UpdateTaskAsync(storyId, task, ct);
                        }

                        completedProjects[projectGroup.ProjectName] = true;
                        await _notificationService.NotifyProgressAsync(storyId,
                            $"[{projectGroup.ProjectName}] Completed ({projectFiles.Count} files)");

                        return projectFiles;
                    });

                    // Wait for all projects in this level to complete
                    await Task.WhenAll(levelTasks);
                }

                // Update shared project state with all generated files
                foreach (var kvp in generatedFiles)
                {
                    projectState.Codebase[kvp.Key] = kvp.Value;
                }

                // Save output
                var outputPath = await _outputRepository.SaveOutputAsync(
                    storyId,
                    story?.Name ?? storyId,
                    generatedFiles.ToDictionary(k => k.Key, v => v.Value),
                    ct);

                return new
                {
                    Files = generatedFiles.ToDictionary(k => k.Key, v => v.Value),
                    ModifiedFiles = modifiedFiles.ToDictionary(k => k.Key, v => v.Value),
                    OutputPath = outputPath,
                    ProjectCount = projectGroups.Count,
                    ParallelLevels = processingLevels.Count,
                    ModificationCount = modifiedFiles.Count
                };
            }, ct);

            if (!codingResult.Approved) return;

            // Store generated files in execution for later use (e.g., fix task generation)
            if (codingResult.Result is { } result && result.GetType().GetProperty("Files")?.GetValue(result) is Dictionary<string, string> files)
            {
                execution.GeneratedFiles = files;
            }

            // Phase 4: Debugging (DebuggerAgent - verify and fix code)
            // IMPORTANT: For targeted method modifications, we compile with dummy wrappers
            // The actual codebase is NOT modified until Deployment phase
            var debugResult = await ExecutePhaseAsync(execution, PipelinePhase.Debugging, async () =>
            {
                await _notificationService.NotifyPhaseStartedAsync(storyId, PipelinePhase.Debugging,
                    "DebuggerAgent verifying and fixing code...");

                var files = await _outputRepository.GetGeneratedFilesAsync(storyId, ct);
                var tasks = await _taskRepository.GetByStoryAsync(storyId, ct);

                if (files.Count == 0)
                {
                    await _notificationService.NotifyProgressAsync(storyId, "No files to verify");
                    return new
                    {
                        Success = true,
                        TotalFiles = 0,
                        Message = "No files generated to verify"
                    };
                }

                // For targeted method modifications, create dummy wrappers for compilation
                var filesToDebug = new Dictionary<string, string>();
                
                foreach (var file in files)
                {
                    // Find the task for this file
                    var matchingTask = tasks.FirstOrDefault(t => 
                        t.TargetFiles.Any(tf => file.Key.EndsWith(tf, StringComparison.OrdinalIgnoreCase) ||
                                                tf.EndsWith(Path.GetFileName(file.Key), StringComparison.OrdinalIgnoreCase)));

                    // If this is a method-only modification, wrap it in a dummy class
                    if (matchingTask?.IsModification == true && 
                        !string.IsNullOrEmpty(matchingTask.TargetMethod) &&
                        !string.IsNullOrEmpty(matchingTask.CurrentContent))
                    {
                        var className = execution.TargetInfo?.Class ?? 
                                        Path.GetFileNameWithoutExtension(file.Key);
                        var namespaceName = matchingTask.Namespace ?? 
                                            ExtractNamespaceFromFile(matchingTask.CurrentContent) ?? 
                                            "TargetedModification";

                        // Create dummy wrapper for compile verification
                        var wrappedCode = CreateDummyWrapperForMethod(
                            file.Value, 
                            className, 
                            namespaceName, 
                            matchingTask.CurrentContent);

                        filesToDebug[file.Key] = wrappedCode;
                        
                        await _notificationService.NotifyProgressAsync(storyId, 
                            $"[Method-Only] Wrapping {matchingTask.TargetMethod}() in dummy class for compile check");
                        
                        _logger.LogInformation("[Debugging] Created dummy wrapper for method: {Method} in {File}",
                            matchingTask.TargetMethod, file.Key);
                    }
                    else
                    {
                        // Full file - use as is
                        filesToDebug[file.Key] = file.Value;
                    }
                }

                await _notificationService.NotifyProgressAsync(storyId, $"Building {filesToDebug.Count} files together...");

                // Use multi-file debugging - compile all files together in a single project
                // This allows test files to reference their implementation files
                var debugResponse = await _debuggerAgent.DebugMultipleFilesAsync(filesToDebug, ct);

                ct.ThrowIfCancellationRequested();

                if (debugResponse.Success)
                {
                    await _notificationService.NotifyProgressAsync(storyId, "All files compiled successfully!");

                    // Update files if they were fixed
                    if (debugResponse.Data.TryGetValue("files", out var fixedFilesObj) &&
                        fixedFilesObj is Dictionary<string, string> fixedFiles)
                    {
                        // Save any fixed files back to output
                        foreach (var (filename, code) in fixedFiles)
                        {
                            if (files.TryGetValue(filename, out var originalCode) && originalCode != code)
                            {
                                _logger.LogInformation("[Debugging] File was fixed: {Filename}", filename);
                                // The fixed code is already in the response
                            }
                        }
                    }
                }
                else
                {
                    await _notificationService.NotifyProgressAsync(storyId,
                        $"Build failed: {debugResponse.Error ?? "Unknown error"}");
                }

                var attempts = debugResponse.Data.TryGetValue("attempts", out var attemptsObj)
                    ? Convert.ToInt32(attemptsObj)
                    : 1;

                return new
                {
                    Success = debugResponse.Success,
                    TotalFiles = files.Count,
                    BuildOutput = debugResponse.Data.GetValueOrDefault("build_output", ""),
                    Attempts = attempts,
                    Error = debugResponse.Error
                };
            }, ct);

            if (!debugResult.Approved) return;

            // Phase 5: Review (ReviewerAgent - quality check)
            var reviewResult = await ExecutePhaseAsync(execution, PipelinePhase.Reviewing, async () =>
            {
                await _notificationService.NotifyPhaseStartedAsync(storyId, PipelinePhase.Reviewing, "Running code review...");

                var files = await _outputRepository.GetGeneratedFilesAsync(storyId, ct);
                var projectState = new ProjectState
                {
                    Story = content,
                    Codebase = files
                };

                var request = new AgentRequest
                {
                    Input = content,
                    ProjectState = projectState
                };

                var response = await _reviewerAgent.RunAsync(request, ct);
                if (response.Success)
                {
                    await _outputRepository.SaveReviewReportAsync(storyId, response.Output, ct);
                }

                return new
                {
                    Approved = response.Data.TryGetValue("approved", out var approved) && (bool)approved,
                    Summary = response.Data.GetValueOrDefault("summary", ""),
                    Verdict = response.Data.GetValueOrDefault("verdict", "")
                };
            }, ct);

            if (!reviewResult.Approved) return;

            // Phase 6: Deployment (DeploymentAgent - deploy to codebase and build)
            // Only run if a codebase is associated
            var deployCodebaseId = story.CodebaseId;
            if (!string.IsNullOrEmpty(deployCodebaseId))
            {
                // Store deployment result for potential rollback
                DeploymentResult? lastDeploymentResult = null;

                var deployResult = await ExecutePhaseAsync(execution, PipelinePhase.Deployment, async () =>
                {
                    await _notificationService.NotifyPhaseStartedAsync(storyId, PipelinePhase.Deployment,
                        "DeploymentAgent deploying to codebase...");

                    // Get the codebase path
                    var codebaseAnalysis = await _codebaseRepository.GetAnalysisAsync(deployCodebaseId, ct);
                    if (codebaseAnalysis == null || string.IsNullOrEmpty(codebaseAnalysis.CodebasePath))
                    {
                        return new
                        {
                            Success = false,
                            Error = "Codebase not found or path not set"
                        };
                    }

                    await _notificationService.NotifyProgressAsync(storyId, $"Deploying to: {codebaseAnalysis.CodebasePath}");
                    await _notificationService.NotifyProgressAsync(storyId,
                        $"Found {codebaseAnalysis.Projects.Count} projects in codebase");

                    // Get generated files
                    var files = await _outputRepository.GetGeneratedFilesAsync(storyId, ct);
                    if (files.Count == 0)
                    {
                        return new
                        {
                            Success = true,
                            Message = "No files to deploy"
                        };
                    }

                    await _notificationService.NotifyProgressAsync(storyId, $"Deploying {files.Count} files...");

                    // Get task metadata for modification info
                    var savedTasks = await _taskRepository.GetByStoryAsync(storyId, ct);
                    
                    // Build deployment files with modification metadata
                    var deploymentFiles = new List<DeploymentFile>();
                    foreach (var file in files)
                    {
                        var deployFile = new DeploymentFile
                        {
                            RelativePath = file.Key,
                            Content = file.Value,
                            IsModification = false
                        };

                        // Find matching task to get modification info
                        var matchingTask = savedTasks.FirstOrDefault(t => 
                            t.TargetFiles.Any(tf => file.Key.EndsWith(tf, StringComparison.OrdinalIgnoreCase) ||
                                                    tf.EndsWith(Path.GetFileName(file.Key), StringComparison.OrdinalIgnoreCase)));
                        
                        if (matchingTask?.IsModification == true)
                        {
                            deployFile.IsModification = true;
                            
                            // Extract target method from task description or target info
                            if (execution.TargetInfo != null && !string.IsNullOrEmpty(execution.TargetInfo.Method))
                            {
                                deployFile.TargetMethodName = execution.TargetInfo.Method;
                                deployFile.TargetClassName = execution.TargetInfo.Class;
                            }
                            
                            _logger.LogInformation("[Deployment] File marked as modification: {File}, Method: {Method}",
                                file.Key, deployFile.TargetMethodName ?? "N/A");
                        }

                        // Check if this is a test file that needs namespace conversion
                        var isTestFile = file.Key.Contains("Test", StringComparison.OrdinalIgnoreCase) ||
                                         matchingTask?.ProjectName?.Contains("Test", StringComparison.OrdinalIgnoreCase) == true;
                        
                        if (isTestFile && execution.TargetInfo != null)
                        {
                            deployFile.IsTestFile = true;
                            
                            // Get the real namespace from the target class file
                            if (!string.IsNullOrEmpty(execution.TargetInfo.FileContent))
                            {
                                deployFile.RealClassNamespace = ExtractNamespaceFromFile(execution.TargetInfo.FileContent);
                            }
                            deployFile.RealClassName = execution.TargetInfo.Class;
                            
                            _logger.LogInformation("[Deployment] Test file will use real namespace: {NS} for class {Class}",
                                deployFile.RealClassNamespace ?? "N/A", deployFile.RealClassName ?? "N/A");
                        }

                        deploymentFiles.Add(deployFile);
                    }

                    // Deploy files to codebase using full analysis for accurate project paths
                    var deploymentResult = await _deploymentAgent.DeployAsync(codebaseAnalysis, deploymentFiles, ct);

                    // Store for potential rollback
                    lastDeploymentResult = deploymentResult;

                    if (deploymentResult.Success)
                    {
                        await _notificationService.NotifyProgressAsync(storyId,
                            $"Deployment successful! {deploymentResult.TotalFilesCopied} files deployed, build passed.");
                    }
                    else
                    {
                        await _notificationService.NotifyProgressAsync(storyId,
                            $"Deployment failed: {deploymentResult.Error}");
                    }

                    return new
                    {
                        Success = deploymentResult.Success,
                        Error = deploymentResult.Error,
                        FilesCopied = deploymentResult.TotalFilesCopied,
                        NewFiles = deploymentResult.NewFilesCreated,
                        ModifiedFiles = deploymentResult.FilesModified,
                        BuildResults = deploymentResult.BuildResults.Select(b => new
                        {
                            b.Success,
                            b.TargetPath,
                            b.Error
                        }).ToList()
                    };
                }, ct);

                // If deployment was rejected, rollback the changes
                if (!deployResult.Approved)
                {
                    if (lastDeploymentResult != null && lastDeploymentResult.TotalFilesCopied > 0)
                    {
                        _logger.LogInformation("[{StoryId}] Deployment rejected, starting rollback...", storyId);
                        await _notificationService.NotifyProgressAsync(storyId, "Rolling back deployment changes...");

                        var rollbackResult = await _deploymentAgent.RollbackAsync(lastDeploymentResult, ct);

                        if (rollbackResult.Success)
                        {
                            await _notificationService.NotifyProgressAsync(storyId,
                                $"Rollback complete: {rollbackResult.DeletedFiles.Count} files deleted, {rollbackResult.RevertedProjects.Count} projects reverted");
                        }
                        else
                        {
                            await _notificationService.NotifyProgressAsync(storyId,
                                $"Rollback had errors: {string.Join(", ", rollbackResult.Errors)}");
                        }
                    }

                    // Mark story as failed
                    await _storyRepository.UpdateStatusAsync(storyId, StoryStatus.Failed, ct);
                    await _notificationService.NotifyPhaseFailedAsync(storyId, PipelinePhase.Deployment,
                        execution.RejectionReason ?? "Deployment rejected by user");

                    return;
                }

                // Store deployment result for potential retry
                execution.LastDeploymentResult = lastDeploymentResult;

                // Phase 7: Integration Testing - Run new/modified tests in the deployed codebase
                var integrationTestResult = await ExecuteUnitTestingPhaseAsync(
                    execution, codebaseAnalysis!, lastDeploymentResult!, ct);

                if (!integrationTestResult.Approved)
                {
                    // Check if we need to retry or abort
                    if (integrationTestResult.NeedsRetry && execution.RetryAttempt < PipelineExecution.MaxRetryAttempts)
                    {
                        // Rollback and retry from Coding phase
                        await HandleRetryAsync(execution, integrationTestResult.FixTasks!, ct);

                        await _notificationService.NotifyProgressAsync(storyId,
                            $"🔄 Restarting from Coding phase with fix tasks...");

                        // CRITICAL: Re-execute Coding phase with fix tasks
                        // Reset phases from Coding onwards to Pending
                        foreach (var phase in execution.Status.Phases)
                        {
                            if (phase.Phase >= PipelinePhase.Coding && phase.Phase != PipelinePhase.Analysis && phase.Phase != PipelinePhase.Planning)
                            {
                                phase.State = PhaseState.Pending;
                                phase.Result = null;
                                phase.Message = null;
                                phase.StartedAt = null;
                                phase.CompletedAt = null;
                            }
                        }

                        // Re-run the pipeline from Coding phase (recursive call with retry context)
                        await ExecuteRetryFromCodingAsync(execution, codebaseAnalysis, story, ct);
                        return;
                    }

                    // Rollback deployment
                    if (lastDeploymentResult != null && lastDeploymentResult.TotalFilesCopied > 0)
                    {
                        await _deploymentAgent.RollbackAsync(lastDeploymentResult, ct);
                    }
                    await _storyRepository.UpdateStatusAsync(storyId, StoryStatus.Failed, ct);
                    return;
                }

                // Phase 8: Pull Request Creation (OPTIONAL)
                // User can choose to create PR or complete without PR
                var prPhaseStatus = execution.Status.Phases.First(p => p.Phase == PipelinePhase.PullRequest);
                execution.CurrentPhase = PipelinePhase.PullRequest;
                execution.Status.CurrentPhase = PipelinePhase.PullRequest;
                prPhaseStatus.State = PhaseState.WaitingApproval;
                prPhaseStatus.StartedAt = DateTime.UtcNow;

                var prInfo = new
                {
                    BranchName = $"feature/ai-{story?.Name?.ToLowerInvariant().Replace(" ", "-") ?? storyId}",
                    FileCount = lastDeploymentResult?.TotalFilesCopied ?? 0,
                    NewFiles = lastDeploymentResult?.NewFilesCreated ?? 0,
                    ModifiedFiles = lastDeploymentResult?.FilesModified ?? 0,
                    IsOptional = true,
                    Message = "GitHub PR creation is optional. You can complete now or create a PR."
                };

                prPhaseStatus.Result = prInfo;

                await _notificationService.NotifyPhasePendingApprovalAsync(
                    storyId,
                    PipelinePhase.PullRequest,
                    $"Deployment successful! {prInfo.FileCount} files deployed. Create GitHub PR or complete?",
                    prInfo);

                // Wait for user decision (approve = create PR, reject = complete without PR)
                bool createPr;
                if (execution.AutoApproveAll)
                {
                    createPr = false; // Skip PR in auto mode
                }
                else
                {
                    execution.PhaseApprovalTcs = new TaskCompletionSource<bool>();
                    createPr = await execution.PhaseApprovalTcs.Task;
                }

                if (createPr)
                {
                    // TODO: Implement actual GitHub PR creation
                    await _notificationService.NotifyProgressAsync(storyId,
                        "GitHub PR integration coming soon. For now, changes are deployed to codebase.");
                    prPhaseStatus.State = PhaseState.Completed;
                    prPhaseStatus.Message = "PR creation skipped (not yet implemented)";
                }
                else
                {
                    // User chose to complete without PR
                    prPhaseStatus.State = PhaseState.Skipped;
                    prPhaseStatus.Message = "Completed without PR";
                    await _notificationService.NotifyProgressAsync(storyId,
                        "Completing without GitHub PR. Changes are deployed to codebase.");
                }

                prPhaseStatus.CompletedAt = DateTime.UtcNow;
                await _notificationService.NotifyPhaseCompletedAsync(storyId, PipelinePhase.PullRequest,
                    createPr ? "PR phase completed" : "Completed without PR");
            }

            // Mark as completed (this also clears InProgress)
            await _storyRepository.UpdateStatusAsync(storyId, StoryStatus.Completed, ct);

            var outputPath = await _outputRepository.GetOutputPathAsync(storyId, ct);
            await _notificationService.NotifyPipelineCompletedAsync(storyId, outputPath ?? "");

            execution.Status.CurrentPhase = PipelinePhase.Completed;
            execution.Status.CompletedAt = DateTime.UtcNow;
            execution.Status.IsRunning = false;

            // Store completed status for later retrieval (in-memory)
            _completedPipelines[storyId] = CloneStatus(execution.Status);

            // Save pipeline history to disk for permanent access
            await _outputRepository.SavePipelineHistoryAsync(storyId, execution.Status, ct);

            // ═══════════════════════════════════════════════════════════════════════════════
            // Knowledge Base: Capture successful patterns (V-Bounce Step 6)
            // ═══════════════════════════════════════════════════════════════════════════════
            try
            {
                await CaptureKnowledgeFromPipelineAsync(storyId, execution, ct);
            }
            catch (Exception kbEx)
            {
                // Don't fail the pipeline if knowledge capture fails
                _logger.LogWarning(kbEx, "[{StoryId}] Knowledge capture failed (non-fatal)", storyId);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("[{StoryId}] Pipeline cancelled", storyId);
            await _approvalRepository.ResetInProgressAsync(storyId, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{StoryId}] Pipeline failed", storyId);
            await _notificationService.NotifyPhaseFailedAsync(storyId, execution.CurrentPhase, ex.Message);
            await _approvalRepository.ResetInProgressAsync(storyId, CancellationToken.None);
        }
        finally
        {
            execution.Status.IsRunning = false;
            _runningPipelines.TryRemove(storyId, out _);
            await _notificationService.NotifyStoryListChangedAsync();
        }
    }

    private async Task<PhaseResult> ExecutePhaseAsync(
        PipelineExecution execution,
        PipelinePhase phase,
        Func<Task<object>> action,
        CancellationToken ct)
    {
        execution.CurrentPhase = phase;
        execution.Status.CurrentPhase = phase;

        var phaseStatus = execution.Status.Phases.First(p => p.Phase == phase);
        phaseStatus.State = PhaseState.Running;
        phaseStatus.StartedAt = DateTime.UtcNow;

        try
        {
            var result = await action();

            phaseStatus.Result = result;
            phaseStatus.State = PhaseState.WaitingApproval;

            await _notificationService.NotifyPhasePendingApprovalAsync(
                execution.StoryId, phase,
                $"{phase} completed. Waiting for approval...",
                result);

            // Wait for approval (or auto-approve)
            bool approved;
            if (execution.AutoApproveAll)
            {
                approved = true;
            }
            else
            {
                execution.PhaseApprovalTcs = new TaskCompletionSource<bool>();
                approved = await execution.PhaseApprovalTcs.Task;
            }

            if (approved)
            {
                phaseStatus.State = PhaseState.Completed;
                phaseStatus.CompletedAt = DateTime.UtcNow;
                await _notificationService.NotifyPhaseCompletedAsync(execution.StoryId, phase, $"{phase} approved");
            }
            else
            {
                phaseStatus.State = PhaseState.Skipped;
                phaseStatus.Message = execution.RejectionReason;
            }

            return new PhaseResult { Approved = approved, Result = result };
        }
        catch (Exception ex)
        {
            phaseStatus.State = PhaseState.Failed;
            phaseStatus.Message = ex.Message;
            throw;
        }
    }

    private PipelineStatusDto CreateInitialStatus(string storyId)
    {
        return new PipelineStatusDto
        {
            StoryId = storyId,
            CurrentPhase = PipelinePhase.None,
            IsRunning = false,
            Phases = new List<PhaseStatusDto>
            {
                new() { Phase = PipelinePhase.Analysis, State = PhaseState.Pending },           // CodeAnalysisAgent
                new() { Phase = PipelinePhase.Planning, State = PhaseState.Pending },           // PlannerAgent
                new() { Phase = PipelinePhase.Coding, State = PhaseState.Pending },             // CoderAgent
                new() { Phase = PipelinePhase.Debugging, State = PhaseState.Pending },          // DebuggerAgent
                new() { Phase = PipelinePhase.Reviewing, State = PhaseState.Pending },          // ReviewerAgent
                new() { Phase = PipelinePhase.Deployment, State = PhaseState.Pending },         // DeploymentAgent
                new() { Phase = PipelinePhase.UnitTesting, State = PhaseState.Pending }, // Test new/modified code
                new() { Phase = PipelinePhase.PullRequest, State = PhaseState.Pending }         // Create GitHub PR
            }
        };
    }

    private List<TaskDto> ConvertToTaskDtos(Dictionary<string, object> data)
    {
        var tasks = new List<TaskDto>();
        int globalIndex = 1;

        // Handle tasks from enhanced PlannerAgent (supports modification-aware planning)
        if (data.TryGetValue("tasks", out var tasksObj) && tasksObj is List<SubTask> subTasks)
        {
            // Get modification task metadata if available
            var modificationTasks = data.TryGetValue("modification_tasks", out var modTasksObj)
                ? modTasksObj as List<FileModificationTask>
                : null;

            // Group tasks by project for ordering
            var tasksByProject = subTasks
                .GroupBy(t => t.ProjectName ?? "default")
                .ToList();

            int projectOrder = 0;
            foreach (var projectGroup in tasksByProject)
            {
                foreach (var st in projectGroup.OrderBy(t => t.Index))
                {
                    // Convert DependsOn (task indices) to DependsOnProjects (project names)
                    var dependsOnProjects = st.DependsOn
                        .Select(idx => subTasks.FirstOrDefault(t => t.Index == idx)?.ProjectName)
                        .Where(p => !string.IsNullOrEmpty(p) && p != projectGroup.Key)
                        .Distinct()
                        .ToList();

                    // Find matching modification task for full path
                    var targetFile = st.TargetFiles.FirstOrDefault() ?? "";
                    var matchingModTask = modificationTasks?.FirstOrDefault(mt =>
                        mt.FilePath.EndsWith(targetFile, StringComparison.OrdinalIgnoreCase) ||
                        targetFile.EndsWith(mt.FilePath, StringComparison.OrdinalIgnoreCase));

                    tasks.Add(new TaskDto
                    {
                        Index = globalIndex++,
                        Title = st.Title,
                        Description = st.Description,
                        TargetFiles = st.TargetFiles,
                        ProjectName = st.ProjectName ?? "default",
                        DependsOnProjects = dependsOnProjects!,
                        ProjectOrder = projectOrder,
                        Status = Models.TaskStatus.Pending,
                        UsesExisting = st.UsesExisting,
                        IsModification = st.IsModification,
                        FullPath = st.FullPath ?? matchingModTask?.FullPath,
                        Namespace = st.Namespace,
                        TargetMethod = st.TargetMethod,
                        CurrentContent = st.CurrentContent
                    });
                }
                projectOrder++;
            }
        }

        return tasks;
    }

    private class PipelineExecution
    {
        public string StoryId { get; set; } = string.Empty;
        public bool AutoApproveAll { get; set; }
        public PipelineStatusDto Status { get; set; } = new();
        public PipelinePhase CurrentPhase { get; set; }
        public TaskCompletionSource<bool>? PhaseApprovalTcs { get; set; }
        public string? RejectionReason { get; set; }
        public CancellationTokenSource CancellationTokenSource { get; set; } = new();

        // Retry tracking
        public int RetryAttempt { get; set; } = 0;
        public const int MaxRetryAttempts = 3;
        public RetryInfoDto? CurrentRetryInfo { get; set; }
        public TaskCompletionSource<RetryAction>? RetryApprovalTcs { get; set; }
        public List<FixTaskDto> PendingFixTasks { get; set; } = new();

        // Deployment state for retry
        public DeploymentResult? LastDeploymentResult { get; set; }
        public Dictionary<string, string>? GeneratedFiles { get; set; }

        // Target info from story (for limiting scope)
        public TargetInfo? TargetInfo { get; set; }
    }

    /// <summary>
    /// Target information from story - limits pipeline scope to specific files/methods
    /// </summary>
    private class TargetInfo
    {
        // Code target
        public string? Project { get; set; }
        public string? File { get; set; }
        public string? Class { get; set; }
        public string? Method { get; set; }
        public ChangeType ChangeType { get; set; }
        public string? FileContent { get; set; }
        public string? MethodContent { get; set; }

        // Test target
        public string? TestProject { get; set; }
        public string? TestFile { get; set; }
        public string? TestClass { get; set; }
        public string? TestFileContent { get; set; }

        public bool HasCodeTarget => !string.IsNullOrEmpty(File) || !string.IsNullOrEmpty(Class);
        public bool HasTestTarget => !string.IsNullOrEmpty(TestFile) || !string.IsNullOrEmpty(TestClass);
    }

    private class PhaseResult
    {
        public bool Approved { get; set; }
        public object? Result { get; set; }
        public bool NeedsRetry { get; set; }
        public List<FixTaskDto>? FixTasks { get; set; }
    }

    /// <summary>
    /// Groups tasks by their project name for parallel execution
    /// </summary>
    private List<ProjectTaskGroup> GroupTasksByProject(List<TaskDto> tasks)
    {
        var groups = tasks
            .GroupBy(t => string.IsNullOrEmpty(t.ProjectName) ? "default" : t.ProjectName)
            .Select(g => new ProjectTaskGroup
            {
                ProjectName = g.Key,
                Order = g.Min(t => t.ProjectOrder),
                DependsOn = g.SelectMany(t => t.DependsOnProjects).Distinct().ToList(),
                Tasks = g.OrderBy(t => t.Index).ToList()
            })
            .OrderBy(g => g.Order)
            .ToList();

        return groups;
    }

    /// <summary>
    /// Organizes project groups into processing levels based on dependencies.
    /// Projects in the same level can be processed in parallel.
    /// </summary>
    private Dictionary<int, List<ProjectTaskGroup>> GetProcessingLevels(List<ProjectTaskGroup> projectGroups)
    {
        var levels = new Dictionary<int, List<ProjectTaskGroup>>();
        var assigned = new HashSet<string>();
        var currentLevel = 0;

        while (assigned.Count < projectGroups.Count)
        {
            var levelGroups = new List<ProjectTaskGroup>();

            foreach (var group in projectGroups)
            {
                if (assigned.Contains(group.ProjectName))
                    continue;

                // Check if all dependencies are satisfied
                var dependenciesMet = group.DependsOn.All(dep =>
                    assigned.Contains(dep) || !projectGroups.Any(g => g.ProjectName == dep));

                if (dependenciesMet)
                {
                    levelGroups.Add(group);
                }
            }

            if (levelGroups.Count == 0 && assigned.Count < projectGroups.Count)
            {
                // Circular dependency detected, add remaining as final level
                _logger.LogWarning("Possible circular dependency detected in project groups");
                levelGroups = projectGroups.Where(g => !assigned.Contains(g.ProjectName)).ToList();
            }

            foreach (var group in levelGroups)
            {
                assigned.Add(group.ProjectName);
            }

            if (levelGroups.Count > 0)
            {
                levels[currentLevel] = levelGroups;
                currentLevel++;
            }
        }

        return levels;
    }

    /// <summary>
    /// Try to extract a class name from the story text for modification planning.
    /// Returns null if no specific class is mentioned.
    /// </summary>
    private string? ExtractTargetClassName(string story)
    {
        // Common words to exclude (not class names)
        var excludeWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "the", "a", "an", "this", "that", "new", "code", "method", "function",
            "class", "interface", "add", "update", "modify", "change", "fix", "create",
            "test", "init", "with", "for", "from", "into", "parameter", "keepalive"
        };

        // Pattern 1: Look for PascalCase class names (e.g., DigitalApiClient, LogRotator)
        var pascalCasePattern = @"\b([A-Z][a-z]+(?:[A-Z][a-z0-9]+)+)\b";
        var pascalMatches = System.Text.RegularExpressions.Regex.Matches(story, pascalCasePattern);
        foreach (System.Text.RegularExpressions.Match match in pascalMatches)
        {
            var className = match.Groups[1].Value;
            if (!excludeWords.Contains(className) && className.Length > 3)
            {
                _logger.LogInformation("[ExtractClassName] Found PascalCase class: {ClassName}", className);
                return className;
            }
        }

        // Pattern 2: Look for explicit class mentions
        var patterns = new[]
        {
            @"(?:modify|update|change|extend|add\s+to|fix)\s+(?:the\s+)?([A-Z]\w+)(?:\s+class)?",
            @"([A-Z]\w+)(?:\.cs|\.Init|\.Create|\s+class|\s+sınıf|\s+sınıfı)",
            @"(?:in|to|for)\s+(?:the\s+)?([A-Z]\w+)",
        };

        foreach (var pattern in patterns)
        {
            var match = System.Text.RegularExpressions.Regex.Match(
                story, pattern, System.Text.RegularExpressions.RegexOptions.None);

            if (match.Success)
            {
                var className = match.Groups[1].Value;
                if (!excludeWords.Contains(className) && className.Length > 2)
                {
                    _logger.LogInformation("[ExtractClassName] Found class via pattern: {ClassName}", className);
                    return className;
                }
            }
        }

        _logger.LogInformation("[ExtractClassName] No class name detected in story");
        return null;
    }

    /// <summary>
    /// Extract a specific method's content from a C# file
    /// </summary>
    private string? ExtractMethodContent(string fileContent, string methodName)
    {
        if (string.IsNullOrEmpty(fileContent) || string.IsNullOrEmpty(methodName))
            return null;

        // Pattern to find method with its body (handles async, public/private, return types, etc.)
        var pattern = $@"((?:public|private|protected|internal|static|async|override|virtual|\s)+[\w<>\[\],\s\?]+\s+{System.Text.RegularExpressions.Regex.Escape(methodName)}\s*(?:<[^>]+>)?\s*\([^)]*\)\s*(?:where[^{{]+)?\s*\{{)";
        
        var match = System.Text.RegularExpressions.Regex.Match(fileContent, pattern, System.Text.RegularExpressions.RegexOptions.Singleline);
        
        if (!match.Success)
        {
            _logger.LogWarning("[ExtractMethod] Method {MethodName} not found in file", methodName);
            return null;
        }

        // Find the matching closing brace
        var startIndex = match.Index;
        var braceCount = 0;
        var endIndex = startIndex;
        var inString = false;
        var inChar = false;
        var escaped = false;

        for (var i = match.Index + match.Length - 1; i < fileContent.Length; i++)
        {
            var c = fileContent[i];

            if (escaped)
            {
                escaped = false;
                continue;
            }

            if (c == '\\')
            {
                escaped = true;
                continue;
            }

            if (c == '"' && !inChar) inString = !inString;
            if (c == '\'' && !inString) inChar = !inChar;

            if (!inString && !inChar)
            {
                if (c == '{') braceCount++;
                if (c == '}')
                {
                    braceCount--;
                    if (braceCount == 0)
                    {
                        endIndex = i + 1;
                        break;
                    }
                }
            }
        }

        if (endIndex > startIndex)
        {
            var methodContent = fileContent.Substring(startIndex, endIndex - startIndex);
            _logger.LogInformation("[ExtractMethod] Extracted method {MethodName} ({Length} chars)", methodName, methodContent.Length);
            return methodContent;
        }

        return null;
    }

    /// <summary>
    /// Create a targeted modification plan when user specified exact file/class/method
    /// This bypasses the LLM planning and creates a direct modification task
    /// </summary>
    private async Task<AgentResponse> PlanTargetedModificationAsync(
        string requirement,
        TargetInfo targetInfo,
        Core.Models.CodebaseAnalysis codebaseAnalysis,
        CancellationToken ct)
    {
        _logger.LogInformation("[Planning] Creating targeted modification plan for: {Project}/{File}/{Class}.{Method}",
            targetInfo.Project, targetInfo.File, targetInfo.Class, targetInfo.Method);

        var tasks = new List<SubTask>();
        var taskIndex = 1;

        // Task 1: Main code modification (if target file specified)
        if (!string.IsNullOrEmpty(targetInfo.File))
        {
            var changeTypeDesc = targetInfo.ChangeType switch
            {
                ChangeType.Create => "Create new code in",
                ChangeType.Modify => "Modify existing code in",
                ChangeType.Delete => "Delete code from",
                _ => "Modify"
            };

            string description;
            
            // If specific method is targeted, only generate the method body (not full file)
            if (!string.IsNullOrEmpty(targetInfo.Method))
            {
                description = $@"# Modify method `{targetInfo.Method}()` in `{targetInfo.Class}`

**Target Class:** {targetInfo.Class}
**Target Method:** {targetInfo.Method}
**Change Type:** {targetInfo.ChangeType}

## REQUIREMENT
{requirement}

## CURRENT METHOD IMPLEMENTATION
```csharp
{targetInfo.MethodContent ?? "// Method content not found"}
```

## OUTPUT INSTRUCTIONS - CRITICAL!
Output ONLY the modified method - NOT the entire file!

Your output must be ONLY the method like this:
```csharp
/// <summary>
/// XML documentation for the method
/// </summary>
public {GetMethodSignatureFromContent(targetInfo.MethodContent, targetInfo.Method)}
{{
    // Your modified implementation here
}}
```

DO NOT output:
- using statements
- namespace declaration
- class declaration
- other methods
- ANY code outside the single method

ONLY output the complete method with its signature and body.";

                tasks.Add(new SubTask
                {
                    Index = taskIndex++,
                    Title = $"Modify {targetInfo.Class}.{targetInfo.Method}()",
                    Description = description,
                    TargetFiles = new List<string> { targetInfo.File },
                    ProjectName = targetInfo.Project,
                    Namespace = ExtractNamespaceFromFile(targetInfo.FileContent),
                    IsModification = true,
                    TargetMethod = targetInfo.Method,
                    CurrentContent = targetInfo.FileContent,  // Store original file for merge
                    DependsOn = new List<int>()
                });
            }
            else
            {
                // Full file modification (no specific method)
                var fileContentSection = "";
                if (!string.IsNullOrEmpty(targetInfo.FileContent))
                {
                    fileContentSection = $@"

## CURRENT FILE CONTENT
```csharp
{targetInfo.FileContent}
```";
                }

                description = $@"# {changeTypeDesc} `{targetInfo.File}`

**Target Class:** {targetInfo.Class ?? "See file content"}
**Change Type:** {targetInfo.ChangeType}

## REQUIREMENT
{requirement}
{fileContentSection}

## INSTRUCTIONS
Output the COMPLETE modified file with all changes applied.";

                tasks.Add(new SubTask
                {
                    Index = taskIndex++,
                    Title = $"Modify {targetInfo.Class ?? Path.GetFileNameWithoutExtension(targetInfo.File)}",
                    Description = description,
                    TargetFiles = new List<string> { targetInfo.File },
                    ProjectName = targetInfo.Project,
                    Namespace = ExtractNamespaceFromFile(targetInfo.FileContent),
                    IsModification = true,
                    CurrentContent = targetInfo.FileContent,
                    DependsOn = new List<int>()
                });
            }
        }

        // Task 2: Unit test - either modify existing or create new
        if (!string.IsNullOrEmpty(targetInfo.TestProject))
        {
            // If TestClass is specified → modify existing test file
            // If TestClass is NOT specified → create new test class
            var hasExistingTestClass = !string.IsNullOrEmpty(targetInfo.TestClass) && !string.IsNullOrEmpty(targetInfo.TestFile);

            if (hasExistingTestClass)
            {
                // MODIFY existing test class - add tests for the TARGET METHOD ONLY
                var targetMethodInfo = !string.IsNullOrEmpty(targetInfo.Method)
                    ? $"**Target Method Being Tested:** `{targetInfo.Method}`"
                    : "**Target:** All changes in the previous task";

                var testDescription = $@"Update or add tests in the existing test file `{targetInfo.TestFile}`.

**Target Test Class:** {targetInfo.TestClass}
{targetMethodInfo}

**REQUIREMENT:**
Add unit tests ONLY for the method/changes specified above. Do NOT write tests for other methods.

**Current Test File Content:**
```csharp
{targetInfo.TestFileContent ?? "// Test file content will be provided"}
```

**CRITICAL INSTRUCTIONS:**
- Add new test methods ONLY for `{targetInfo.Method ?? "the modified code"}`
- Do NOT create tests for unrelated methods in the class
- Do NOT create a new test class
- Do NOT create a new test file
- Keep ALL existing tests intact (do not modify or delete them)
- Follow the existing test naming convention
- Test method naming: {targetInfo.Method ?? "MethodName"}_Scenario_ExpectedResult
- Output the COMPLETE modified test file";

                tasks.Add(new SubTask
                {
                    Index = taskIndex++,
                    Title = $"Add tests for {targetInfo.Method ?? "modified code"} in {targetInfo.TestClass}",
                    Description = testDescription,
                    TargetFiles = new List<string> { targetInfo.TestFile! },
                    ProjectName = targetInfo.TestProject,
                    Namespace = ExtractNamespaceFromFile(targetInfo.TestFileContent),
                    IsModification = true,
                    TargetMethod = targetInfo.Method,  // Track which method is being tested
                    DependsOn = new List<int> { 1 }
                });
            }
            else
            {
                // CREATE new test class - but ONLY for the target method
                var targetClassName = targetInfo.Class ?? "Target";
                var targetMethodName = targetInfo.Method;
                
                // If we have a specific method, create a focused test class
                var newTestClassName = !string.IsNullOrEmpty(targetMethodName)
                    ? $"{targetClassName}_{targetMethodName}Tests"  // e.g., FileLogger_CreateLogSourceTests
                    : $"{targetClassName}Tests";
                var testFileName = $"{newTestClassName}.cs";
                
                // Determine test file path based on code file structure
                var testFilePath = !string.IsNullOrEmpty(targetInfo.File) 
                    ? Path.Combine(Path.GetDirectoryName(targetInfo.File) ?? "", testFileName)
                    : testFileName;

                var targetMethodInfo = !string.IsNullOrEmpty(targetMethodName)
                    ? $@"**IMPORTANT: This test class is ONLY for the `{targetMethodName}` method.**

**Target Method:** `{targetMethodName}`"
                    : "**Target:** All changes in the previous task";

                var testDescription = $@"Create a NEW unit test class for `{targetClassName}`.

**New Test Class Name:** {newTestClassName}
**Test File:** {testFilePath}
**Test Project:** {targetInfo.TestProject}

{targetMethodInfo}

**REQUIREMENT:**
Write unit tests ONLY for the `{targetMethodName ?? "modified method"}`. Do NOT test other methods in the class.

**Target Code Being Tested:**
- Class: {targetInfo.Class ?? "See previous task"}
- Method: {targetMethodName ?? "Modified method only"}
- Project: {targetInfo.Project ?? "See previous task"}

**INSTRUCTIONS:**
- Create a NEW test class named `{newTestClassName}`
- Write tests ONLY for `{targetMethodName ?? "the modified method"}`, not other methods
- Use NUnit framework with [TestFixture] and [Test] attributes
- Use FluentAssertions for assertions
- Follow Arrange-Act-Assert pattern
- Test method naming: {targetMethodName ?? "MethodName"}_Scenario_ExpectedResult
- Include tests for:
  - Normal/happy path scenarios
  - Edge cases (null inputs, boundary values)
  - Error conditions (if applicable)
- Add XML documentation for the test class
- Keep the test class focused and minimal";

                tasks.Add(new SubTask
                {
                    Index = taskIndex++,
                    Title = $"Create unit tests for {targetMethodName ?? targetClassName}",
                    Description = testDescription,
                    TargetFiles = new List<string> { testFilePath },
                    ProjectName = targetInfo.TestProject,
                    Namespace = $"{targetInfo.TestProject}",  // Will be adjusted by coder
                    IsModification = false,  // This is a CREATE, not modification
                    TargetMethod = targetMethodName,  // Track which method is being tested
                    DependsOn = new List<int> { 1 }
                });
            }
        }

        _logger.LogInformation("[Planning] Created {Count} targeted modification task(s)", tasks.Count);

        return await Task.FromResult(new AgentResponse
        {
            Success = true,
            Output = $"Targeted modification plan: {tasks.Count} task(s)",
            Data = new Dictionary<string, object>
            {
                ["project_name"] = targetInfo.Project ?? "Modification",
                ["summary"] = $"Targeted modification of {targetInfo.Class ?? targetInfo.File}",
                ["tasks"] = tasks,
                ["is_targeted_modification"] = true
            },
            TokensUsed = 0  // No LLM call needed
        });
    }

    /// <summary>
    /// Extract namespace from file content
    /// </summary>
    private string? ExtractNamespaceFromFile(string? fileContent)
    {
        if (string.IsNullOrEmpty(fileContent))
            return null;

        var match = System.Text.RegularExpressions.Regex.Match(fileContent, @"namespace\s+([\w.]+)");
        return match.Success ? match.Groups[1].Value : null;
    }

    /// <summary>
    /// Extract method signature hint from method content for LLM guidance
    /// </summary>
    private static string GetMethodSignatureFromContent(string? methodContent, string methodName)
    {
        if (string.IsNullOrEmpty(methodContent))
            return $"void {methodName}()";

        // Try to extract signature from method content
        var signatureMatch = System.Text.RegularExpressions.Regex.Match(
            methodContent, 
            @"((?:public|private|protected|internal|static|virtual|override|async|sealed|new|\s)+[\w<>\[\],\?\s]+\s+" + 
            System.Text.RegularExpressions.Regex.Escape(methodName) + @"\s*(?:<[^>]+>)?\s*\([^)]*\))",
            System.Text.RegularExpressions.RegexOptions.Singleline);

        if (signatureMatch.Success)
        {
            var signature = signatureMatch.Groups[1].Value.Trim();
            // Remove access modifiers for cleaner output hint
            return System.Text.RegularExpressions.Regex.Replace(signature, @"^(public|private|protected|internal)\s+", "");
        }

        return $"void {methodName}()";
    }

    /// <summary>
    /// Create a dummy wrapper class for compiling a standalone method during debugging.
    /// This allows verifying the method compiles without modifying the actual codebase.
    /// </summary>
    private string CreateDummyWrapperForMethod(string methodCode, string className, string namespaceName, string? originalFileContent)
    {
        // Extract using statements from original file
        var usings = new List<string> { "using System;", "using System.Collections.Generic;", "using System.Linq;", "using System.Threading.Tasks;" };
        
        if (!string.IsNullOrEmpty(originalFileContent))
        {
            var usingMatches = System.Text.RegularExpressions.Regex.Matches(originalFileContent, @"^using\s+[^;]+;", System.Text.RegularExpressions.RegexOptions.Multiline);
            foreach (System.Text.RegularExpressions.Match match in usingMatches)
            {
                if (!usings.Contains(match.Value))
                    usings.Add(match.Value);
            }
        }

        // Extract field dependencies from original class (simple heuristic)
        var fields = "";
        if (!string.IsNullOrEmpty(originalFileContent))
        {
            var fieldMatches = System.Text.RegularExpressions.Regex.Matches(
                originalFileContent, 
                @"^\s*(private|protected|internal|public)?\s*(readonly\s+)?(static\s+)?[\w<>\[\],\?\s]+\s+_?\w+\s*(=|;)",
                System.Text.RegularExpressions.RegexOptions.Multiline);
            
            foreach (System.Text.RegularExpressions.Match match in fieldMatches)
            {
                // Convert to simple placeholder fields
                var fieldLine = match.Value.Trim();
                if (fieldLine.Contains("="))
                    fieldLine = fieldLine.Substring(0, fieldLine.IndexOf("=")) + " = default!;";
                else if (!fieldLine.EndsWith(";"))
                    fieldLine += ";";
                    
                fields += "        " + fieldLine + "\n";
            }
        }

        return $@"{string.Join("\n", usings)}

namespace {namespaceName}
{{
    /// <summary>
    /// DUMMY WRAPPER CLASS for compile verification only.
    /// This class is used during debugging to verify the method compiles.
    /// The actual deployment will merge only the method into the real file.
    /// </summary>
    public class {className}
    {{
{fields}
        #region Method Under Test
        
{methodCode}

        #endregion
    }}
}}";
    }

    /// <summary>
    /// Execute modification-aware planning using CodeAnalysisAgent
    /// </summary>
    private async Task<AgentResponse> ExecuteModificationPlanningAsync(
        string story,
        string codebaseId,
        Core.Models.CodebaseAnalysis analysis,
        CancellationToken ct)
    {
        // Try to extract the target class name from the story
        var targetClassName = ExtractTargetClassName(story);

        if (string.IsNullOrEmpty(targetClassName))
        {
            _logger.LogInformation("[Planning] No specific class detected in story, using standard planning");
            return await _plannerAgent.PlanWithCodebaseAsync(story, analysis, null, ct);
        }

        _logger.LogInformation("[Planning] Detected target class: {ClassName}, searching for references", targetClassName);

        // Find the class
        var classResult = await _codeAnalysisAgent.FindClassAsync(analysis, targetClassName, includeContent: true);

        if (!classResult.Found)
        {
            _logger.LogWarning("[Planning] Class {ClassName} not found in codebase, using standard planning", targetClassName);
            return await _plannerAgent.PlanWithCodebaseAsync(story, analysis, null, ct);
        }

        _logger.LogInformation("[Planning] Found class {ClassName} at {FilePath}", targetClassName, classResult.FilePath);

        // Find all references to the class
        var referenceResult = await _codeAnalysisAgent.FindReferencesAsync(analysis, targetClassName, ct);

        _logger.LogInformation("[Planning] Found {RefCount} references in {FileCount} files",
            referenceResult.References.Count, referenceResult.AffectedFiles.Count);

        // Create modification tasks
        var modificationTasks = await _codeAnalysisAgent.CreateModificationTasksAsync(
            analysis, referenceResult, $"Modification for: {story}", ct);

        // Use modification planning
        return await _plannerAgent.PlanModificationAsync(
            story,
            classResult,
            referenceResult,
            modificationTasks,
            analysis,
            ct);
    }

    /// <summary>
    /// Execute coding with modification support
    /// </summary>
    private async Task<AgentResponse> ExecuteModificationCodingAsync(
        TaskDto task,
        ProjectState projectState,
        string codebaseId,
        Core.Models.CodebaseAnalysis analysis,
        CancellationToken ct)
    {
        // Check if this task is a modification task
        if (task.IsModification)
        {
            // Try to load the current file content from file system first
            string? currentContent = null;

            if (!string.IsNullOrEmpty(task.FullPath))
            {
                currentContent = await _codeAnalysisAgent.GetFileContentAsync(task.FullPath);
            }

            // If file doesn't exist (e.g., after rollback), use the preserved ExistingCode
            if (string.IsNullOrEmpty(currentContent) && !string.IsNullOrEmpty(task.ExistingCode))
            {
                _logger.LogInformation("[Coding] Using preserved ExistingCode for task (file was rolled back): {Task}", task.Title);
                currentContent = task.ExistingCode;
            }

            if (string.IsNullOrEmpty(currentContent))
            {
                _logger.LogWarning("[Coding] Could not load content for modification task: {FilePath}", task.FullPath);
                // Fall back to generation mode
                return await ExecuteGenerationCodingAsync(task, projectState, ct);
            }

            _logger.LogInformation("[Coding] Executing modification for: {FilePath}", task.FullPath ?? task.TargetFiles.FirstOrDefault());

            var context = new Dictionary<string, string>
            {
                ["is_modification"] = "true",
                ["current_content"] = currentContent,
                ["target_file"] = task.TargetFiles.FirstOrDefault() ?? (task.FullPath != null ? Path.GetFileName(task.FullPath) : "unknown.cs"),
                ["task_index"] = task.Index.ToString(),
                ["project_name"] = task.ProjectName ?? "default"
            };

            if (!string.IsNullOrEmpty(task.FullPath))
            {
                context["full_path"] = task.FullPath;
            }

            // Add namespace if available
            if (!string.IsNullOrEmpty(task.Namespace))
            {
                context["target_namespace"] = task.Namespace;
            }

            var request = new AgentRequest
            {
                Input = task.Description,
                ProjectState = projectState,
                Context = context
            };

            return await _coderAgent.RunAsync(request, ct);
        }
        else
        {
            return await ExecuteGenerationCodingAsync(task, projectState, ct);
        }
    }

    /// <summary>
    /// Execute standard code generation (non-modification)
    /// </summary>
    private async Task<AgentResponse> ExecuteGenerationCodingAsync(
        TaskDto task,
        ProjectState projectState,
        CancellationToken ct)
    {
        var context = new Dictionary<string, string>
        {
            ["task_index"] = task.Index.ToString(),
            ["target_file"] = task.TargetFiles.FirstOrDefault() ?? "",
            ["project_name"] = task.ProjectName ?? "default"
        };

        // Add namespace if available (CRITICAL for correct code generation)
        if (!string.IsNullOrEmpty(task.Namespace))
        {
            context["target_namespace"] = task.Namespace;
            _logger.LogDebug("[Coding] Using namespace: {Namespace} for task {Index}", task.Namespace, task.Index);
        }

        var request = new AgentRequest
        {
            Input = task.Description,
            ProjectState = projectState,
            Context = context
        };

        return await _coderAgent.RunAsync(request, ct);
    }

    /// <summary>
    /// Internal class for grouping tasks by project
    /// </summary>
    private class ProjectTaskGroup
    {
        public string ProjectName { get; set; } = string.Empty;
        public int Order { get; set; }
        public List<string> DependsOn { get; set; } = new();
        public List<TaskDto> Tasks { get; set; } = new();
    }

    /// <summary>
    /// Execute integration testing phase - run new/modified tests in the deployed codebase
    /// </summary>
    private async Task<PhaseResult> ExecuteUnitTestingPhaseAsync(
        PipelineExecution execution,
        Core.Models.CodebaseAnalysis codebaseAnalysis,
        DeploymentResult deploymentResult,
        CancellationToken ct)
    {
        var storyId = execution.StoryId;
        execution.CurrentPhase = PipelinePhase.UnitTesting;
        execution.Status.CurrentPhase = PipelinePhase.UnitTesting;

        var phaseStatus = execution.Status.Phases.First(p => p.Phase == PipelinePhase.UnitTesting);
        phaseStatus.State = PhaseState.Running;
        phaseStatus.StartedAt = DateTime.UtcNow;

        try
        {
            await _notificationService.NotifyPhaseStartedAsync(storyId, PipelinePhase.UnitTesting,
                "Running integration tests on deployed code (using UnitTestAgent)...");

            // Use UnitTestAgent to run tests in parallel for all affected test projects
            await _notificationService.NotifyProgressAsync(storyId,
                "UnitTestAgent: Discovering affected test projects and classes...");

            var testExecutionSummary = await _unitTestAgent.RunTestsAsync(codebaseAnalysis, deploymentResult, ct);

            // Check if tests were skipped
            if (testExecutionSummary.Skipped)
            {
                await _notificationService.NotifyProgressAsync(storyId,
                    $"Integration tests skipped: {testExecutionSummary.SkipReason}");

                phaseStatus.State = PhaseState.Completed;
                phaseStatus.CompletedAt = DateTime.UtcNow;
                phaseStatus.Result = new { Skipped = true, Reason = testExecutionSummary.SkipReason };

                await _notificationService.NotifyPhaseCompletedAsync(storyId, PipelinePhase.UnitTesting,
                    $"Integration testing skipped - {testExecutionSummary.SkipReason}", phaseStatus.Result);

                return new PhaseResult { Approved = true, Result = phaseStatus.Result };
            }

            // Report test execution summary
            await _notificationService.NotifyProgressAsync(storyId,
                $"UnitTestAgent: Executed tests in {testExecutionSummary.ProjectResults.Count} project(s) in parallel");

            foreach (var projectResult in testExecutionSummary.ProjectResults)
            {
                await _notificationService.NotifyProgressAsync(storyId,
                    $"  [{projectResult.ProjectName}] {projectResult.Passed}/{projectResult.TotalTests} passed" +
                    (projectResult.Failed > 0 ? $", {projectResult.Failed} failed" : ""));
            }

            // Convert to API DTO
            var testResults = ConvertToTestSummaryDto(testExecutionSummary);

            // Notify test results via SignalR
            await _notificationService.NotifyTestResultsAsync(storyId, testResults);

            // Check for failures
            if (testResults.Failed > 0)
            {
                _logger.LogWarning("[{StoryId}] Integration tests failed: {Failed}/{Total}",
                    storyId, testResults.Failed, testResults.TotalTests);

                // Generate fix tasks from test failures with LLM analysis
                // LLM determines whether to fix test or implementation based on error analysis
                await _notificationService.NotifyProgressAsync(storyId, "🔍 Analyzing test failures with LLM to determine fix target...");
                var fixTasks = await GenerateFixTasksFromTestFailuresAsync(testResults, execution.GeneratedFiles, codebaseAnalysis, deploymentResult, ct);

                // Check if this is a breaking change (existing tests failing)
                if (testResults.IsBreakingChange)
                {
                    await _notificationService.NotifyProgressAsync(storyId,
                        $"⚠️ BREAKING CHANGE: {testResults.ExistingTestsFailed} existing test(s) now failing!");
                }

                // Create retry info
                var retryInfo = new RetryInfoDto
                {
                    CurrentAttempt = execution.RetryAttempt + 1,
                    MaxAttempts = PipelineExecution.MaxRetryAttempts,
                    Reason = RetryReason.TestsFailed,
                    FixTasks = fixTasks,
                    TestSummary = testResults,
                    LastError = $"{testResults.Failed} test(s) failed",
                    LastAttemptAt = DateTime.UtcNow
                };

                execution.CurrentRetryInfo = retryInfo;
                execution.Status.RetryInfo = retryInfo;
                execution.Status.RetryTargetPhase = PipelinePhase.Coding;

                // Notify about retry story
                await _notificationService.NotifyRetryRequiredAsync(storyId, PipelinePhase.UnitTesting, retryInfo);
                await _notificationService.NotifyFixTasksGeneratedAsync(storyId, fixTasks);

                phaseStatus.State = PhaseState.WaitingRetryApproval;
                phaseStatus.Result = new
                {
                    Success = false,
                    testResults.TotalTests,
                    testResults.Passed,
                    testResults.Failed,
                    testResults.IsBreakingChange,
                    FixTaskCount = fixTasks.Count
                };

                // Wait for retry approval
                if (!execution.AutoApproveAll)
                {
                    execution.RetryApprovalTcs = new TaskCompletionSource<RetryAction>();
                    var action = await execution.RetryApprovalTcs.Task;

                    switch (action)
                    {
                        case RetryAction.AutoFix:
                            return new PhaseResult
                            {
                                Approved = false,
                                NeedsRetry = true,
                                FixTasks = fixTasks,
                                Result = phaseStatus.Result
                            };

                        case RetryAction.SkipTests:
                            phaseStatus.State = PhaseState.Completed;
                            phaseStatus.Message = "Tests skipped by user";
                            return new PhaseResult { Approved = true, Result = phaseStatus.Result };

                        case RetryAction.Abort:
                            phaseStatus.State = PhaseState.Failed;
                            phaseStatus.Message = "Aborted by user";
                            return new PhaseResult { Approved = false, Result = phaseStatus.Result };

                        default: // ManualFix - user will fix manually
                            phaseStatus.State = PhaseState.Failed;
                            phaseStatus.Message = "Manual fix required";
                            return new PhaseResult { Approved = false, Result = phaseStatus.Result };
                    }
                }
                else
                {
                    // Auto-approve retry
                    return new PhaseResult
                    {
                        Approved = false,
                        NeedsRetry = true,
                        FixTasks = fixTasks,
                        Result = phaseStatus.Result
                    };
                }
            }

            // All tests passed!
            phaseStatus.State = PhaseState.WaitingApproval;
            phaseStatus.Result = new
            {
                Success = true,
                testResults.TotalTests,
                testResults.Passed,
                testResults.Failed,
                NewTestsPassed = testResults.NewTestsPassed
            };

            await _notificationService.NotifyPhasePendingApprovalAsync(storyId, PipelinePhase.UnitTesting,
                $"Integration tests passed! {testResults.Passed}/{testResults.TotalTests}", phaseStatus.Result);

            // Wait for approval
            bool approved;
            if (execution.AutoApproveAll)
            {
                approved = true;
            }
            else
            {
                execution.PhaseApprovalTcs = new TaskCompletionSource<bool>();
                approved = await execution.PhaseApprovalTcs.Task;
            }

            if (approved)
            {
                phaseStatus.State = PhaseState.Completed;
                phaseStatus.CompletedAt = DateTime.UtcNow;
                await _notificationService.NotifyPhaseCompletedAsync(storyId, PipelinePhase.UnitTesting,
                    "Integration testing approved");
            }
            else
            {
                phaseStatus.State = PhaseState.Skipped;
                phaseStatus.Message = execution.RejectionReason;
            }

            return new PhaseResult { Approved = approved, Result = phaseStatus.Result };
        }
        catch (Exception ex)
        {
            phaseStatus.State = PhaseState.Failed;
            phaseStatus.Message = ex.Message;
            throw;
        }
    }

    /// <summary>
    /// Convert TestExecutionSummary from UnitTestAgent to API DTO
    /// </summary>
    private TestSummaryDto ConvertToTestSummaryDto(TestExecutionSummary executionSummary)
    {
        var dto = new TestSummaryDto
        {
            TotalTests = executionSummary.TotalTests,
            Passed = executionSummary.Passed,
            Failed = executionSummary.Failed,
            Skipped = executionSummary.SkippedTestsCount,
            NewTestsPassed = executionSummary.NewTestsPassed,
            NewTestsFailed = executionSummary.NewTestsFailed,
            ExistingTestsFailed = executionSummary.ExistingTestsFailed,
            TotalDuration = executionSummary.Duration,
            FailedTests = executionSummary.FailedTests.Select(ft => new TestResultDto
            {
                TestName = ft.MethodName,
                ClassName = ft.ClassName,
                Passed = ft.Passed,
                IsNewTest = ft.IsNewTest,
                ErrorMessage = ft.ErrorMessage,
                StackTrace = ft.StackTrace,
                Duration = ft.Duration
            }).ToList()
        };

        return dto;
    }

    /// <summary>
    /// Analyze test failure using LLM to determine which file needs fixing
    /// Returns: "test" if test file needs fixing, "implementation" if impl needs fixing
    /// </summary>
    private async Task<TestFailureAnalysis> AnalyzeTestFailureWithLLMAsync(
        string testMethodName,
        string errorMessage,
        string? stackTrace,
        string testCode,
        string implementationCode,
        string testFileName,
        string implementationFileName,
        CancellationToken ct)
    {
        var prompt = $@"## TEST FAILURE ANALYSIS

A unit test has failed. Analyze the error and determine which file needs to be fixed.

### FAILED TEST
- **Test Method:** `{testMethodName}`
- **Error Message:** {errorMessage}
- **Stack Trace:** {stackTrace ?? "N/A"}

### TEST CODE ({testFileName})
```csharp
{testCode}
```

### IMPLEMENTATION CODE ({implementationFileName})
```csharp
{implementationCode}
```

## YOUR TASK
Analyze the test failure and determine:
1. Is the TEST code wrong (incorrect assertion, wrong expected value)?
2. Or is the IMPLEMENTATION code wrong (buggy logic, missing functionality)?

## RESPONSE FORMAT (JSON only, no markdown):
{{
  ""file_to_fix"": ""test"" or ""implementation"",
  ""reason"": ""Brief explanation why this file needs fixing"",
  ""suggested_fix"": ""What specific change should be made""
}}";

        try
        {
            var (response, tokens) = await _coderAgent.CallLLMDirectAsync(
                "You are a code analyst. Analyze test failures and determine which file (test or implementation) needs to be fixed. Always respond with valid JSON only.",
                prompt,
                temperature: 0.1f,
                maxTokens: 500,
                ct);

            _logger.LogInformation("[ErrorAnalysis] LLM response: {Response}", response);

            // Parse JSON response
            var jsonMatch = System.Text.RegularExpressions.Regex.Match(response, @"\{[\s\S]*\}");
            if (jsonMatch.Success)
            {
                var json = System.Text.Json.JsonDocument.Parse(jsonMatch.Value);
                var root = json.RootElement;

                return new TestFailureAnalysis
                {
                    FileToFix = root.GetProperty("file_to_fix").GetString() ?? "implementation",
                    Reason = root.GetProperty("reason").GetString() ?? "",
                    SuggestedFix = root.GetProperty("suggested_fix").GetString() ?? "",
                    TokensUsed = tokens
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[ErrorAnalysis] Failed to analyze test failure, defaulting to implementation");
        }

        // Default to implementation if analysis fails
        return new TestFailureAnalysis
        {
            FileToFix = "implementation",
            Reason = "Analysis failed, defaulting to implementation fix",
            SuggestedFix = "Review the implementation code"
        };
    }

    /// <summary>
    /// Result of LLM-based test failure analysis
    /// </summary>
    private class TestFailureAnalysis
    {
        public string FileToFix { get; set; } = "implementation";
        public string Reason { get; set; } = "";
        public string SuggestedFix { get; set; } = "";
        public int TokensUsed { get; set; }
    }

    /// <summary>
    /// Generate fix tasks from test failures with LLM analysis
    /// Determines whether to fix test or implementation based on error analysis
    /// </summary>
    private async Task<List<FixTaskDto>> GenerateFixTasksFromTestFailuresAsync(
        TestSummaryDto testSummary,
        Dictionary<string, string>? generatedFiles = null,
        Core.Models.CodebaseAnalysis? codebaseAnalysis = null,
        DeploymentResult? deploymentResult = null,
        CancellationToken ct = default)
    {
        var fixTasks = new List<FixTaskDto>();
        int index = 1;

        // Group failed tests by the implementation they're testing
        var failedTestGroups = testSummary.FailedTests
            .GroupBy(t => ExtractImplementationClassName(t.TestName, t.ClassName))
            .ToList();

        foreach (var group in failedTestGroups)
        {
            var implementationClass = group.Key;
            var failedTests = group.ToList();

            // Find the implementation file from generated files
            string implementationFile = "";
            string implementationCode = "";
            string testFile = "";
            string testCode = "";
            string implProjectName = "";
            string testProjectName = "";
            string implNamespace = "";
            string testNamespace = "";
            string implFullPath = "";
            string testFullPath = "";

            if (generatedFiles != null)
            {
                // Find implementation file (e.g., DateTimeHelper.cs)
                var implFileEntry = generatedFiles.FirstOrDefault(f =>
                    f.Key.Contains($"{implementationClass}.cs", StringComparison.OrdinalIgnoreCase) &&
                    !f.Key.Contains("Test", StringComparison.OrdinalIgnoreCase));

                if (!string.IsNullOrEmpty(implFileEntry.Key))
                {
                    implementationFile = implFileEntry.Key;
                    implementationCode = implFileEntry.Value;
                    var parts = implFileEntry.Key.Split('/');
                    if (parts.Length > 0)
                    {
                        implProjectName = parts[0];
                    }
                    var nsMatch = System.Text.RegularExpressions.Regex.Match(implementationCode, @"namespace\s+([\w.]+)");
                    if (nsMatch.Success)
                    {
                        implNamespace = nsMatch.Groups[1].Value;
                    }

                    if (deploymentResult?.CopiedFiles != null)
                    {
                        var deployedFile = deploymentResult.CopiedFiles.FirstOrDefault(df =>
                            df.TargetPath.EndsWith($"{implementationClass}.cs", StringComparison.OrdinalIgnoreCase) &&
                            !df.TargetPath.Contains("Test", StringComparison.OrdinalIgnoreCase));
                        if (deployedFile != null)
                        {
                            implFullPath = deployedFile.TargetPath;
                        }
                    }
                }

                // Find test file
                var testFileEntry = generatedFiles.FirstOrDefault(f =>
                    f.Key.Contains($"{implementationClass}Tests.cs", StringComparison.OrdinalIgnoreCase) ||
                    f.Key.Contains($"{implementationClass}Test.cs", StringComparison.OrdinalIgnoreCase));

                if (!string.IsNullOrEmpty(testFileEntry.Key))
                {
                    testFile = testFileEntry.Key;
                    testCode = testFileEntry.Value;
                    var parts = testFileEntry.Key.Split('/');
                    if (parts.Length > 0)
                    {
                        testProjectName = parts[0];
                    }
                    var nsMatch = System.Text.RegularExpressions.Regex.Match(testCode, @"namespace\s+([\w.]+)");
                    if (nsMatch.Success)
                    {
                        testNamespace = nsMatch.Groups[1].Value;
                    }

                    if (deploymentResult?.CopiedFiles != null)
                    {
                        var deployedFile = deploymentResult.CopiedFiles.FirstOrDefault(df =>
                            df.TargetPath.Contains($"{implementationClass}Test", StringComparison.OrdinalIgnoreCase));
                        if (deployedFile != null)
                        {
                            testFullPath = deployedFile.TargetPath;
                        }
                    }
                }
            }

            // Use LLM to analyze which file needs fixing
            var firstFailedTest = failedTests.First();
            TestFailureAnalysis analysis;

            if (!string.IsNullOrEmpty(testCode) && !string.IsNullOrEmpty(implementationCode))
            {
                _logger.LogInformation("[{Class}] Analyzing test failure with LLM to determine fix target...", implementationClass);

                analysis = await AnalyzeTestFailureWithLLMAsync(
                    firstFailedTest.TestName,
                    firstFailedTest.ErrorMessage ?? "Unknown error",
                    firstFailedTest.StackTrace,
                    testCode,
                    implementationCode,
                    testFile,
                    implementationFile,
                    ct);

                _logger.LogInformation("[{Class}] LLM Analysis: Fix '{FileToFix}' - {Reason}",
                    implementationClass, analysis.FileToFix, analysis.Reason);
            }
            else
            {
                // Default to implementation if we can't get both codes
                analysis = new TestFailureAnalysis
                {
                    FileToFix = "implementation",
                    Reason = "Could not load both test and implementation code",
                    SuggestedFix = "Review the implementation"
                };
            }

            // Determine target file based on analysis
            string targetFile, targetCode, targetProjectName, targetNamespace, targetFullPath;
            bool isTestFix = analysis.FileToFix.Equals("test", StringComparison.OrdinalIgnoreCase);

            if (isTestFix)
            {
                targetFile = testFile;
                targetCode = testCode;
                targetProjectName = testProjectName;
                targetNamespace = testNamespace;
                targetFullPath = testFullPath;
            }
            else
            {
                targetFile = implementationFile;
                targetCode = implementationCode;
                targetProjectName = implProjectName;
                targetNamespace = implNamespace;
                targetFullPath = implFullPath;
            }

            // Build detailed error description
            var errorDetails = new System.Text.StringBuilder();
            errorDetails.AppendLine($"The following test(s) failed for {implementationClass}:");
            errorDetails.AppendLine();

            foreach (var test in failedTests)
            {
                errorDetails.AppendLine($"❌ Test: {test.TestName}");
                errorDetails.AppendLine($"   Error: {test.ErrorMessage}");
                if (!string.IsNullOrEmpty(test.StackTrace))
                {
                    var relevantStack = test.StackTrace.Split('\n').Take(3);
                    errorDetails.AppendLine($"   Stack: {string.Join(" → ", relevantStack)}");
                }
                errorDetails.AppendLine();
            }

            // Build comprehensive fix task description
            var description = new System.Text.StringBuilder();
            description.AppendLine("## ⚠️ CRITICAL INSTRUCTIONS - READ CAREFULLY ⚠️");
            description.AppendLine();
            description.AppendLine("**DO NOT create a new file or new class!**");
            description.AppendLine("**You MUST modify the EXISTING code below to fix the test failure.**");
            description.AppendLine();

            // Add codebase context if available
            if (codebaseAnalysis != null)
            {
                description.AppendLine("---");
                description.AppendLine();
                description.AppendLine("## CODEBASE CONTEXT");
                description.AppendLine($"- **Codebase:** {codebaseAnalysis.CodebaseName}");
                description.AppendLine($"- **Target Framework:** {codebaseAnalysis.Projects.FirstOrDefault(p => p.Name == targetProjectName)?.TargetFramework ?? "Unknown"}");

                // Add coding conventions
                if (codebaseAnalysis.Conventions != null)
                {
                    description.AppendLine();
                    description.AppendLine("### Coding Conventions (MUST FOLLOW):");
                    if (!string.IsNullOrEmpty(codebaseAnalysis.Conventions.NamingStyle))
                        description.AppendLine($"- Naming Style: {codebaseAnalysis.Conventions.NamingStyle}");
                    if (!string.IsNullOrEmpty(codebaseAnalysis.Conventions.PrivateFieldPrefix))
                        description.AppendLine($"- Private Field Prefix: {codebaseAnalysis.Conventions.PrivateFieldPrefix}");
                    if (codebaseAnalysis.Conventions.UsesXmlDocs)
                        description.AppendLine("- Uses XML Documentation: Yes");
                    if (codebaseAnalysis.Conventions.UsesAsyncSuffix)
                        description.AppendLine("- Uses Async Suffix: Yes (e.g., GetDataAsync)");
                    if (!string.IsNullOrEmpty(codebaseAnalysis.Conventions.TestFramework))
                        description.AppendLine($"- Test Framework: {codebaseAnalysis.Conventions.TestFramework}");
                }

                // Add project info
                var projectInfo = codebaseAnalysis.Projects.FirstOrDefault(p =>
                    p.Name.Equals(targetProjectName, StringComparison.OrdinalIgnoreCase) ||
                    p.Name.Replace(".Dev", "").Equals(targetProjectName.Replace(".Dev", ""), StringComparison.OrdinalIgnoreCase));
                if (projectInfo != null)
                {
                    description.AppendLine();
                    description.AppendLine($"### Project: {projectInfo.Name}");
                    description.AppendLine($"- Root Namespace: {projectInfo.RootNamespace}");
                    if (projectInfo.Namespaces.Count > 0)
                        description.AppendLine($"- Available Namespaces: {string.Join(", ", projectInfo.Namespaces.Take(5))}");
                    if (projectInfo.DetectedPatterns.Count > 0)
                        description.AppendLine($"- Detected Patterns: {string.Join(", ", projectInfo.DetectedPatterns)}");
                }
            }

            description.AppendLine();
            description.AppendLine("---");
            description.AppendLine();

            // Add LLM analysis result
            description.AppendLine("## 🔍 ERROR ANALYSIS RESULT");
            description.AppendLine($"**File to fix:** `{(isTestFix ? "TEST" : "IMPLEMENTATION")}`");
            description.AppendLine($"**Reason:** {analysis.Reason}");
            description.AppendLine($"**Suggested fix:** {analysis.SuggestedFix}");
            description.AppendLine();
            description.AppendLine("---");
            description.AppendLine();
            description.AppendLine(errorDetails.ToString());

            if (isTestFix)
            {
                // TEST needs fixing - show test code as primary
                description.AppendLine("---");
                description.AppendLine();
                description.AppendLine($"## ⚠️ TEST CODE TO FIX (File: {testFile})");
                description.AppendLine("**This is the file you need to modify:**");
                if (!string.IsNullOrEmpty(testFullPath))
                    description.AppendLine($"**Full Path:** `{testFullPath}`");
                description.AppendLine();
                description.AppendLine("```csharp");
                description.AppendLine(testCode);
                description.AppendLine("```");
                description.AppendLine();

                if (!string.IsNullOrEmpty(implementationCode))
                {
                    description.AppendLine("---");
                    description.AppendLine();
                    description.AppendLine($"## IMPLEMENTATION CODE (File: {implementationFile}) - For reference only");
                    description.AppendLine("**The implementation is correct, fix the test instead:**");
                    description.AppendLine();
                    description.AppendLine("```csharp");
                    description.AppendLine(implementationCode);
                    description.AppendLine("```");
                }
            }
            else
            {
                // IMPLEMENTATION needs fixing - show impl code as primary
                if (!string.IsNullOrEmpty(implementationCode))
                {
                    description.AppendLine("---");
                    description.AppendLine();
                    description.AppendLine($"## ⚠️ IMPLEMENTATION CODE TO FIX (File: {implementationFile})");
                    description.AppendLine("**This is the file you need to modify:**");
                    if (!string.IsNullOrEmpty(implFullPath))
                        description.AppendLine($"**Full Path:** `{implFullPath}`");
                    description.AppendLine();
                    description.AppendLine("```csharp");
                    description.AppendLine(implementationCode);
                    description.AppendLine("```");
                    description.AppendLine();
                }

                if (!string.IsNullOrEmpty(testCode))
                {
                    description.AppendLine("---");
                    description.AppendLine();
                    description.AppendLine($"## TEST CODE (File: {testFile}) - For reference only");
                    description.AppendLine("**The test is correct, fix the implementation instead:**");
                    description.AppendLine();
                    description.AppendLine("```csharp");
                    description.AppendLine(testCode);
                    description.AppendLine("```");
                }
            }

            // Extract class name from target file
            var targetClassName = isTestFix ? $"{implementationClass}Tests" : implementationClass;

            description.AppendLine();
            description.AppendLine("---");
            description.AppendLine();
            description.AppendLine("## YOUR TASK:");
            description.AppendLine($"1. Keep the SAME namespace: `{targetNamespace}`");
            description.AppendLine($"2. Keep the SAME class name: `{targetClassName}`");
            description.AppendLine($"3. Keep the SAME file structure (do not rename or move the file)");
            description.AppendLine($"4. Fix the {(isTestFix ? "test assertion/expectation" : "implementation logic")} as suggested above");
            description.AppendLine("5. Return the COMPLETE fixed file content with ALL existing methods intact");
            description.AppendLine("6. Follow the codebase conventions listed above");

            var fixTitle = isTestFix
                ? $"Fix {implementationClass}Tests - {failedTests.Count} test(s) have incorrect assertions"
                : $"Fix {implementationClass} - {failedTests.Count} test(s) failing";

            var suggestedFixText = isTestFix
                ? $"Modify the test file {testFile} to fix incorrect assertions. {analysis.SuggestedFix}"
                : $"Modify the implementation {implementationFile} to make the tests pass. {analysis.SuggestedFix}";

            fixTasks.Add(new FixTaskDto
            {
                Index = index++,
                Title = fixTitle,
                Description = description.ToString(),
                TargetFile = targetFile,
                Type = FixTaskType.TestFailure,
                ErrorMessage = string.Join("; ", failedTests.Select(t => t.ErrorMessage)),
                StackTrace = failedTests.FirstOrDefault()?.StackTrace,
                SuggestedFix = suggestedFixText,
                ProjectName = targetProjectName,
                Namespace = targetNamespace,
                FullPath = targetFullPath,  // For ExecuteModificationCodingAsync to use
                // Preserve existing code from generatedFiles BEFORE rollback - crucial for LLM
                ExistingCode = targetCode
            });
        }

        return fixTasks;
    }

    /// <summary>
    /// Extract implementation class name from test method or class name
    /// </summary>
    private static string ExtractImplementationClassName(string testMethodName, string? testClassName)
    {
        // Try to extract from class name first (e.g., "DateTimeHelperTests" -> "DateTimeHelper")
        if (!string.IsNullOrEmpty(testClassName))
        {
            var className = testClassName.Split('.').Last();
            if (className.EndsWith("Tests"))
                return className[..^5];
            if (className.EndsWith("Test"))
                return className[..^4];
        }

        // Try to extract from method name pattern (e.g., "GetEndOfDay_MaxValue_..." -> related to a class)
        var methodParts = testMethodName.Split('_');
        if (methodParts.Length > 0)
        {
            return methodParts[0];
        }

        return "Unknown";
    }

    /// <summary>
    /// Handle retry flow - rollback and return to Coding phase
    /// </summary>
    private async Task HandleRetryAsync(
        PipelineExecution execution,
        List<FixTaskDto> fixTasks,
        CancellationToken ct)
    {
        var storyId = execution.StoryId;
        execution.RetryAttempt++;
        execution.PendingFixTasks = fixTasks;

        _logger.LogInformation("[{StoryId}] Starting retry attempt {Attempt}/{Max}",
            storyId, execution.RetryAttempt, PipelineExecution.MaxRetryAttempts);

        await _notificationService.NotifyRetryStartingAsync(
            storyId, execution.RetryAttempt, PipelineExecution.MaxRetryAttempts, PipelinePhase.Coding);

        // Convert fix tasks to regular tasks BEFORE rollback 
        // so we preserve the existing code content for LLM
        var tasks = fixTasks.Select((ft, i) => new TaskDto
        {
            Index = i + 1, // Will be reassigned by AppendFixTasksAsync
            Title = ft.Title,
            // Description already contains detailed instructions from GenerateFixTasksFromTestFailures
            Description = ft.Description,
            TargetFiles = new List<string> { ft.TargetFile },
            Status = Models.TaskStatus.Pending,
            // Use the actual project name from the fix task, not "fix"
            ProjectName = !string.IsNullOrEmpty(ft.ProjectName) ? ft.ProjectName : "default",
            // Include namespace for proper code generation
            Namespace = ft.Namespace,
            // Mark as modification so CoderAgent knows to modify existing code
            IsModification = true,
            // Full path for ExecuteModificationCodingAsync to read current content
            FullPath = ft.FullPath,
            // Preserve existing code from generated files for LLM - this is crucial
            // because the file will be deleted during rollback
            ExistingCode = ft.ExistingCode,
            // Mark as fix task
            Type = TaskType.Fix,
            RetryAttempt = execution.RetryAttempt
        }).ToList();

        // Rollback deployment if needed
        if (execution.LastDeploymentResult != null && execution.LastDeploymentResult.TotalFilesCopied > 0)
        {
            await _notificationService.NotifyProgressAsync(storyId, "Rolling back deployment for retry...");
            await _deploymentAgent.RollbackAsync(execution.LastDeploymentResult, ct);
        }

        // Append fix tasks to existing tasks (preserves original tasks)
        await _taskRepository.AppendFixTasksAsync(storyId, tasks, execution.RetryAttempt, ct);

        // Reset phases after Coding to Pending
        foreach (var phase in execution.Status.Phases)
        {
            if (phase.Phase > PipelinePhase.Planning)
            {
                phase.State = PhaseState.Pending;
                phase.Result = null;
                phase.Message = null;
            }
        }

        // Clear retry info after handling
        execution.CurrentRetryInfo = null;
        execution.Status.RetryInfo = null;
        execution.Status.RetryTargetPhase = null;

        await _notificationService.NotifyProgressAsync(storyId,
            $"Retry prepared with {fixTasks.Count} fix task(s). Returning to Coding phase...");
    }

    /// <summary>
    /// Execute retry from Coding phase - used after unit test failures
    /// </summary>
    private async Task ExecuteRetryFromCodingAsync(
        PipelineExecution execution,
        Core.Models.CodebaseAnalysis? codebaseAnalysis,
        StoryDto? story,
        CancellationToken ct)
    {
        var storyId = execution.StoryId;
        var content = await _storyRepository.GetContentAsync(storyId, ct);

        _logger.LogInformation("[{StoryId}] Executing retry from Coding phase (Attempt {Attempt})",
            storyId, execution.RetryAttempt);

        await _notificationService.NotifyProgressAsync(storyId,
            $"🔄 RETRY {execution.RetryAttempt}/{PipelineExecution.MaxRetryAttempts} - Starting Coding phase with fix tasks...");

        // Phase: Coding (with fix tasks)
        var codingResult = await ExecutePhaseAsync(execution, PipelinePhase.Coding, async () =>
        {
            await _notificationService.NotifyPhaseStartedAsync(storyId, PipelinePhase.Coding,
                $"Retry #{execution.RetryAttempt} - Fixing code based on test failures...");

            var allTasks = (await _taskRepository.GetByStoryAsync(storyId, ct)).ToList();

            // CRITICAL: Only process FIX tasks that are PENDING for this retry attempt
            // Do NOT re-process original tasks or already completed fix tasks
            var fixTasks = allTasks
                .Where(t => t.Type == TaskType.Fix &&
                           t.Status == Models.TaskStatus.Pending &&
                           t.RetryAttempt == execution.RetryAttempt)
                .ToList();

            var projectState = new ProjectState { Story = content ?? "" };
            var generatedFiles = new ConcurrentDictionary<string, string>();

            // CRITICAL: Load previously generated files from original coding phase
            // These files were created before the failed test, we need them for deployment
            if (execution.GeneratedFiles != null && execution.GeneratedFiles.Count > 0)
            {
                foreach (var kvp in execution.GeneratedFiles)
                {
                    generatedFiles[kvp.Key] = kvp.Value;
                    projectState.Codebase[kvp.Key] = kvp.Value;
                }
                _logger.LogInformation("[{StoryId}] Loaded {Count} previously generated files for retry",
                    storyId, execution.GeneratedFiles.Count);
            }

            // Load previously generated files into project state (from completed original tasks)
            var completedTasks = allTasks.Where(t => t.Status == Models.TaskStatus.Completed).ToList();
            _logger.LogInformation("[{StoryId}] Retry: {CompletedCount} completed tasks, {FixCount} pending fix tasks to process",
                storyId, completedTasks.Count, fixTasks.Count);

            await _notificationService.NotifyProgressAsync(storyId,
                $"Processing {fixTasks.Count} fix task(s) with {execution.GeneratedFiles?.Count ?? 0} existing files...");

            foreach (var task in fixTasks)
            {
                ct.ThrowIfCancellationRequested();

                await _notificationService.NotifyProgressAsync(storyId, $"Fixing: {task.Title}");

                // Use modification-aware coding if codebase is available
                AgentResponse response;
                if (codebaseAnalysis != null && !string.IsNullOrEmpty(story?.CodebaseId))
                {
                    response = await ExecuteModificationCodingAsync(task, projectState, story.CodebaseId, codebaseAnalysis, ct);
                }
                else
                {
                    response = await ExecuteGenerationCodingAsync(task, projectState, ct);
                }

                if (response.Success && response.Data.TryGetValue("filename", out var filename) &&
                    response.Data.TryGetValue("code", out var code))
                {
                    var projectName = task.ProjectName ?? "fix";
                    var filenameStr = filename.ToString()!;

                    // Avoid duplicate project name in path - if filename already includes project name, use it directly
                    string fileKey;
                    if (filenameStr.StartsWith(projectName + "/", StringComparison.OrdinalIgnoreCase) ||
                        filenameStr.StartsWith(projectName + "\\", StringComparison.OrdinalIgnoreCase))
                    {
                        fileKey = filenameStr.Replace("\\", "/");
                    }
                    else
                    {
                        fileKey = $"{projectName}/{filenameStr}";
                    }

                    generatedFiles[fileKey] = code.ToString()!;
                    _logger.LogInformation("[{StoryId}] Fix task added file: {FileKey}", storyId, fileKey);
                }

                task.Status = Models.TaskStatus.Completed;
                await _taskRepository.UpdateTaskAsync(storyId, task, ct);
            }

            // Update project state
            foreach (var kvp in generatedFiles)
            {
                projectState.Codebase[kvp.Key] = kvp.Value;
            }

            // CRITICAL: Update execution.GeneratedFiles with ALL files (original + fixed)
            // This ensures next phases (deployment, testing) have complete file set
            execution.GeneratedFiles = generatedFiles.ToDictionary(k => k.Key, v => v.Value);
            _logger.LogInformation("[{StoryId}] Updated GeneratedFiles with {Count} total files after fix",
                storyId, execution.GeneratedFiles.Count);

            // Save output
            var outputPath = await _outputRepository.SaveOutputAsync(
                storyId,
                story?.Name ?? storyId,
                generatedFiles.ToDictionary(k => k.Key, v => v.Value),
                ct);

            return new
            {
                Files = generatedFiles.ToDictionary(k => k.Key, v => v.Value),
                OutputPath = outputPath,
                RetryAttempt = execution.RetryAttempt,
                OriginalFiles = execution.GeneratedFiles?.Count ?? 0,
                FixedFiles = fixTasks.Count
            };
        }, ct);

        if (!codingResult.Approved)
        {
            await _storyRepository.UpdateStatusAsync(storyId, StoryStatus.Failed, ct);
            return;
        }

        // Phase: Debugging - Debug ALL generated files together (original + fixed)
        var debuggingResult = await ExecutePhaseAsync(execution, PipelinePhase.Debugging, async () =>
        {
            await _notificationService.NotifyPhaseStartedAsync(storyId, PipelinePhase.Debugging, "Checking for compilation errors...");

            var projectState = new ProjectState { Story = content ?? "" };

            // Get ALL generated files (original + fixed)
            var outputFiles = await _outputRepository.GetGeneratedFilesAsync(storyId, ct);
            _logger.LogInformation("[{StoryId}] Debugging {Count} files (original + fixed)", storyId, outputFiles.Count);

            foreach (var kvp in outputFiles)
            {
                projectState.Codebase[kvp.Key] = kvp.Value;
            }

            // Debug each file with full codebase context
            var debugResults = new Dictionary<string, bool>();
            foreach (var file in outputFiles)
            {
                // Skip test files for debugging (they don't need to compile standalone)
                if (file.Key.Contains("Test", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation("[{StoryId}] Skipping test file for debug: {File}", storyId, file.Key);
                    debugResults[file.Key] = true;
                    continue;
                }

                _logger.LogInformation("[{StoryId}] Debugging file: {File}", storyId, file.Key);
                var response = await _debuggerAgent.RunAsync(
                    new AgentRequest { Input = file.Value, ProjectState = projectState },
                    ct);

                debugResults[file.Key] = response.Success;
                if (!response.Success)
                {
                    await _notificationService.NotifyProgressAsync(storyId, $"Debug issues found for {Path.GetFileName(file.Key)}, fixing...");
                }
            }

            return new
            {
                Status = "Debugging completed",
                FileCount = outputFiles.Count,
                Results = debugResults
            };
        }, ct);

        if (!debuggingResult.Approved)
        {
            await _storyRepository.UpdateStatusAsync(storyId, StoryStatus.Failed, ct);
            return;
        }

        // Phase: Reviewing
        var reviewResult = await ExecutePhaseAsync(execution, PipelinePhase.Reviewing, async () =>
        {
            await _notificationService.NotifyPhaseStartedAsync(storyId, PipelinePhase.Reviewing, "Reviewing generated code...");

            var outputFiles = await _outputRepository.GetGeneratedFilesAsync(storyId, ct);
            var reviewSummary = new Dictionary<string, object>();

            foreach (var file in outputFiles)
            {
                var projectState = new ProjectState { Story = content ?? "" };
                projectState.Codebase[file.Key] = file.Value;

                var response = await _reviewerAgent.RunAsync(
                    new AgentRequest { Input = file.Value, ProjectState = projectState },
                    ct);

                if (response.Success && response.Data != null)
                {
                    reviewSummary[file.Key] = response.Data;
                }
            }

            return reviewSummary;
        }, ct);

        if (!reviewResult.Approved)
        {
            await _storyRepository.UpdateStatusAsync(storyId, StoryStatus.Failed, ct);
            return;
        }

        // Phase: Deployment
        DeploymentResult? lastDeploymentResult = null;
        var deploymentResult = await ExecutePhaseAsync(execution, PipelinePhase.Deployment, async () =>
        {
            await _notificationService.NotifyPhaseStartedAsync(storyId, PipelinePhase.Deployment, "Deploying retry code to codebase...");

            var outputFiles = await _outputRepository.GetGeneratedFilesAsync(storyId, ct);
            if (codebaseAnalysis == null)
            {
                throw new InvalidOperationException("Codebase analysis is required for deployment");
            }

            lastDeploymentResult = await _deploymentAgent.DeployAsync(
                codebaseAnalysis,
                outputFiles.ToDictionary(k => k.Key, v => v.Value),
                ct);

            execution.LastDeploymentResult = lastDeploymentResult;

            return new
            {
                lastDeploymentResult.Success,
                lastDeploymentResult.TotalFilesCopied,
                lastDeploymentResult.NewFilesCreated,
                lastDeploymentResult.FilesModified,
                ProjectsUpdated = lastDeploymentResult.UpdatedProjects.Count,
                BuildResults = lastDeploymentResult.BuildResults.Select(b => new { b.Success, b.TargetPath, b.Error }).ToList()
            };
        }, ct);

        if (!deploymentResult.Approved)
        {
            if (lastDeploymentResult != null && lastDeploymentResult.TotalFilesCopied > 0)
            {
                await _deploymentAgent.RollbackAsync(lastDeploymentResult, ct);
            }
            await _storyRepository.UpdateStatusAsync(storyId, StoryStatus.Failed, ct);
            return;
        }

        // Phase: Unit Testing (again)
        var unitTestResult = await ExecuteUnitTestingPhaseAsync(execution, codebaseAnalysis!, lastDeploymentResult!, ct);

        if (!unitTestResult.Approved)
        {
            // Check if we can retry again
            if (unitTestResult.NeedsRetry && execution.RetryAttempt < PipelineExecution.MaxRetryAttempts)
            {
                await HandleRetryAsync(execution, unitTestResult.FixTasks!, ct);

                // Reset phases and retry again
                foreach (var phase in execution.Status.Phases)
                {
                    if (phase.Phase >= PipelinePhase.Coding && phase.Phase != PipelinePhase.Analysis && phase.Phase != PipelinePhase.Planning)
                    {
                        phase.State = PhaseState.Pending;
                        phase.Result = null;
                        phase.Message = null;
                        phase.StartedAt = null;
                        phase.CompletedAt = null;
                    }
                }

                // Recursive retry
                await ExecuteRetryFromCodingAsync(execution, codebaseAnalysis, story, ct);
                return;
            }

            // Max retries reached or user aborted
            if (lastDeploymentResult != null && lastDeploymentResult.TotalFilesCopied > 0)
            {
                await _deploymentAgent.RollbackAsync(lastDeploymentResult, ct);
            }
            await _storyRepository.UpdateStatusAsync(storyId, StoryStatus.Failed, ct);
            return;
        }

        // Phase: Pull Request (optional)
        var prPhaseStatus = execution.Status.Phases.First(p => p.Phase == PipelinePhase.PullRequest);
        execution.CurrentPhase = PipelinePhase.PullRequest;
        execution.Status.CurrentPhase = PipelinePhase.PullRequest;
        prPhaseStatus.State = PhaseState.WaitingApproval;
        prPhaseStatus.StartedAt = DateTime.UtcNow;

        var prInfo = new
        {
            BranchName = $"feature/ai-{story?.Name?.ToLowerInvariant().Replace(" ", "-") ?? storyId}",
            FilesDeployed = lastDeploymentResult?.TotalFilesCopied ?? 0,
            NewFiles = lastDeploymentResult?.NewFilesCreated ?? 0,
            ModifiedFiles = lastDeploymentResult?.FilesModified ?? 0,
            RetryAttempts = execution.RetryAttempt,
            Message = "Tests passed after retry! Ready to create PR or complete."
        };

        prPhaseStatus.Result = prInfo;
        prPhaseStatus.Message = $"Retry #{execution.RetryAttempt} successful! {prInfo.FilesDeployed} files deployed. Create PR?";

        await _notificationService.NotifyPhaseCompletedAsync(storyId, PipelinePhase.PullRequest,
            prPhaseStatus.Message, prInfo);

        // Wait for user decision
        if (!execution.AutoApproveAll)
        {
            execution.PhaseApprovalTcs = new TaskCompletionSource<bool>();
            var createPr = await execution.PhaseApprovalTcs.Task;

            if (createPr)
            {
                // TODO: Create GitHub PR
                prPhaseStatus.Message = "PR creation not yet implemented - deployment complete!";
            }
            else
            {
                prPhaseStatus.Message = "Completed without PR";
            }
        }

        prPhaseStatus.State = PhaseState.Completed;
        prPhaseStatus.CompletedAt = DateTime.UtcNow;

        // Mark as completed
        execution.CurrentPhase = PipelinePhase.Completed;
        execution.Status.CurrentPhase = PipelinePhase.Completed;
        execution.Status.IsRunning = false;
        execution.Status.CompletedAt = DateTime.UtcNow;

        // Store completed status for later retrieval (in-memory)
        _completedPipelines[storyId] = CloneStatus(execution.Status);

        // Save pipeline history to disk for permanent access
        await _outputRepository.SavePipelineHistoryAsync(storyId, execution.Status, ct);

        await _storyRepository.UpdateStatusAsync(storyId, StoryStatus.Completed, ct);

        var outputPath = await _outputRepository.GetOutputPathAsync(storyId, ct);
        await _notificationService.NotifyPipelineCompletedAsync(storyId, outputPath ?? "");
        await _notificationService.NotifyStoryListChangedAsync();

        _logger.LogInformation("[{StoryId}] Pipeline completed successfully after {RetryAttempts} retry(s)!",
            storyId, execution.RetryAttempt);
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // Knowledge Base Integration (V-Bounce Step 6: Knowledge Capture)
    // ═══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Captures knowledge from a successfully completed pipeline.
    /// This implements V-Bounce model's "Knowledge Capture" step.
    /// </summary>
    private async Task CaptureKnowledgeFromPipelineAsync(
        string storyId,
        PipelineExecution execution,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("[{StoryId}] Capturing knowledge from completed pipeline", storyId);

        // Get all tasks for this story
        var tasks = await _taskRepository.GetByStoryAsync(storyId, cancellationToken);

        // Get generated files
        var generatedFiles = await _outputRepository.GetGeneratedFilesAsync(storyId, cancellationToken);

        // Capture patterns from generated code
        if (generatedFiles != null && generatedFiles.Any())
        {
            await _knowledgeService.CaptureFromCompletedPipelineAsync(
                storyId, generatedFiles, cancellationToken);
        }

        // Capture individual task implementations as patterns
        foreach (var task in tasks.Where(t => t.Status == Models.TaskStatus.Completed))
        {
            var taskFile = task.TargetFiles.FirstOrDefault();
            if (!string.IsNullOrEmpty(taskFile) && generatedFiles != null)
            {
                var fileContent = generatedFiles
                    .FirstOrDefault(kv => kv.Key.EndsWith(taskFile, StringComparison.OrdinalIgnoreCase))
                    .Value;

                if (!string.IsNullOrEmpty(fileContent))
                {
                    await _knowledgeService.CapturePatternFromStoryAsync(
                        storyId,
                        task.Title,
                        task.Description,
                        fileContent,
                        cancellationToken: cancellationToken);
                }
            }
        }

        // Mark all knowledge from this story as verified (since pipeline completed successfully)
        await _knowledgeService.VerifyStoryKnowledgeAsync(storyId, cancellationToken);

        _logger.LogInformation("[{StoryId}] Knowledge capture completed", storyId);
    }

    /// <summary>
    /// Creates a deep clone of PipelineStatusDto to preserve state after pipeline completion
    /// </summary>
    private static PipelineStatusDto CloneStatus(PipelineStatusDto original)
    {
        return new PipelineStatusDto
        {
            StoryId = original.StoryId,
            CurrentPhase = original.CurrentPhase,
            IsRunning = original.IsRunning,
            StartedAt = original.StartedAt,
            CompletedAt = original.CompletedAt,
            Phases = original.Phases.Select(p => new PhaseStatusDto
            {
                Phase = p.Phase,
                State = p.State,
                Message = p.Message,
                StartedAt = p.StartedAt,
                CompletedAt = p.CompletedAt,
                Result = p.Result
            }).ToList()
        };
    }
}
