namespace AIDevelopmentEasy.CLI.Models;

/// <summary>
/// Requirement file information with processing status
/// </summary>
public class RequirementInfo
{
    public string FilePath { get; init; } = string.Empty;
    public string FileName => Path.GetFileName(FilePath);
    public string Name => Path.GetFileNameWithoutExtension(FilePath);
    public RequirementType Type { get; init; }
    public ProcessingStatus Status { get; private set; } = ProcessingStatus.NotStarted;
    public string WorkingDirectory { get; init; } = string.Empty;
    
    /// <summary>
    /// Check and update status based on existing files
    /// </summary>
    public void RefreshStatus()
    {
        var approvedFile = Path.Combine(WorkingDirectory, "_approved.json");
        var completedFile = Path.Combine(WorkingDirectory, "_completed.json");
        var tasksDir = Type == RequirementType.Single 
            ? Path.Combine(WorkingDirectory, "tasks")
            : Path.Combine(WorkingDirectory, "projects");

        if (File.Exists(completedFile))
        {
            Status = ProcessingStatus.Completed;
        }
        else if (File.Exists(approvedFile))
        {
            Status = ProcessingStatus.Approved;
        }
        else if (Directory.Exists(tasksDir) && Directory.GetFiles(tasksDir, "*.json", SearchOption.AllDirectories).Any())
        {
            Status = ProcessingStatus.Planned;
        }
        else
        {
            Status = ProcessingStatus.NotStarted;
        }
    }

    public void MarkAsPlanned() => Status = ProcessingStatus.Planned;
    public void MarkAsApproved() => Status = ProcessingStatus.Approved;
    public void MarkAsInProgress() => Status = ProcessingStatus.InProgress;
    public void MarkAsCompleted() => Status = ProcessingStatus.Completed;
}

public enum RequirementType
{
    Single,
    Multi
}

public enum ProcessingStatus
{
    NotStarted,
    Planned,
    Approved,
    InProgress,
    Completed
}

public static class ProcessingStatusExtensions
{
    public static string ToDisplayString(this ProcessingStatus status) => status switch
    {
        ProcessingStatus.NotStarted => "⬜ Not Started",
        ProcessingStatus.Planned => "📋 Planned",
        ProcessingStatus.Approved => "✅ Approved",
        ProcessingStatus.InProgress => "🔄 In Progress",
        ProcessingStatus.Completed => "✔️ Completed",
        _ => "❓ Unknown"
    };

    public static ConsoleColor ToColor(this ProcessingStatus status) => status switch
    {
        ProcessingStatus.NotStarted => ConsoleColor.Gray,
        ProcessingStatus.Planned => ConsoleColor.Yellow,
        ProcessingStatus.Approved => ConsoleColor.Cyan,
        ProcessingStatus.InProgress => ConsoleColor.Blue,
        ProcessingStatus.Completed => ConsoleColor.Green,
        _ => ConsoleColor.White
    };
}
