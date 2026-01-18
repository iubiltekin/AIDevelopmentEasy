namespace AIDevelopmentEasy.Api.Models;

// ═══════════════════════════════════════════════════════════════════════════════
// Requirement DTOs
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Requirement information for API responses
/// </summary>
public class RequirementDto
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string RawContent { get; set; } = string.Empty;
    public string? FinalContent { get; set; }
    public RequirementTypeDto Type { get; set; }
    public RequirementStatusDto Status { get; set; }
    public WizardPhaseDto CurrentPhase { get; set; }
    public string? CodebaseId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int StoryCount { get; set; }
    public List<string> CreatedStoryIds { get; set; } = new();
}

/// <summary>
/// Detailed requirement with all wizard data
/// </summary>
public class RequirementDetailDto : RequirementDto
{
    public QuestionSetDto? Questions { get; set; }
    public AnswerSetDto? Answers { get; set; }
    public string? AiNotes { get; set; }
    public List<StoryDefinitionDto> GeneratedStories { get; set; } = new();
}

/// <summary>
/// Request to create a new requirement
/// </summary>
public class CreateRequirementRequest
{
    /// <summary>
    /// Human-readable title (optional, auto-generated if not provided)
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Raw requirement text
    /// </summary>
    public string RawContent { get; set; } = string.Empty;

    /// <summary>
    /// Type of requirement
    /// </summary>
    public RequirementTypeDto Type { get; set; }

    /// <summary>
    /// Associated codebase ID (optional)
    /// </summary>
    public string? CodebaseId { get; set; }
}

/// <summary>
/// Request to update a requirement
/// </summary>
public class UpdateRequirementRequest
{
    public string? Title { get; set; }
    public string? RawContent { get; set; }
    public RequirementTypeDto? Type { get; set; }
    public string? CodebaseId { get; set; }
}

// ═══════════════════════════════════════════════════════════════════════════════
// Wizard Status DTOs
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Wizard execution status
/// </summary>
public class WizardStatusDto
{
    public string RequirementId { get; set; } = string.Empty;
    public WizardPhaseDto CurrentPhase { get; set; }
    public bool IsRunning { get; set; }
    public List<WizardPhaseStatusDto> Phases { get; set; } = new();
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Status of a single wizard phase
/// </summary>
public class WizardPhaseStatusDto
{
    public WizardPhaseDto Phase { get; set; }
    public WizardPhaseStateDto State { get; set; }
    public string? Message { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public object? Result { get; set; }
}

// ═══════════════════════════════════════════════════════════════════════════════
// Question & Answer DTOs
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Set of questions
/// </summary>
public class QuestionSetDto
{
    public List<QuestionDto> Questions { get; set; } = new();
}

/// <summary>
/// A single question
/// </summary>
public class QuestionDto
{
    public string Id { get; set; } = string.Empty;
    public QuestionCategoryDto Category { get; set; }
    public string Text { get; set; } = string.Empty;
    public QuestionTypeDto Type { get; set; }
    public List<string> Options { get; set; } = new();
    public bool Required { get; set; }
    public string? Context { get; set; }
}

/// <summary>
/// Set of answers
/// </summary>
public class AnswerSetDto
{
    public List<AnswerDto> Answers { get; set; } = new();
}

/// <summary>
/// A single answer
/// </summary>
public class AnswerDto
{
    public string QuestionId { get; set; } = string.Empty;
    public List<string> SelectedOptions { get; set; } = new();
    public string? TextResponse { get; set; }
}

/// <summary>
/// Request to submit answers
/// </summary>
public class SubmitAnswersRequest
{
    public List<AnswerDto> Answers { get; set; } = new();
    public string? AiNotes { get; set; }
}

// ═══════════════════════════════════════════════════════════════════════════════
// Story Definition DTOs
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Story definition from decomposition
/// </summary>
public class StoryDefinitionDto
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> AcceptanceCriteria { get; set; } = new();
    public StoryComplexityDto EstimatedComplexity { get; set; }
    public List<string> Dependencies { get; set; } = new();
    public string? TechnicalNotes { get; set; }
    public bool Selected { get; set; } = true;
}

/// <summary>
/// Request to create stories from selected definitions
/// </summary>
public class CreateStoriesRequest
{
    /// <summary>
    /// IDs of selected story definitions to create
    /// </summary>
    public List<string> SelectedStoryIds { get; set; } = new();
}

// ═══════════════════════════════════════════════════════════════════════════════
// Wizard Action Requests
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Request to start the wizard
/// </summary>
public class StartWizardRequest
{
    /// <summary>
    /// Auto-approve all phases (skip waiting for user approval)
    /// </summary>
    public bool AutoApproveAll { get; set; } = false;
}

/// <summary>
/// Request to approve current phase
/// </summary>
public class ApproveWizardPhaseRequest
{
    public bool Approved { get; set; } = true;
    public string? Comment { get; set; }
}

// ═══════════════════════════════════════════════════════════════════════════════
// Enums
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Requirement type
/// </summary>
public enum RequirementTypeDto
{
    Feature,
    Improvement,
    Defect,
    TechDebt
}

/// <summary>
/// Requirement status
/// </summary>
public enum RequirementStatusDto
{
    Draft,
    InProgress,
    Completed,
    Cancelled,
    Failed
}

/// <summary>
/// Wizard phase
/// </summary>
public enum WizardPhaseDto
{
    Input,
    Analysis,
    Questions,
    Refinement,
    Decomposition,
    Review,
    Completed
}

/// <summary>
/// Wizard phase state
/// </summary>
public enum WizardPhaseStateDto
{
    Pending,
    Running,
    WaitingApproval,
    Completed,
    Failed,
    Skipped
}

/// <summary>
/// Question category
/// </summary>
public enum QuestionCategoryDto
{
    Functional,
    NonFunctional,
    Technical,
    Business,
    UX
}

/// <summary>
/// Question type
/// </summary>
public enum QuestionTypeDto
{
    Single,
    Multiple,
    Text
}

/// <summary>
/// Story complexity
/// </summary>
public enum StoryComplexityDto
{
    Small,
    Medium,
    Large
}

// ═══════════════════════════════════════════════════════════════════════════════
// Real-time Update Messages
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Real-time wizard update message (for SignalR)
/// </summary>
public class WizardUpdateMessage
{
    public string RequirementId { get; set; } = string.Empty;
    public string UpdateType { get; set; } = string.Empty;
    public WizardPhaseDto Phase { get; set; }
    public string Message { get; set; } = string.Empty;
    public object? Data { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
