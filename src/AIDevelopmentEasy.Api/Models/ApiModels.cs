namespace AIDevelopmentEasy.Api.Models;

/// <summary>
/// Story information for API responses
/// </summary>
public class StoryDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public StoryType Type { get; set; }
    public StoryStatus Status { get; set; }
    public string? CodebaseId { get; set; }
    /// <summary>
    /// The requirement this story was created from (if any)
    /// </summary>
    public string? RequirementId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastProcessedAt { get; set; }
    public List<TaskDto> Tasks { get; set; } = new();

    // ═══════════════════════════════════════════════════════════════════
    // Target Info (Optional) - For bugfixes and modifications
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Target project name within the codebase (e.g., "AIDevelopmentEasy.Api")
    /// </summary>
    public string? TargetProject { get; set; }

    /// <summary>
    /// Target file path relative to codebase (e.g., "Services/UserService.cs")
    /// </summary>
    public string? TargetFile { get; set; }

    /// <summary>
    /// Target class name (e.g., "UserService")
    /// </summary>
    public string? TargetClass { get; set; }

    /// <summary>
    /// Target method name (e.g., "GetById")
    /// </summary>
    public string? TargetMethod { get; set; }

    /// <summary>
    /// Type of change: Create new, Modify existing, or Delete
    /// </summary>
    public ChangeType ChangeType { get; set; } = ChangeType.Create;

    // ═══════════════════════════════════════════════════════════════════
    // Test Target Info (Optional) - For unit test modifications
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Target test project name (e.g., "AIDevelopmentEasy.Tests")
    /// </summary>
    public string? TargetTestProject { get; set; }

    /// <summary>
    /// Target test file path (e.g., "Services/UserServiceTests.cs")
    /// </summary>
    public string? TargetTestFile { get; set; }

    /// <summary>
    /// Target test class name (e.g., "UserServiceTests")
    /// </summary>
    public string? TargetTestClass { get; set; }
}

/// <summary>
/// Task type indicating whether this is an original or fix task
/// </summary>
public enum TaskType
{
    /// <summary>Original task created during planning phase</summary>
    Original = 0,
    /// <summary>Fix task generated from test/build failures</summary>
    Fix = 1
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
    /// Task type: Original (from planning) or Fix (from failures)
    /// </summary>
    public TaskType Type { get; set; } = TaskType.Original;

    /// <summary>
    /// Retry attempt number this task belongs to (0 = initial, 1+ = retry)
    /// </summary>
    public int RetryAttempt { get; set; } = 0;

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

    /// <summary>
    /// Existing code content for fix tasks - contains the code that needs to be modified.
    /// This is populated from generatedFiles before rollback so it's available for LLM.
    /// </summary>
    public string? ExistingCode { get; set; }

    /// <summary>
    /// Target method name for focused modifications.
    /// When set, only this method should be modified (not the entire file).
    /// </summary>
    public string? TargetMethod { get; set; }

    /// <summary>
    /// Original file content for merge during deployment.
    /// Used when modifying a specific method - the method is merged back into this file.
    /// </summary>
    public string? CurrentContent { get; set; }
}

/// <summary>
/// Pipeline execution status
/// </summary>
public class PipelineStatusDto
{
    public string StoryId { get; set; } = string.Empty;
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
/// Request to process a story
/// </summary>
public class ProcessStoryRequest
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
    public string StoryId { get; set; } = string.Empty;
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
/// Request to create a story with optional codebase
/// </summary>
public class CreateStoryRequest
{
    public string Name { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public StoryType Type { get; set; }
    public string? CodebaseId { get; set; }
    /// <summary>
    /// The requirement this story was created from (if any)
    /// </summary>
    public string? RequirementId { get; set; }

    // Target Info (Optional)
    public string? TargetProject { get; set; }
    public string? TargetFile { get; set; }
    public string? TargetClass { get; set; }
    public string? TargetMethod { get; set; }
    public ChangeType ChangeType { get; set; } = ChangeType.Create;

    // Test Target Info (Optional)
    public string? TargetTestProject { get; set; }
    public string? TargetTestFile { get; set; }
    public string? TargetTestClass { get; set; }
}

/// <summary>
/// Request to update story target information before starting pipeline
/// </summary>
public class UpdateStoryTargetRequest
{
    // Code Target
    public string? TargetProject { get; set; }
    public string? TargetFile { get; set; }
    public string? TargetClass { get; set; }
    public string? TargetMethod { get; set; }
    public ChangeType ChangeType { get; set; } = ChangeType.Create;

    // Test Target
    public string? TargetTestProject { get; set; }
    public string? TargetTestFile { get; set; }
    public string? TargetTestClass { get; set; }
}

/// <summary>
/// Type of change for a story
/// </summary>
public enum ChangeType
{
    /// <summary>Create new file/class/method</summary>
    Create = 0,
    /// <summary>Modify existing file/class/method</summary>
    Modify = 1,
    /// <summary>Delete existing file/class/method</summary>
    Delete = 2
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
public enum StoryType
{
    Single
}

public enum StoryStatus
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
    /// <summary>
    /// Existing code content that needs to be modified.
    /// Captured from generatedFiles BEFORE rollback so it remains available for LLM.
    /// </summary>
    public string? ExistingCode { get; set; }
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
