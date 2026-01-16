namespace AIDevelopmentEasy.Api.Models;

/// <summary>
/// Requirement information for API responses
/// </summary>
public class RequirementDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public RequirementType Type { get; set; }
    public RequirementStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastProcessedAt { get; set; }
    public List<TaskDto> Tasks { get; set; } = new();
}

/// <summary>
/// Task information
/// </summary>
public class TaskDto
{
    public int Index { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public List<string> TargetFiles { get; set; } = new();
    public TaskStatus Status { get; set; }
}

/// <summary>
/// Pipeline execution status
/// </summary>
public class PipelineStatusDto
{
    public string RequirementId { get; set; } = string.Empty;
    public PipelinePhase CurrentPhase { get; set; }
    public bool IsRunning { get; set; }
    public List<PhaseStatusDto> Phases { get; set; } = new();
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

/// <summary>
/// Individual phase status
/// </summary>
public class PhaseStatusDto
{
    public PipelinePhase Phase { get; set; }
    public PhaseState State { get; set; }
    public string? Message { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public object? Result { get; set; }
}

/// <summary>
/// Request to process a requirement
/// </summary>
public class ProcessRequirementRequest
{
    public bool AutoApproveAll { get; set; } = false;
}

/// <summary>
/// Request to approve a phase
/// </summary>
public class ApprovePhaseRequest
{
    public bool Approved { get; set; } = true;
    public string? Comment { get; set; }
}

/// <summary>
/// Real-time update message
/// </summary>
public class PipelineUpdateMessage
{
    public string RequirementId { get; set; } = string.Empty;
    public string UpdateType { get; set; } = string.Empty;
    public PipelinePhase Phase { get; set; }
    public string Message { get; set; } = string.Empty;
    public object? Data { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

// Enums
public enum RequirementType
{
    Single,
    Multi
}

public enum RequirementStatus
{
    NotStarted,
    Planned,
    Approved,
    InProgress,
    Completed,
    Failed
}

public enum TaskStatus
{
    Pending,
    InProgress,
    Completed,
    Failed
}

public enum PipelinePhase
{
    None,
    Planning,
    Coding,
    Debugging,
    Testing,
    Reviewing,
    Completed
}

public enum PhaseState
{
    Pending,
    WaitingApproval,
    Running,
    Completed,
    Failed,
    Skipped
}
