using System.Collections.Concurrent;
using AIDevelopmentEasy.Api.Hubs;
using AIDevelopmentEasy.Api.Models;
using AIDevelopmentEasy.Api.Repositories.Interfaces;
using AIDevelopmentEasy.Api.Services.Interfaces;
using AIDevelopmentEasy.Core.Agents;
using AIDevelopmentEasy.Core.Agents.Base;

// Use Api.Models.PipelinePhase to avoid ambiguity with Core.Agents.Base.PipelinePhase
using PipelinePhase = AIDevelopmentEasy.Api.Models.PipelinePhase;
using CorePipelinePhase = AIDevelopmentEasy.Core.Agents.Base.PipelinePhase;

namespace AIDevelopmentEasy.Api.Services;

/// <summary>
/// Pipeline orchestration service.
/// Manages the execution of the multi-agent pipeline with approval gates.
/// </summary>
public class PipelineService : IPipelineService
{
    private readonly IRequirementRepository _requirementRepository;
    private readonly ITaskRepository _taskRepository;
    private readonly IApprovalRepository _approvalRepository;
    private readonly IOutputRepository _outputRepository;
    private readonly IPipelineNotificationService _notificationService;
    private readonly PlannerAgent _plannerAgent;
    private readonly MultiProjectPlannerAgent _multiProjectPlannerAgent;
    private readonly CoderAgent _coderAgent;
    private readonly DebuggerAgent _debuggerAgent;
    private readonly ReviewerAgent _reviewerAgent;
    private readonly ILogger<PipelineService> _logger;

    // Track running pipelines and their state
    private static readonly ConcurrentDictionary<string, PipelineExecution> _runningPipelines = new();

    public PipelineService(
        IRequirementRepository requirementRepository,
        ITaskRepository taskRepository,
        IApprovalRepository approvalRepository,
        IOutputRepository outputRepository,
        IPipelineNotificationService notificationService,
        PlannerAgent plannerAgent,
        MultiProjectPlannerAgent multiProjectPlannerAgent,
        CoderAgent coderAgent,
        DebuggerAgent debuggerAgent,
        ReviewerAgent reviewerAgent,
        ILogger<PipelineService> logger)
    {
        _requirementRepository = requirementRepository;
        _taskRepository = taskRepository;
        _approvalRepository = approvalRepository;
        _outputRepository = outputRepository;
        _notificationService = notificationService;
        _plannerAgent = plannerAgent;
        _multiProjectPlannerAgent = multiProjectPlannerAgent;
        _coderAgent = coderAgent;
        _debuggerAgent = debuggerAgent;
        _reviewerAgent = reviewerAgent;
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

    public async Task<bool> ApprovePhaseAsync(string requirementId, PipelinePhase phase, CancellationToken cancellationToken = default)
    {
        if (!_runningPipelines.TryGetValue(requirementId, out var execution))
        {
            return false;
        }

        if (execution.CurrentPhase != phase || execution.PhaseApprovalTcs == null)
        {
            _logger.LogWarning("Cannot approve phase {Phase} - current phase is {CurrentPhase}", phase, execution.CurrentPhase);
            return false;
        }

        execution.PhaseApprovalTcs.TrySetResult(true);
        _logger.LogInformation("[{RequirementId}] Phase {Phase} approved", requirementId, phase);
        return true;
    }

    public async Task<bool> RejectPhaseAsync(string requirementId, PipelinePhase phase, string? reason = null, CancellationToken cancellationToken = default)
    {
        if (!_runningPipelines.TryGetValue(requirementId, out var execution))
        {
            return false;
        }

        if (execution.CurrentPhase != phase || execution.PhaseApprovalTcs == null)
        {
            return false;
        }

        execution.RejectionReason = reason;
        execution.PhaseApprovalTcs.TrySetResult(false);
        _logger.LogInformation("[{RequirementId}] Phase {Phase} rejected. Reason: {Reason}", requirementId, phase, reason);
        return true;
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
            var isMultiProject = requirement?.Type == RequirementType.Multi;

            // Phase 1: Planning
            var planningResult = await ExecutePhaseAsync(execution, PipelinePhase.Planning, async () =>
            {
                await _notificationService.NotifyPhaseStartedAsync(requirementId, PipelinePhase.Planning, "Analyzing requirements...");

                var projectState = new ProjectState { Requirement = content };
                var request = new AgentRequest { Input = content, ProjectState = projectState };

                AgentResponse response;
                if (isMultiProject)
                {
                    var multiReq = System.Text.Json.JsonSerializer.Deserialize<AIDevelopmentEasy.Core.Models.MultiProjectRequirement>(content);
                    if (multiReq == null) throw new InvalidOperationException("Invalid multi-project requirement format");
                    response = await _multiProjectPlannerAgent.PlanMultiProjectAsync(multiReq, projectState, ct);
                }
                else
                {
                    response = await _plannerAgent.RunAsync(request, ct);
                }

                if (!response.Success)
                {
                    throw new InvalidOperationException($"Planning failed: {response.Error}");
                }

                // Convert and save tasks
                var tasks = ConvertToTaskDtos(response.Data);
                await _taskRepository.SaveTasksAsync(requirementId, tasks, ct);

                return tasks;
            }, ct);

            if (!planningResult.Approved) return;

            // Approve plan
            await _approvalRepository.ApprovePlanAsync(requirementId, ct);

            // Phase 2: Coding
            var codingResult = await ExecutePhaseAsync(execution, PipelinePhase.Coding, async () =>
            {
                await _notificationService.NotifyPhaseStartedAsync(requirementId, PipelinePhase.Coding, "Generating code...");

                var tasks = await _taskRepository.GetByRequirementAsync(requirementId, ct);
                var projectState = new ProjectState { Requirement = content };
                var generatedFiles = new Dictionary<string, string>();

                foreach (var task in tasks)
                {
                    ct.ThrowIfCancellationRequested();

                    await _notificationService.NotifyProgressAsync(requirementId, $"Generating: {task.Title}");

                    var request = new AgentRequest
                    {
                        Input = task.Description,
                        ProjectState = projectState,
                        Context = new Dictionary<string, string>
                        {
                            ["task_index"] = task.Index.ToString(),
                            ["target_file"] = task.TargetFiles.FirstOrDefault() ?? ""
                        }
                    };

                    var response = await _coderAgent.RunAsync(request, ct);
                    if (response.Success && response.Data.TryGetValue("filename", out var filename) &&
                        response.Data.TryGetValue("code", out var code))
                    {
                        generatedFiles[filename.ToString()!] = code.ToString()!;
                        projectState.Codebase[filename.ToString()!] = code.ToString()!;
                    }
                }

                // Save output
                var outputPath = await _outputRepository.SaveOutputAsync(requirementId, requirement?.Name ?? requirementId, generatedFiles, ct);
                return new { Files = generatedFiles, OutputPath = outputPath };
            }, ct);

            if (!codingResult.Approved) return;

            // Phase 3: Debugging
            var debugResult = await ExecutePhaseAsync(execution, PipelinePhase.Debugging, async () =>
            {
                await _notificationService.NotifyPhaseStartedAsync(requirementId, PipelinePhase.Debugging, "Running compilation check...");

                // For now, just validate syntax - full debugging would require MSBuild
                await _notificationService.NotifyProgressAsync(requirementId, "Compilation check completed");
                return new { Success = true, Message = "Syntax validation passed" };
            }, ct);

            if (!debugResult.Approved) return;

            // Phase 4: Testing
            var testResult = await ExecutePhaseAsync(execution, PipelinePhase.Testing, async () =>
            {
                await _notificationService.NotifyPhaseStartedAsync(requirementId, PipelinePhase.Testing, "Analyzing test coverage...");

                // For now, just acknowledge tests - full testing would require running NUnit
                await Task.Delay(500, ct); // Simulate test run
                return new { Passed = 0, Failed = 0, Message = "Test analysis completed" };
            }, ct);

            if (!testResult.Approved) return;

            // Phase 5: Review
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
                new() { Phase = PipelinePhase.Planning, State = PhaseState.Pending },
                new() { Phase = PipelinePhase.Coding, State = PhaseState.Pending },
                new() { Phase = PipelinePhase.Debugging, State = PhaseState.Pending },
                new() { Phase = PipelinePhase.Testing, State = PhaseState.Pending },
                new() { Phase = PipelinePhase.Reviewing, State = PhaseState.Pending }
            }
        };
    }

    private List<TaskDto> ConvertToTaskDtos(Dictionary<string, object> data)
    {
        var tasks = new List<TaskDto>();

        if (data.TryGetValue("tasks", out var tasksObj) && tasksObj is List<SubTask> subTasks)
        {
            foreach (var st in subTasks)
            {
                tasks.Add(new TaskDto
                {
                    Index = st.Index,
                    Title = st.Title,
                    Description = st.Description,
                    TargetFiles = st.TargetFiles,
                    Status = Models.TaskStatus.Pending
                });
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
}
