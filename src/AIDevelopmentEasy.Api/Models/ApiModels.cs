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
    
    /// <summary>
    /// Retry information when a phase needs to be retried
    /// </summary>
    public RetryInfoDto? RetryInfo { get; set; }
    
    /// <summary>
    /// Target phase to return to after retry approval
    /// </summary>
    public PipelinePhase? RetryTargetPhase { get; set; }
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
    Analysis,           // CodeAnalysisAgent - Codebase analysis (optional)
    Planning,           // PlannerAgent - Task decomposition
    Coding,             // CoderAgent - Code generation/modification
    Debugging,          // DebuggerAgent - Testing and fixing
    Reviewing,          // ReviewerAgent - Quality review
    Deployment,         // DeploymentAgent - Deploy to codebase and build
    UnitTesting, // Run new/modified tests in deployed codebase
    PullRequest,        // Create GitHub PR (after tests pass)
    Completed
}

public enum PhaseState
{
    Pending,
    WaitingApproval,
    Running,
    Completed,
    Failed,
    Skipped,
    WaitingRetryApproval  // Waiting for user to approve auto-fix retry
}

// ═══════════════════════════════════════════════════════════════════════════════
// Retry and Fix Task DTOs
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// A fix task generated from build/test failures
/// </summary>
public class FixTaskDto
{
    public int Index { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string TargetFile { get; set; } = string.Empty;
    public FixTaskType Type { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public string? ErrorLocation { get; set; }
    public string? StackTrace { get; set; }
    public string? SuggestedFix { get; set; }
    /// <summary>
    /// Project name where the fix should be applied (extracted from generated files)
    /// </summary>
    public string? ProjectName { get; set; }
    /// <summary>
    /// Namespace of the file to be fixed
    /// </summary>
    public string? Namespace { get; set; }
    /// <summary>
    /// Full path to the file in the codebase (for modification mode)
    /// </summary>
    public string? FullPath { get; set; }
}

/// <summary>
/// Type of fix task
/// </summary>
public enum FixTaskType
{
    BuildError,       // Compilation error
    TestFailure,      // Unit test failure
    IntegrationError  // Integration/dependency issue
}

/// <summary>
/// Test execution result for a single test
/// </summary>
public class TestResultDto
{
    public string TestName { get; set; } = string.Empty;
    public string ClassName { get; set; } = string.Empty;
    public string? FilePath { get; set; }
    public bool Passed { get; set; }
    public string? ErrorMessage { get; set; }
    public string? StackTrace { get; set; }
    public TimeSpan Duration { get; set; }
    public bool IsNewTest { get; set; }  // true = newly added test
}

/// <summary>
/// Summary of test execution
/// </summary>
public class TestSummaryDto
{
    public int TotalTests { get; set; }
    public int Passed { get; set; }
    public int Failed { get; set; }
    public int Skipped { get; set; }
    public int NewTestsPassed { get; set; }
    public int NewTestsFailed { get; set; }
    public int ExistingTestsFailed { get; set; }  // CRITICAL: Breaking changes!
    public TimeSpan TotalDuration { get; set; }
    public bool IsBreakingChange => ExistingTestsFailed > 0;
    public List<TestResultDto> FailedTests { get; set; } = new();
}

/// <summary>
/// Retry context information
/// </summary>
public class RetryInfoDto
{
    public int CurrentAttempt { get; set; }
    public int MaxAttempts { get; set; }
    public RetryReason Reason { get; set; }
    public List<FixTaskDto> FixTasks { get; set; } = new();
    public string? LastError { get; set; }
    public TestSummaryDto? TestSummary { get; set; }
    public DateTime? LastAttemptAt { get; set; }
}

/// <summary>
/// Reason for retry
/// </summary>
public enum RetryReason
{
    BuildFailed,
    TestsFailed,
    IntegrationFailed
}

/// <summary>
/// Request to approve retry with fix tasks
/// </summary>
public class ApproveRetryRequest
{
    public bool Approved { get; set; } = true;
    public RetryAction Action { get; set; } = RetryAction.AutoFix;
    public string? Comment { get; set; }
}

/// <summary>
/// Action to take on retry
/// </summary>
public enum RetryAction
{
    AutoFix,       // Let CoderAgent auto-fix
    ManualFix,     // User will fix manually
    SkipTests,     // Skip failed tests and continue
    Abort          // Abort the pipeline
}
