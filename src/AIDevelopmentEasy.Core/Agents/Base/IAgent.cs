namespace AIDevelopmentEasy.Core.Agents.Base;

/// <summary>
/// Base interface for all AIDevelopmentEasy agents.
/// Each agent has a specific role in the software development pipeline.
/// </summary>
public interface IAgent
{
    /// <summary>
    /// Agent's unique name/role identifier
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// Agent's role description
    /// </summary>
    string Role { get; }
    
    /// <summary>
    /// Execute the agent's task with given input
    /// </summary>
    Task<AgentResponse> RunAsync(AgentRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Request payload for agent execution
/// </summary>
public record AgentRequest
{
    /// <summary>
    /// The main input/task for the agent
    /// </summary>
    public string Input { get; init; } = string.Empty;
    
    /// <summary>
    /// Additional context (e.g., existing code, plan, error messages)
    /// </summary>
    public Dictionary<string, string> Context { get; init; } = new();
    
    /// <summary>
    /// Reference to shared project state
    /// </summary>
    public ProjectState? ProjectState { get; init; }
}

/// <summary>
/// Response from agent execution
/// </summary>
public record AgentResponse
{
    /// <summary>
    /// Whether the agent completed successfully
    /// </summary>
    public bool Success { get; init; }
    
    /// <summary>
    /// The agent's output (plan, code, fix, review report, etc.)
    /// </summary>
    public string Output { get; init; } = string.Empty;
    
    /// <summary>
    /// Structured data if applicable (e.g., list of tasks, code files)
    /// </summary>
    public Dictionary<string, object> Data { get; init; } = new();
    
    /// <summary>
    /// Error message if failed
    /// </summary>
    public string? Error { get; init; }
    
    /// <summary>
    /// Tokens used in this request
    /// </summary>
    public int TokensUsed { get; init; }
}

/// <summary>
/// Shared project state - the "blackboard" for agent communication.
/// Agents communicate through artifacts rather than direct messaging.
/// </summary>
public class ProjectState
{
    /// <summary>
    /// Project name/identifier
    /// </summary>
    public string ProjectName { get; set; } = string.Empty;
    
    /// <summary>
    /// Original user requirement
    /// </summary>
    public string Requirement { get; set; } = string.Empty;
    
    /// <summary>
    /// Coding standards and conventions (JSON string)
    /// </summary>
    public string? CodingStandards { get; set; }
    
    /// <summary>
    /// Current plan/subtasks from Planner agent
    /// </summary>
    public List<SubTask> Plan { get; set; } = new();
    
    /// <summary>
    /// In-memory file system: filename -> code content
    /// </summary>
    public Dictionary<string, string> Codebase { get; set; } = new();
    
    /// <summary>
    /// Execution/test logs
    /// </summary>
    public List<ExecutionLog> ExecutionLogs { get; set; } = new();
    
    /// <summary>
    /// Final review report
    /// </summary>
    public string? ReviewReport { get; set; }
    
    /// <summary>
    /// Current phase in the pipeline
    /// </summary>
    public PipelinePhase CurrentPhase { get; set; } = PipelinePhase.Planning;
    
    /// <summary>
    /// Conversation/action history for debugging
    /// </summary>
    public List<AgentAction> History { get; set; } = new();
}

/// <summary>
/// A subtask in the development plan.
/// Enhanced with multi-project support for codebase-aware planning.
/// </summary>
public record SubTask
{
    public int Index { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public SubTaskStatus Status { get; set; } = SubTaskStatus.Pending;
    public string? GeneratedCode { get; set; }
    public List<string> TargetFiles { get; init; } = new();
    
    /// <summary>
    /// Target project name (for multi-project codebases).
    /// If null, uses the default/main project.
    /// </summary>
    public string? ProjectName { get; init; }
    
    /// <summary>
    /// Indices of tasks that must complete before this one.
    /// Used for dependency ordering.
    /// </summary>
    public List<int> DependsOn { get; init; } = new();
    
    /// <summary>
    /// Existing classes/interfaces from the codebase to use or extend.
    /// Helps CoderAgent understand context.
    /// </summary>
    public List<string> UsesExisting { get; init; } = new();
    
    /// <summary>
    /// Whether this task modifies an existing file (true) or creates a new file (false).
    /// When true, the Coder should read the existing file and output the complete modified version.
    /// </summary>
    public bool IsModification { get; init; } = false;
    
    /// <summary>
    /// Current content of the file to modify (when IsModification is true).
    /// This is populated before sending to the Coder.
    /// </summary>
    public string? CurrentContent { get; set; }
    
    /// <summary>
    /// Full path to the target file in the codebase.
    /// Used for reading and writing file content.
    /// </summary>
    public string? FullPath { get; init; }
    
    /// <summary>
    /// Target namespace for the generated code.
    /// Helps ensure correct namespace declaration.
    /// </summary>
    public string? Namespace { get; init; }
}

public enum SubTaskStatus
{
    Pending,
    InProgress,
    Coded,
    Debugged,
    Completed,
    Failed
}

public enum PipelinePhase
{
    Planning,
    Coding,
    Debugging,
    Reviewing,
    Completed
}

/// <summary>
/// Log entry for code execution
/// </summary>
public record ExecutionLog
{
    public DateTime Timestamp { get; init; } = DateTime.Now;
    public string Code { get; init; } = string.Empty;
    public bool Success { get; init; }
    public string? Output { get; init; }
    public string? Error { get; init; }
}

/// <summary>
/// Record of an agent action for history/debugging
/// </summary>
public record AgentAction
{
    public DateTime Timestamp { get; init; } = DateTime.Now;
    public string AgentName { get; init; } = string.Empty;
    public string Action { get; init; } = string.Empty;
    public string Input { get; init; } = string.Empty;
    public string Output { get; init; } = string.Empty;
}
