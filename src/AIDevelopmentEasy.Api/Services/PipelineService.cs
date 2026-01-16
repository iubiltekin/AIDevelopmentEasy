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
/// 1. Planning - PlannerAgent decomposes requirements into tasks (with codebase context)
/// 2. Coding - CoderAgent generates code for each task
/// 3. Debugging - DebuggerAgent tests and fixes code
/// 4. Testing - Test analysis and execution
/// 5. Reviewing - ReviewerAgent validates final output
/// 
/// When a codebase is associated with the requirement, CodeAnalysisAgent provides
/// context about existing projects, patterns, and conventions to the PlannerAgent.
/// </summary>
public class PipelineService : IPipelineService
{
    private readonly IRequirementRepository _requirementRepository;
    private readonly ITaskRepository _taskRepository;
    private readonly IApprovalRepository _approvalRepository;
    private readonly IOutputRepository _outputRepository;
    private readonly ICodebaseRepository _codebaseRepository;
    private readonly IPipelineNotificationService _notificationService;
    private readonly PlannerAgent _plannerAgent;
    private readonly CoderAgent _coderAgent;
    private readonly DebuggerAgent _debuggerAgent;
    private readonly ReviewerAgent _reviewerAgent;
    private readonly CodeAnalysisAgent _codeAnalysisAgent;
    private readonly DeploymentAgent _deploymentAgent;
    private readonly ILogger<PipelineService> _logger;

    // Track running pipelines and their state
    private static readonly ConcurrentDictionary<string, PipelineExecution> _runningPipelines = new();

    public PipelineService(
        IRequirementRepository requirementRepository,
        ITaskRepository taskRepository,
        IApprovalRepository approvalRepository,
        IOutputRepository outputRepository,
        ICodebaseRepository codebaseRepository,
        IPipelineNotificationService notificationService,
        PlannerAgent plannerAgent,
        CoderAgent coderAgent,
        DebuggerAgent debuggerAgent,
        ReviewerAgent reviewerAgent,
        CodeAnalysisAgent codeAnalysisAgent,
        DeploymentAgent deploymentAgent,
        ILogger<PipelineService> logger)
    {
        _requirementRepository = requirementRepository;
        _taskRepository = taskRepository;
        _approvalRepository = approvalRepository;
        _outputRepository = outputRepository;
        _codebaseRepository = codebaseRepository;
        _notificationService = notificationService;
        _plannerAgent = plannerAgent;
        _coderAgent = coderAgent;
        _debuggerAgent = debuggerAgent;
        _reviewerAgent = reviewerAgent;
        _codeAnalysisAgent = codeAnalysisAgent;
        _deploymentAgent = deploymentAgent;
        _logger = logger;
    }

    public async Task<PipelineStatusDto> StartAsync(string requirementId, bool autoApproveAll = false, CancellationToken cancellationToken = default)
    {
        // Check if already running
        if (_runningPipelines.ContainsKey(requirementId))
        {
            throw new InvalidOperationException($"Pipeline already running for requirement: {requirementId}");
        }

        // Get the requirement
        var requirement = await _requirementRepository.GetByIdAsync(requirementId, cancellationToken);
        if (requirement == null)
        {
            throw new ArgumentException($"Requirement not found: {requirementId}");
        }

        // Check if already completed
        if (requirement.Status == RequirementStatus.Completed)
        {
            throw new InvalidOperationException($"Requirement already completed: {requirementId}");
        }

        // Create pipeline execution context
        var execution = new PipelineExecution
        {
            RequirementId = requirementId,
            AutoApproveAll = autoApproveAll,
            Status = CreateInitialStatus(requirementId),
            CancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
        };

        _runningPipelines[requirementId] = execution;

        // Start pipeline execution in background
        _ = Task.Run(() => ExecutePipelineAsync(execution), execution.CancellationTokenSource.Token);

        return execution.Status;
    }

    public Task<PipelineStatusDto?> GetStatusAsync(string requirementId, CancellationToken cancellationToken = default)
    {
        if (_runningPipelines.TryGetValue(requirementId, out var execution))
        {
            return Task.FromResult<PipelineStatusDto?>(execution.Status);
        }

        // Return a "not running" status
        return Task.FromResult<PipelineStatusDto?>(new PipelineStatusDto
        {
            RequirementId = requirementId,
            CurrentPhase = PipelinePhase.None,
            IsRunning = false
        });
    }

    public Task<bool> ApprovePhaseAsync(string requirementId, PipelinePhase phase, CancellationToken cancellationToken = default)
    {
        if (!_runningPipelines.TryGetValue(requirementId, out var execution))
        {
            return Task.FromResult(false);
        }

        if (execution.CurrentPhase != phase || execution.PhaseApprovalTcs == null)
        {
            _logger.LogWarning("Cannot approve phase {Phase} - current phase is {CurrentPhase}", phase, execution.CurrentPhase);
            return Task.FromResult(false);
        }

        execution.PhaseApprovalTcs.TrySetResult(true);
        _logger.LogInformation("[{RequirementId}] Phase {Phase} approved", requirementId, phase);
        return Task.FromResult(true);
    }

    public Task<bool> RejectPhaseAsync(string requirementId, PipelinePhase phase, string? reason = null, CancellationToken cancellationToken = default)
    {
        if (!_runningPipelines.TryGetValue(requirementId, out var execution))
        {
            return Task.FromResult(false);
        }

        if (execution.CurrentPhase != phase || execution.PhaseApprovalTcs == null)
        {
            return Task.FromResult(false);
        }

        execution.RejectionReason = reason;
        execution.PhaseApprovalTcs.TrySetResult(false);
        _logger.LogInformation("[{RequirementId}] Phase {Phase} rejected. Reason: {Reason}", requirementId, phase, reason);
        return Task.FromResult(true);
    }

    public Task CancelAsync(string requirementId, CancellationToken cancellationToken = default)
    {
        if (_runningPipelines.TryGetValue(requirementId, out var execution))
        {
            execution.CancellationTokenSource.Cancel();
            _runningPipelines.TryRemove(requirementId, out _);
            _logger.LogInformation("[{RequirementId}] Pipeline cancelled", requirementId);
        }

        return Task.CompletedTask;
    }

    public Task<bool> IsRunningAsync(string requirementId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_runningPipelines.ContainsKey(requirementId));
    }

    public Task<IEnumerable<string>> GetRunningPipelinesAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IEnumerable<string>>(_runningPipelines.Keys.ToList());
    }

    private async Task ExecutePipelineAsync(PipelineExecution execution)
    {
        var requirementId = execution.RequirementId;
        var ct = execution.CancellationTokenSource.Token;

        try
        {
            execution.Status.IsRunning = true;
            execution.Status.StartedAt = DateTime.UtcNow;

            // Update requirement status to InProgress
            await _requirementRepository.UpdateStatusAsync(requirementId, RequirementStatus.InProgress, ct);
            await _notificationService.NotifyRequirementListChangedAsync();

            // Get requirement content
            var content = await _requirementRepository.GetContentAsync(requirementId, ct);
            if (string.IsNullOrEmpty(content))
            {
                throw new InvalidOperationException("Requirement content is empty");
            }

            var requirement = await _requirementRepository.GetByIdAsync(requirementId, ct);
            if (requirement == null)
            {
                throw new InvalidOperationException($"Requirement not found: {requirementId}");
            }

            // Phase 1: Analysis (optional - only when codebase is linked)
            Core.Models.CodebaseAnalysis? codebaseAnalysis = null;
            ClassSearchResult? classResult = null;
            ReferenceSearchResult? referenceResult = null;
            
            if (!string.IsNullOrEmpty(requirement.CodebaseId))
            {
                var analysisResult = await ExecutePhaseAsync(execution, PipelinePhase.Analysis, async () =>
                {
                    await _notificationService.NotifyPhaseStartedAsync(requirementId, PipelinePhase.Analysis, 
                        "CodeAnalysisAgent analyzing codebase...");

                    await _notificationService.NotifyProgressAsync(requirementId, $"Loading codebase: {requirement.CodebaseId}");
                    codebaseAnalysis = await _codebaseRepository.GetAnalysisAsync(requirement.CodebaseId, ct);
                    
                    if (codebaseAnalysis == null)
                    {
                        throw new InvalidOperationException($"Codebase analysis not found: {requirement.CodebaseId}");
                    }

                    var summary = codebaseAnalysis.Summary;
                    await _notificationService.NotifyProgressAsync(requirementId, 
                        $"Codebase loaded: {summary.TotalProjects} projects, {summary.TotalClasses} classes");

                    // Try to extract target class from requirement
                    var targetClassName = ExtractTargetClassName(content);
                    
                    if (!string.IsNullOrEmpty(targetClassName))
                    {
                        await _notificationService.NotifyProgressAsync(requirementId, 
                            $"Detected target class: {targetClassName}, finding references...");
                        
                        classResult = await _codeAnalysisAgent.FindClassAsync(codebaseAnalysis, targetClassName, includeContent: true);
                        
                        if (classResult.Found)
                        {
                            await _notificationService.NotifyProgressAsync(requirementId, 
                                $"Found class at: {classResult.FilePath}");
                            
                            referenceResult = await _codeAnalysisAgent.FindReferencesAsync(codebaseAnalysis, targetClassName, ct);
                            
                            await _notificationService.NotifyProgressAsync(requirementId, 
                                $"Found {referenceResult.References.Count} references in {referenceResult.AffectedFiles.Count} files");
                        }
                        else
                        {
                            await _notificationService.NotifyProgressAsync(requirementId, 
                                $"Class '{targetClassName}' not found, will create new code");
                        }
                    }

                    return new
                    {
                        CodebaseName = codebaseAnalysis.CodebaseName,
                        CodebasePath = codebaseAnalysis.CodebasePath,
                        TotalProjects = summary.TotalProjects,
                        TotalClasses = summary.TotalClasses,
                        TotalInterfaces = summary.TotalInterfaces,
                        Patterns = summary.DetectedPatterns,
                        TargetClass = targetClassName,
                        ClassFound = classResult?.Found ?? false,
                        ClassFilePath = classResult?.FullPath,
                        ReferenceCount = referenceResult?.References.Count ?? 0,
                        AffectedFiles = referenceResult?.AffectedFiles ?? new List<string>()
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
                await _notificationService.NotifyPhaseStartedAsync(requirementId, PipelinePhase.Planning, 
                    "PlannerAgent decomposing requirements into tasks...");

                var projectState = new ProjectState { Requirement = content };
                AgentResponse response;
                
                if (codebaseAnalysis != null)
                {
                    _logger.LogInformation("[Planning] Using codebase-aware planning for: {CodebaseId}", requirement?.CodebaseId);
                    
                    if (classResult?.Found == true && referenceResult != null)
                    {
                        // Modification planning with class/reference context
                        await _notificationService.NotifyProgressAsync(requirementId, 
                            "Creating modification plan for existing class and references...");
                        
                        var modificationTasks = await _codeAnalysisAgent.CreateModificationTasksAsync(
                            codebaseAnalysis, referenceResult, content, ct);
                        
                        response = await _plannerAgent.PlanModificationAsync(
                            content, classResult, referenceResult, modificationTasks, codebaseAnalysis, ct);
                        
                        await _notificationService.NotifyProgressAsync(requirementId, 
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
                await _taskRepository.SaveTasksAsync(requirementId, tasks, ct);

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
            await _approvalRepository.ApprovePlanAsync(requirementId, ct);

            // Phase 2: Coding (with modification support and parallel execution by project)
            var codingResult = await ExecutePhaseAsync(execution, PipelinePhase.Coding, async () =>
            {
                await _notificationService.NotifyPhaseStartedAsync(requirementId, PipelinePhase.Coding, "Generating/Modifying code...");

                var tasks = (await _taskRepository.GetByRequirementAsync(requirementId, ct)).ToList();
                var projectState = new ProjectState { Requirement = content };
                var generatedFiles = new ConcurrentDictionary<string, string>();
                var modifiedFiles = new ConcurrentDictionary<string, string>();

                // Check if any tasks are modifications
                var hasModifications = tasks.Any(t => t.IsModification);
                if (hasModifications)
                {
                    await _notificationService.NotifyProgressAsync(requirementId, 
                        "Mode: MODIFICATION - will update existing files in the codebase");
                }

                // Group tasks by project
                var projectGroups = GroupTasksByProject(tasks);
                var completedProjects = new ConcurrentDictionary<string, bool>();

                await _notificationService.NotifyProgressAsync(requirementId, 
                    $"Found {projectGroups.Count} project(s): {string.Join(", ", projectGroups.Select(g => g.ProjectName))}");

                // Process projects in dependency order, parallelize where possible
                var processingLevels = GetProcessingLevels(projectGroups);
                
                foreach (var level in processingLevels)
                {
                    ct.ThrowIfCancellationRequested();

                    await _notificationService.NotifyProgressAsync(requirementId, 
                        $"Processing level {level.Key + 1}: {string.Join(", ", level.Value.Select(g => g.ProjectName))} (parallel)");

                    // Process all projects in this level in parallel
                    var levelTasks = level.Value.Select(async projectGroup =>
                    {
                        var projectFiles = new Dictionary<string, string>();

                        foreach (var task in projectGroup.Tasks)
                        {
                            ct.ThrowIfCancellationRequested();

                            var actionType = task.IsModification ? "Modifying" : "Generating";
                            await _notificationService.NotifyProgressAsync(requirementId, 
                                $"[{projectGroup.ProjectName}] {actionType}: {task.Title}");

                            // Use modification-aware coding if codebase is available
                            AgentResponse response;
                            if (codebaseAnalysis != null && !string.IsNullOrEmpty(requirement?.CodebaseId))
                            {
                                response = await ExecuteModificationCodingAsync(task, projectState, requirement.CodebaseId, codebaseAnalysis, ct);
                            }
                            else
                            {
                                response = await ExecuteGenerationCodingAsync(task, projectState, ct);
                            }

                            if (response.Success && response.Data.TryGetValue("filename", out var filename) &&
                                response.Data.TryGetValue("code", out var code))
                            {
                                var fileKey = $"{projectGroup.ProjectName}/{filename}";
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
                            await _taskRepository.UpdateTaskAsync(requirementId, task, ct);
                        }

                        completedProjects[projectGroup.ProjectName] = true;
                        await _notificationService.NotifyProgressAsync(requirementId, 
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
                    requirementId, 
                    requirement?.Name ?? requirementId, 
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

            // Phase 4: Debugging (DebuggerAgent - verify and fix code)
            var debugResult = await ExecutePhaseAsync(execution, PipelinePhase.Debugging, async () =>
            {
                await _notificationService.NotifyPhaseStartedAsync(requirementId, PipelinePhase.Debugging, 
                    "DebuggerAgent verifying and fixing code...");

                var files = await _outputRepository.GetGeneratedFilesAsync(requirementId, ct);
                
                if (files.Count == 0)
                {
                    await _notificationService.NotifyProgressAsync(requirementId, "No files to verify");
                    return new 
                    { 
                        Success = true, 
                        TotalFiles = 0,
                        Message = "No files generated to verify"
                    };
                }

                await _notificationService.NotifyProgressAsync(requirementId, $"Building {files.Count} files together...");

                // Use multi-file debugging - compile all files together in a single project
                // This allows test files to reference their implementation files
                var debugResponse = await _debuggerAgent.DebugMultipleFilesAsync(files, ct);

                ct.ThrowIfCancellationRequested();

                if (debugResponse.Success)
                {
                    await _notificationService.NotifyProgressAsync(requirementId, "All files compiled successfully!");
                    
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
                    await _notificationService.NotifyProgressAsync(requirementId, 
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
                await _notificationService.NotifyPhaseStartedAsync(requirementId, PipelinePhase.Reviewing, "Running code review...");

                var files = await _outputRepository.GetGeneratedFilesAsync(requirementId, ct);
                var projectState = new ProjectState
                {
                    Requirement = content,
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
                    await _outputRepository.SaveReviewReportAsync(requirementId, response.Output, ct);
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
            var deployCodebaseId = requirement.CodebaseId;
            if (!string.IsNullOrEmpty(deployCodebaseId))
            {
                // Store deployment result for potential rollback
                DeploymentResult? lastDeploymentResult = null;

                var deployResult = await ExecutePhaseAsync(execution, PipelinePhase.Deployment, async () =>
                {
                    await _notificationService.NotifyPhaseStartedAsync(requirementId, PipelinePhase.Deployment, 
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

                    await _notificationService.NotifyProgressAsync(requirementId, $"Deploying to: {codebaseAnalysis.CodebasePath}");
                    await _notificationService.NotifyProgressAsync(requirementId, 
                        $"Found {codebaseAnalysis.Projects.Count} projects in codebase");

                    // Get generated files
                    var files = await _outputRepository.GetGeneratedFilesAsync(requirementId, ct);
                    if (files.Count == 0)
                    {
                        return new 
                        { 
                            Success = true, 
                            Message = "No files to deploy"
                        };
                    }

                    await _notificationService.NotifyProgressAsync(requirementId, $"Deploying {files.Count} files...");

                    // Deploy files to codebase using full analysis for accurate project paths
                    var deploymentResult = await _deploymentAgent.DeployAsync(codebaseAnalysis, files, ct);
                    
                    // Store for potential rollback
                    lastDeploymentResult = deploymentResult;

                    if (deploymentResult.Success)
                    {
                        await _notificationService.NotifyProgressAsync(requirementId, 
                            $"Deployment successful! {deploymentResult.TotalFilesCopied} files deployed, build passed.");
                    }
                    else
                    {
                        await _notificationService.NotifyProgressAsync(requirementId, 
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
                        _logger.LogInformation("[{RequirementId}] Deployment rejected, starting rollback...", requirementId);
                        await _notificationService.NotifyProgressAsync(requirementId, "Rolling back deployment changes...");
                        
                        var rollbackResult = await _deploymentAgent.RollbackAsync(lastDeploymentResult, ct);
                        
                        if (rollbackResult.Success)
                        {
                            await _notificationService.NotifyProgressAsync(requirementId, 
                                $"Rollback complete: {rollbackResult.DeletedFiles.Count} files deleted, {rollbackResult.RevertedProjects.Count} projects reverted");
                        }
                        else
                        {
                            await _notificationService.NotifyProgressAsync(requirementId, 
                                $"Rollback had errors: {string.Join(", ", rollbackResult.Errors)}");
                        }
                    }
                    
                    // Mark requirement as failed
                    await _requirementRepository.UpdateStatusAsync(requirementId, RequirementStatus.Failed, ct);
                    await _notificationService.NotifyPhaseFailedAsync(requirementId, PipelinePhase.Deployment, 
                        execution.RejectionReason ?? "Deployment rejected by user");
                    
                    return;
                }
            }

            // Mark as completed (this also clears InProgress)
            await _requirementRepository.UpdateStatusAsync(requirementId, RequirementStatus.Completed, ct);

            var outputPath = await _outputRepository.GetOutputPathAsync(requirementId, ct);
            await _notificationService.NotifyPipelineCompletedAsync(requirementId, outputPath ?? "");

            execution.Status.CurrentPhase = PipelinePhase.Completed;
            execution.Status.CompletedAt = DateTime.UtcNow;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("[{RequirementId}] Pipeline cancelled", requirementId);
            await _approvalRepository.ResetInProgressAsync(requirementId, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{RequirementId}] Pipeline failed", requirementId);
            await _notificationService.NotifyPhaseFailedAsync(requirementId, execution.CurrentPhase, ex.Message);
            await _approvalRepository.ResetInProgressAsync(requirementId, CancellationToken.None);
        }
        finally
        {
            execution.Status.IsRunning = false;
            _runningPipelines.TryRemove(requirementId, out _);
            await _notificationService.NotifyRequirementListChangedAsync();
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
                execution.RequirementId, phase,
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
                await _notificationService.NotifyPhaseCompletedAsync(execution.RequirementId, phase, $"{phase} approved");
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

    private PipelineStatusDto CreateInitialStatus(string requirementId)
    {
        return new PipelineStatusDto
        {
            RequirementId = requirementId,
            CurrentPhase = PipelinePhase.None,
            IsRunning = false,
            Phases = new List<PhaseStatusDto>
            {
                new() { Phase = PipelinePhase.Analysis, State = PhaseState.Pending },    // CodeAnalysisAgent
                new() { Phase = PipelinePhase.Planning, State = PhaseState.Pending },    // PlannerAgent
                new() { Phase = PipelinePhase.Coding, State = PhaseState.Pending },      // CoderAgent
                new() { Phase = PipelinePhase.Debugging, State = PhaseState.Pending },   // DebuggerAgent
                new() { Phase = PipelinePhase.Reviewing, State = PhaseState.Pending },   // ReviewerAgent
                new() { Phase = PipelinePhase.Deployment, State = PhaseState.Pending }   // DeploymentAgent
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
                        Namespace = st.Namespace
                    });
                }
                projectOrder++;
            }
        }

        return tasks;
    }

    private class PipelineExecution
    {
        public string RequirementId { get; set; } = string.Empty;
        public bool AutoApproveAll { get; set; }
        public PipelineStatusDto Status { get; set; } = new();
        public PipelinePhase CurrentPhase { get; set; }
        public TaskCompletionSource<bool>? PhaseApprovalTcs { get; set; }
        public string? RejectionReason { get; set; }
        public CancellationTokenSource CancellationTokenSource { get; set; } = new();
    }

    private class PhaseResult
    {
        public bool Approved { get; set; }
        public object? Result { get; set; }
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
    /// Try to extract a class name from the requirement text for modification planning.
    /// Returns null if no specific class is mentioned.
    /// </summary>
    private string? ExtractTargetClassName(string requirement)
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
        var pascalMatches = System.Text.RegularExpressions.Regex.Matches(requirement, pascalCasePattern);
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
                requirement, pattern, System.Text.RegularExpressions.RegexOptions.None);
            
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

        _logger.LogInformation("[ExtractClassName] No class name detected in requirement");
        return null;
    }

    /// <summary>
    /// Execute modification-aware planning using CodeAnalysisAgent
    /// </summary>
    private async Task<AgentResponse> ExecuteModificationPlanningAsync(
        string requirement,
        string codebaseId,
        Core.Models.CodebaseAnalysis analysis,
        CancellationToken ct)
    {
        // Try to extract the target class name from the requirement
        var targetClassName = ExtractTargetClassName(requirement);
        
        if (string.IsNullOrEmpty(targetClassName))
        {
            _logger.LogInformation("[Planning] No specific class detected in requirement, using standard planning");
            return await _plannerAgent.PlanWithCodebaseAsync(requirement, analysis, null, ct);
        }

        _logger.LogInformation("[Planning] Detected target class: {ClassName}, searching for references", targetClassName);

        // Find the class
        var classResult = await _codeAnalysisAgent.FindClassAsync(analysis, targetClassName, includeContent: true);
        
        if (!classResult.Found)
        {
            _logger.LogWarning("[Planning] Class {ClassName} not found in codebase, using standard planning", targetClassName);
            return await _plannerAgent.PlanWithCodebaseAsync(requirement, analysis, null, ct);
        }

        _logger.LogInformation("[Planning] Found class {ClassName} at {FilePath}", targetClassName, classResult.FilePath);

        // Find all references to the class
        var referenceResult = await _codeAnalysisAgent.FindReferencesAsync(analysis, targetClassName, ct);
        
        _logger.LogInformation("[Planning] Found {RefCount} references in {FileCount} files", 
            referenceResult.References.Count, referenceResult.AffectedFiles.Count);

        // Create modification tasks
        var modificationTasks = await _codeAnalysisAgent.CreateModificationTasksAsync(
            analysis, referenceResult, $"Modification for: {requirement}", ct);

        // Use modification planning
        return await _plannerAgent.PlanModificationAsync(
            requirement,
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
        if (task.IsModification && !string.IsNullOrEmpty(task.FullPath))
        {
            // Load the current file content
            var currentContent = await _codeAnalysisAgent.GetFileContentAsync(task.FullPath);
            
            if (string.IsNullOrEmpty(currentContent))
            {
                _logger.LogWarning("[Coding] Could not load content for modification task: {FilePath}", task.FullPath);
                // Fall back to generation mode
                return await ExecuteGenerationCodingAsync(task, projectState, ct);
            }

            _logger.LogInformation("[Coding] Executing modification for: {FilePath}", task.FullPath);

            var context = new Dictionary<string, string>
            {
                ["is_modification"] = "true",
                ["current_content"] = currentContent,
                ["target_file"] = task.TargetFiles.FirstOrDefault() ?? Path.GetFileName(task.FullPath),
                ["full_path"] = task.FullPath,
                ["task_index"] = task.Index.ToString(),
                ["project_name"] = task.ProjectName ?? "default"
            };

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
}
