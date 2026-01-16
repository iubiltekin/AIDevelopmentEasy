using AIDevelopmentEasy.CLI.Models;

namespace AIDevelopmentEasy.CLI.Services.Interfaces;

/// <summary>
/// Console UI operations interface
/// </summary>
public interface IConsoleUI
{
    // Banner and basic display
    void ShowBanner();
    void ShowPaths(string codingStandardsPath, string requirementsPath, string outputPath, string logsPath);
    void Clear();
    
    // Requirements menu
    void ShowRequirementsMenu(IReadOnlyList<RequirementInfo> requirements);
    RequirementInfo? SelectRequirement(IReadOnlyList<RequirementInfo> requirements);
    
    // Phase display
    void ShowPhase(string phaseName, int? phaseNumber = null);
    void ShowPhaseComplete(string phaseName);
    
    // Status messages
    void ShowSuccess(string message);
    void ShowError(string message);
    void ShowWarning(string message);
    void ShowInfo(string message);
    void ShowProgress(string message);
    
    // Section display
    void ShowSectionHeader(string title);
    void ShowSubSection(string title);
    
    // Task display
    void ShowTask(int index, string title, IEnumerable<string>? files = null);
    void ShowTaskProgress(int index, string title);
    void ShowTaskResult(bool success, string? message = null);
    void ShowGeneratedTasks(IEnumerable<(int Index, string Title, IEnumerable<string> Files)> tasks);
    
    // Approvals - returns true if approved, false if skipped
    bool ConfirmPlanApproval(string tasksPath);
    bool ConfirmCodingStart();
    bool ConfirmDebugStart();
    bool ConfirmTestStart();
    bool ConfirmReviewStart();
    
    // Results display
    void ShowCompilationResult(bool success, string? output = null);
    void ShowTestResults(bool success, int passed, int failed, string? details = null);
    void ShowReviewResults(string verdict, bool approved, string? report = null);
    void ShowFinalSummary(string outputPath, int fileCount);
    
    // General prompts
    string? Prompt(string message);
    bool Confirm(string message, bool defaultValue = true);
    void PressAnyKeyToContinue(string message = "Press any key to continue...");
    
    void ShowCompleted();
    void ShowNoRequirementsFound(string requirementsPath);
}
