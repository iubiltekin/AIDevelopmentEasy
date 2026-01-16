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
    public string? CodebaseId { get; set; }
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
    public List<string> DependsOnProjects { get; set; } = new();
    public int ProjectOrder { get; set; } = 0;
    public TaskStatus Status { get; set; }

    /// <summary>
    /// Existing classes/interfaces from the codebase to use or extend.
    /// Helps CoderAgent understand context when generating code.
    /// </summary>
    public List<string> UsesExisting { get; set; } = new();

    /// <summary>
    /// Whether this task modifies an existing file (true) or creates a new file (false).
    /// When true, the Coder should read the existing file and output the complete modified version.
    /// </summary>
    public bool IsModification { get; set; } = false;

    /// <summary>
    /// Full path to the target file in the codebase.
    /// Used for reading existing file content when IsModification is true.
    /// </summary>
    public string? FullPath { get; set; }

    /// <summary>
    /// Target namespace for the generated code.
    /// Helps ensure correct namespace declaration.
    /// </summary>
    public string? Namespace { get; set; }
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

// ═══════════════════════════════════════════════════════════════════════════════
// Codebase DTOs
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Codebase information for API responses
/// </summary>
public class CodebaseDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public CodebaseStatus Status { get; set; }
    public DateTime? AnalyzedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public CodebaseSummaryDto? Summary { get; set; }
}

/// <summary>
/// Summary of codebase analysis
/// </summary>
public class CodebaseSummaryDto
{
    public int TotalSolutions { get; set; }
    public int TotalProjects { get; set; }
    public int TotalClasses { get; set; }
    public int TotalInterfaces { get; set; }
    public string PrimaryFramework { get; set; } = string.Empty;
    public List<string> DetectedPatterns { get; set; } = new();
    public List<string> KeyNamespaces { get; set; } = new();
}

/// <summary>
/// Request to register a new codebase
/// </summary>
public class CreateCodebaseRequest
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
}

/// <summary>
/// Request to create a requirement with optional codebase
/// </summary>
public class CreateRequirementRequest
{
    public string Name { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public RequirementType Type { get; set; }
    public string? CodebaseId { get; set; }
}

/// <summary>
/// Codebase status
/// </summary>
public enum CodebaseStatus
{
    Pending,
    Analyzing,
    Ready,
    Failed
}

// Enums
public enum RequirementType
{
    Single
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
    Analysis,    // CodeAnalysisAgent - Codebase analysis (optional)
    Planning,    // PlannerAgent - Task decomposition
    Coding,      // CoderAgent - Code generation/modification
    Debugging,   // DebuggerAgent - Testing and fixing
    Reviewing,   // ReviewerAgent - Quality review
    Deployment,  // DeploymentAgent - Deploy to codebase and build
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
