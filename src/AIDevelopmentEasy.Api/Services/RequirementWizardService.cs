using System.Collections.Concurrent;
using AIDevelopmentEasy.Api.Models;
using AIDevelopmentEasy.Api.Repositories.Interfaces;
using AIDevelopmentEasy.Api.Services.Interfaces;
using AIDevelopmentEasy.Core.Agents;
using AIDevelopmentEasy.Core.Models;

namespace AIDevelopmentEasy.Api.Services;

/// <summary>
/// Requirement Wizard orchestration service.
/// Manages the wizard workflow with approval gates at each phase:
/// 
/// 1. Input → (Create) → Analysis (LLM generates questions)
/// 2. Analysis → (Approve) → Questions (User answers)
/// 3. Questions → (Submit) → Refinement (LLM creates final doc)
/// 4. Refinement → (Approve) → Decomposition (LLM generates stories)
/// 5. Decomposition → (Approve) → Review (User selects stories)
/// 6. Review → (Create Stories) → Completed
/// </summary>
public class RequirementWizardService : IRequirementWizardService
{
    private readonly IRequirementRepository _requirementRepository;
    private readonly IStoryRepository _storyRepository;
    private readonly RequirementAnalystAgent _analystAgent;
    private readonly ILogger<RequirementWizardService> _logger;

    // Track running wizards
    private static readonly ConcurrentDictionary<string, WizardExecution> _runningWizards = new();

    public RequirementWizardService(
        IRequirementRepository requirementRepository,
        IStoryRepository storyRepository,
        RequirementAnalystAgent analystAgent,
        ILogger<RequirementWizardService> logger)
    {
        _requirementRepository = requirementRepository;
        _storyRepository = storyRepository;
        _analystAgent = analystAgent;
        _logger = logger;
    }

    public async Task<WizardStatusDto> StartAsync(string requirementId, bool autoApproveAll = false, CancellationToken cancellationToken = default)
    {
        // Check if already running
        if (_runningWizards.ContainsKey(requirementId))
        {
            throw new InvalidOperationException($"Wizard already running for requirement: {requirementId}");
        }

        // Get the requirement
        var requirement = await _requirementRepository.GetEntityAsync(requirementId, cancellationToken);
        if (requirement == null)
        {
            throw new ArgumentException($"Requirement not found: {requirementId}");
        }

        // Check if in Draft status
        if (requirement.Status != RequirementStatus.Draft)
        {
            throw new InvalidOperationException($"Requirement must be in Draft status to start wizard. Current: {requirement.Status}");
        }

        // Create execution context
        var execution = new WizardExecution
        {
            RequirementId = requirementId,
            AutoApproveAll = autoApproveAll,
            Status = CreateInitialStatus(requirementId),
            CancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
        };

        _runningWizards[requirementId] = execution;

        // Update requirement status
        await _requirementRepository.UpdateStatusAsync(requirementId, RequirementStatusDto.InProgress, cancellationToken);

        // Start with Analysis phase
        _ = Task.Run(() => ExecuteAnalysisPhaseAsync(execution), execution.CancellationTokenSource.Token);

        return execution.Status;
    }

    public async Task<WizardStatusDto?> GetStatusAsync(string requirementId, CancellationToken cancellationToken = default)
    {
        // Check running wizards first
        if (_runningWizards.TryGetValue(requirementId, out var execution))
        {
            return execution.Status;
        }

        // Check saved status
        return await _requirementRepository.GetWizardStatusAsync(requirementId, cancellationToken);
    }

    public async Task<WizardStatusDto> ApprovePhaseAsync(string requirementId, bool approved = true, string? comment = null, CancellationToken cancellationToken = default)
    {
        if (!_runningWizards.TryGetValue(requirementId, out var execution))
        {
            throw new InvalidOperationException($"No wizard running for requirement: {requirementId}");
        }

        var currentPhase = execution.Status.CurrentPhase;
        _logger.LogInformation("[Wizard] Approval for phase {Phase}: {Approved}", currentPhase, approved);

        if (!approved)
        {
            // Cancel the wizard
            return await CancelAsync(requirementId, cancellationToken);
        }

        // Proceed to next phase based on current phase
        switch (currentPhase)
        {
            case WizardPhaseDto.Analysis:
                // Analysis complete, move to Questions (waiting for user input)
                UpdatePhaseState(execution, WizardPhaseDto.Analysis, WizardPhaseStateDto.Completed);
                UpdatePhaseState(execution, WizardPhaseDto.Questions, WizardPhaseStateDto.WaitingApproval, "Waiting for answers");
                execution.Status.CurrentPhase = WizardPhaseDto.Questions;
                await _requirementRepository.UpdatePhaseAsync(requirementId, WizardPhaseDto.Questions, cancellationToken);
                break;

            case WizardPhaseDto.Refinement:
                // Refinement complete, start Decomposition
                UpdatePhaseState(execution, WizardPhaseDto.Refinement, WizardPhaseStateDto.Completed);
                _ = Task.Run(() => ExecuteDecompositionPhaseAsync(execution), execution.CancellationTokenSource.Token);
                break;

            case WizardPhaseDto.Decomposition:
                // Decomposition complete, move to Review
                UpdatePhaseState(execution, WizardPhaseDto.Decomposition, WizardPhaseStateDto.Completed);
                UpdatePhaseState(execution, WizardPhaseDto.Review, WizardPhaseStateDto.WaitingApproval, "Select stories to create");
                execution.Status.CurrentPhase = WizardPhaseDto.Review;
                await _requirementRepository.UpdatePhaseAsync(requirementId, WizardPhaseDto.Review, cancellationToken);
                break;

            default:
                _logger.LogWarning("[Wizard] Unexpected approve at phase: {Phase}", currentPhase);
                break;
        }

        await _requirementRepository.SaveWizardStatusAsync(requirementId, execution.Status, cancellationToken);
        return execution.Status;
    }

    public async Task<WizardStatusDto> SubmitAnswersAsync(string requirementId, SubmitAnswersRequest request, CancellationToken cancellationToken = default)
    {
        if (!_runningWizards.TryGetValue(requirementId, out var execution))
        {
            throw new InvalidOperationException($"No wizard running for requirement: {requirementId}");
        }

        if (execution.Status.CurrentPhase != WizardPhaseDto.Questions)
        {
            throw new InvalidOperationException($"Cannot submit answers in phase: {execution.Status.CurrentPhase}");
        }

        _logger.LogInformation("[Wizard] Submitting {Count} answers for requirement {Id}", request.Answers.Count, requirementId);

        // Save answers
        var answerSet = new AnswerSetDto { Answers = request.Answers };
        await _requirementRepository.SaveAnswersAsync(requirementId, answerSet, request.AiNotes, cancellationToken);

        // Mark Questions phase complete
        UpdatePhaseState(execution, WizardPhaseDto.Questions, WizardPhaseStateDto.Completed);

        // Start Refinement phase
        _ = Task.Run(() => ExecuteRefinementPhaseAsync(execution), execution.CancellationTokenSource.Token);

        await _requirementRepository.SaveWizardStatusAsync(requirementId, execution.Status, cancellationToken);
        return execution.Status;
    }

    public async Task<WizardStatusDto> CreateStoriesAsync(string requirementId, CreateStoriesRequest request, CancellationToken cancellationToken = default)
    {
        if (!_runningWizards.TryGetValue(requirementId, out var execution))
        {
            throw new InvalidOperationException($"No wizard running for requirement: {requirementId}");
        }

        if (execution.Status.CurrentPhase != WizardPhaseDto.Review)
        {
            throw new InvalidOperationException($"Cannot create stories in phase: {execution.Status.CurrentPhase}");
        }

        _logger.LogInformation("[Wizard] Creating {Count} stories for requirement {Id}", request.SelectedStoryIds.Count, requirementId);

        UpdatePhaseState(execution, WizardPhaseDto.Review, WizardPhaseStateDto.Running, "Creating stories...");

        try
        {
            // Get requirement with generated stories
            var requirement = await _requirementRepository.GetByIdAsync(requirementId, cancellationToken);
            if (requirement == null)
            {
                throw new InvalidOperationException("Requirement not found");
            }

            // Filter selected stories
            var selectedStories = requirement.GeneratedStories
                .Where(s => request.SelectedStoryIds.Contains(s.Id))
                .ToList();

            // Create each story
            foreach (var storyDef in selectedStories)
            {
                // Build story content from definition
                var content = BuildStoryContent(storyDef, requirement);

                // Create the story with link to requirement
                var story = await _storyRepository.CreateAsync(
                    storyDef.Title,
                    content,
                    StoryType.Single,
                    requirement.CodebaseId,
                    requirementId,  // Link to requirement
                    cancellationToken);

                // Link to requirement
                await _requirementRepository.AddCreatedStoryIdAsync(requirementId, story.Id, cancellationToken);

                _logger.LogInformation("[Wizard] Created story: {Id} - {Title}", story.Id, story.Name);
            }

            // Mark Review complete
            UpdatePhaseState(execution, WizardPhaseDto.Review, WizardPhaseStateDto.Completed);

            // Mark wizard complete
            UpdatePhaseState(execution, WizardPhaseDto.Completed, WizardPhaseStateDto.Completed, $"Created {selectedStories.Count} stories");
            execution.Status.CurrentPhase = WizardPhaseDto.Completed;
            execution.Status.CompletedAt = DateTime.UtcNow;
            execution.Status.IsRunning = false;

            // Update requirement status
            await _requirementRepository.UpdateStatusAsync(requirementId, RequirementStatusDto.Completed, cancellationToken);
            await _requirementRepository.UpdatePhaseAsync(requirementId, WizardPhaseDto.Completed, cancellationToken);

            // Remove from running wizards
            _runningWizards.TryRemove(requirementId, out _);

            _logger.LogInformation("[Wizard] Completed for requirement {Id}", requirementId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Wizard] Error creating stories for {Id}", requirementId);
            UpdatePhaseState(execution, WizardPhaseDto.Review, WizardPhaseStateDto.Failed, ex.Message);
            execution.Status.Error = ex.Message;
        }

        await _requirementRepository.SaveWizardStatusAsync(requirementId, execution.Status, cancellationToken);
        return execution.Status;
    }

    public async Task<WizardStatusDto> CancelAsync(string requirementId, CancellationToken cancellationToken = default)
    {
        if (_runningWizards.TryRemove(requirementId, out var execution))
        {
            execution.CancellationTokenSource.Cancel();
            execution.Status.IsRunning = false;
            execution.Status.Error = "Cancelled by user";

            // Mark current phase as failed
            var currentPhaseStatus = execution.Status.Phases.FirstOrDefault(p => p.Phase == execution.Status.CurrentPhase);
            if (currentPhaseStatus != null)
            {
                currentPhaseStatus.State = WizardPhaseStateDto.Failed;
                currentPhaseStatus.Message = "Cancelled";
            }

            await _requirementRepository.UpdateStatusAsync(requirementId, RequirementStatusDto.Cancelled, cancellationToken);
            await _requirementRepository.SaveWizardStatusAsync(requirementId, execution.Status, cancellationToken);

            _logger.LogInformation("[Wizard] Cancelled for requirement {Id}", requirementId);
            return execution.Status;
        }

        throw new InvalidOperationException($"No wizard running for requirement: {requirementId}");
    }

    public Task<bool> IsRunningAsync(string requirementId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_runningWizards.ContainsKey(requirementId));
    }

    #region Phase Execution

    private async Task ExecuteAnalysisPhaseAsync(WizardExecution execution)
    {
        var requirementId = execution.RequirementId;
        _logger.LogInformation("[Wizard] Starting Analysis phase for {Id}", requirementId);

        UpdatePhaseState(execution, WizardPhaseDto.Analysis, WizardPhaseStateDto.Running, "Analyzing requirement...");
        execution.Status.CurrentPhase = WizardPhaseDto.Analysis;

        try
        {
            var requirement = await _requirementRepository.GetEntityAsync(requirementId, execution.CancellationTokenSource.Token);
            if (requirement == null)
            {
                throw new InvalidOperationException("Requirement not found");
            }

            // Call LLM to analyze and generate questions
            var result = await _analystAgent.AnalyzeAsync(
                requirement.RawContent,
                requirement.Type,
                execution.CancellationTokenSource.Token);

            if (!result.Success)
            {
                throw new Exception(result.Error ?? "Analysis failed");
            }

            // Save questions
            var questionsDto = MapQuestionSetToDto(result.Questions!);
            await _requirementRepository.SaveQuestionsAsync(requirementId, questionsDto, execution.CancellationTokenSource.Token);
            await _requirementRepository.UpdatePhaseAsync(requirementId, WizardPhaseDto.Analysis, execution.CancellationTokenSource.Token);

            // Update phase status
            UpdatePhaseState(execution, WizardPhaseDto.Analysis, WizardPhaseStateDto.WaitingApproval,
                $"Generated {result.Questions!.Questions.Count} questions",
                result.Questions);

            // If auto-approve, proceed immediately
            if (execution.AutoApproveAll)
            {
                await ApprovePhaseAsync(requirementId, true, "Auto-approved", execution.CancellationTokenSource.Token);
            }

            _logger.LogInformation("[Wizard] Analysis complete for {Id}, generated {Count} questions",
                requirementId, result.Questions!.Questions.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Wizard] Analysis failed for {Id}", requirementId);
            UpdatePhaseState(execution, WizardPhaseDto.Analysis, WizardPhaseStateDto.Failed, ex.Message);
            execution.Status.Error = ex.Message;
            await _requirementRepository.UpdateStatusAsync(requirementId, RequirementStatusDto.Failed, execution.CancellationTokenSource.Token);
            _runningWizards.TryRemove(requirementId, out _);
        }

        await _requirementRepository.SaveWizardStatusAsync(requirementId, execution.Status, execution.CancellationTokenSource.Token);
    }

    private async Task ExecuteRefinementPhaseAsync(WizardExecution execution)
    {
        var requirementId = execution.RequirementId;
        _logger.LogInformation("[Wizard] Starting Refinement phase for {Id}", requirementId);

        UpdatePhaseState(execution, WizardPhaseDto.Refinement, WizardPhaseStateDto.Running, "Creating final requirement...");
        execution.Status.CurrentPhase = WizardPhaseDto.Refinement;

        try
        {
            var requirement = await _requirementRepository.GetEntityAsync(requirementId, execution.CancellationTokenSource.Token);
            if (requirement == null)
            {
                throw new InvalidOperationException("Requirement not found");
            }

            // Call LLM to refine
            var result = await _analystAgent.RefineAsync(
                requirement.RawContent,
                requirement.Type,
                requirement.Answers!,
                requirement.AiNotes,
                execution.CancellationTokenSource.Token);

            if (!result.Success)
            {
                throw new Exception(result.Error ?? "Refinement failed");
            }

            // Save final content
            await _requirementRepository.SaveFinalContentAsync(requirementId, result.FinalRequirement!, execution.CancellationTokenSource.Token);
            await _requirementRepository.UpdatePhaseAsync(requirementId, WizardPhaseDto.Refinement, execution.CancellationTokenSource.Token);

            // Update phase status
            UpdatePhaseState(execution, WizardPhaseDto.Refinement, WizardPhaseStateDto.WaitingApproval,
                "Final requirement created",
                result.FinalRequirement);

            // If auto-approve, proceed immediately
            if (execution.AutoApproveAll)
            {
                await ApprovePhaseAsync(requirementId, true, "Auto-approved", execution.CancellationTokenSource.Token);
            }

            _logger.LogInformation("[Wizard] Refinement complete for {Id}", requirementId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Wizard] Refinement failed for {Id}", requirementId);
            UpdatePhaseState(execution, WizardPhaseDto.Refinement, WizardPhaseStateDto.Failed, ex.Message);
            execution.Status.Error = ex.Message;
            await _requirementRepository.UpdateStatusAsync(requirementId, RequirementStatusDto.Failed, execution.CancellationTokenSource.Token);
            _runningWizards.TryRemove(requirementId, out _);
        }

        await _requirementRepository.SaveWizardStatusAsync(requirementId, execution.Status, execution.CancellationTokenSource.Token);
    }

    private async Task ExecuteDecompositionPhaseAsync(WizardExecution execution)
    {
        var requirementId = execution.RequirementId;
        _logger.LogInformation("[Wizard] Starting Decomposition phase for {Id}", requirementId);

        UpdatePhaseState(execution, WizardPhaseDto.Decomposition, WizardPhaseStateDto.Running, "Generating stories...");
        execution.Status.CurrentPhase = WizardPhaseDto.Decomposition;

        try
        {
            var requirement = await _requirementRepository.GetEntityAsync(requirementId, execution.CancellationTokenSource.Token);
            if (requirement == null)
            {
                throw new InvalidOperationException("Requirement not found");
            }

            // Call LLM to decompose
            var result = await _analystAgent.DecomposeAsync(
                requirement.FinalContent!,
                requirement.Type,
                execution.CancellationTokenSource.Token);

            if (!result.Success)
            {
                throw new Exception(result.Error ?? "Decomposition failed");
            }

            // Save generated stories
            var storiesDto = result.Stories.Select(MapStoryDefinitionToDto).ToList();
            await _requirementRepository.SaveGeneratedStoriesAsync(requirementId, storiesDto, execution.CancellationTokenSource.Token);
            await _requirementRepository.UpdatePhaseAsync(requirementId, WizardPhaseDto.Decomposition, execution.CancellationTokenSource.Token);

            // Update phase status
            UpdatePhaseState(execution, WizardPhaseDto.Decomposition, WizardPhaseStateDto.WaitingApproval,
                $"Generated {result.Stories.Count} stories",
                storiesDto);

            // If auto-approve, proceed immediately
            if (execution.AutoApproveAll)
            {
                await ApprovePhaseAsync(requirementId, true, "Auto-approved", execution.CancellationTokenSource.Token);
            }

            _logger.LogInformation("[Wizard] Decomposition complete for {Id}, generated {Count} stories",
                requirementId, result.Stories.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Wizard] Decomposition failed for {Id}", requirementId);
            UpdatePhaseState(execution, WizardPhaseDto.Decomposition, WizardPhaseStateDto.Failed, ex.Message);
            execution.Status.Error = ex.Message;
            await _requirementRepository.UpdateStatusAsync(requirementId, RequirementStatusDto.Failed, execution.CancellationTokenSource.Token);
            _runningWizards.TryRemove(requirementId, out _);
        }

        await _requirementRepository.SaveWizardStatusAsync(requirementId, execution.Status, execution.CancellationTokenSource.Token);
    }

    #endregion

    #region Helper Methods

    private WizardStatusDto CreateInitialStatus(string requirementId)
    {
        return new WizardStatusDto
        {
            RequirementId = requirementId,
            CurrentPhase = WizardPhaseDto.Input,
            IsRunning = true,
            StartedAt = DateTime.UtcNow,
            Phases = new List<WizardPhaseStatusDto>
            {
                new() { Phase = WizardPhaseDto.Input, State = WizardPhaseStateDto.Completed, CompletedAt = DateTime.UtcNow },
                new() { Phase = WizardPhaseDto.Analysis, State = WizardPhaseStateDto.Pending },
                new() { Phase = WizardPhaseDto.Questions, State = WizardPhaseStateDto.Pending },
                new() { Phase = WizardPhaseDto.Refinement, State = WizardPhaseStateDto.Pending },
                new() { Phase = WizardPhaseDto.Decomposition, State = WizardPhaseStateDto.Pending },
                new() { Phase = WizardPhaseDto.Review, State = WizardPhaseStateDto.Pending },
                new() { Phase = WizardPhaseDto.Completed, State = WizardPhaseStateDto.Pending }
            }
        };
    }

    private void UpdatePhaseState(WizardExecution execution, WizardPhaseDto phase, WizardPhaseStateDto state, string? message = null, object? result = null)
    {
        var phaseStatus = execution.Status.Phases.FirstOrDefault(p => p.Phase == phase);
        if (phaseStatus != null)
        {
            phaseStatus.State = state;
            phaseStatus.Message = message;
            phaseStatus.Result = result;

            if (state == WizardPhaseStateDto.Running)
                phaseStatus.StartedAt = DateTime.UtcNow;
            else if (state == WizardPhaseStateDto.Completed || state == WizardPhaseStateDto.Failed)
                phaseStatus.CompletedAt = DateTime.UtcNow;
        }
    }

    private string BuildStoryContent(StoryDefinitionDto story, RequirementDetailDto requirement)
    {
        var content = $@"# {story.Title}

## Description

{story.Description}

## Acceptance Criteria

{string.Join("\n", story.AcceptanceCriteria.Select(ac => $"- {ac}"))}

## Technical Notes

{story.TechnicalNotes ?? "N/A"}

## Complexity

{story.EstimatedComplexity}

---

**Parent Requirement:** {requirement.Id} - {requirement.Title}
";
        return content;
    }

    private QuestionSetDto MapQuestionSetToDto(QuestionSet set)
    {
        return new QuestionSetDto
        {
            Questions = set.Questions.Select(q => new QuestionDto
            {
                Id = q.Id,
                Category = (QuestionCategoryDto)q.Category,
                Text = q.Text,
                Type = (QuestionTypeDto)q.Type,
                Options = q.Options,
                Required = q.Required,
                Context = q.Context
            }).ToList()
        };
    }

    private StoryDefinitionDto MapStoryDefinitionToDto(StoryDefinition def)
    {
        return new StoryDefinitionDto
        {
            Id = def.Id,
            Title = def.Title,
            Description = def.Description,
            AcceptanceCriteria = def.AcceptanceCriteria,
            EstimatedComplexity = (StoryComplexityDto)def.EstimatedComplexity,
            Dependencies = def.Dependencies,
            TechnicalNotes = def.TechnicalNotes,
            Selected = def.Selected
        };
    }

    #endregion

    /// <summary>
    /// Wizard execution context
    /// </summary>
    private class WizardExecution
    {
        public string RequirementId { get; set; } = string.Empty;
        public bool AutoApproveAll { get; set; }
        public WizardStatusDto Status { get; set; } = new();
        public CancellationTokenSource CancellationTokenSource { get; set; } = new();
    }
}
