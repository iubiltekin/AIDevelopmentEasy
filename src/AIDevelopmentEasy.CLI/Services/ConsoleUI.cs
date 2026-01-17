using AIDevelopmentEasy.CLI.Models;
using AIDevelopmentEasy.CLI.Services.Interfaces;

namespace AIDevelopmentEasy.CLI.Services;

/// <summary>
/// Console UI implementation for displaying information and prompts
/// </summary>
public class ConsoleUI : IConsoleUI
{
    private const string Separator = "═══════════════════════════════════════════════════════════════";
    private const string ThinSeparator = "───────────────────────────────────────────────────────────────";

    public void ShowBanner()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(@"
    ╔═══════════════════════════════════════════════════════════════════════════╗
    ║                                                                           ║
    ║      █████╗ ██╗    ██████╗ ███████╗██╗   ██╗███████╗██╗      ██████╗     ║
    ║     ██╔══██╗██║    ██╔══██╗██╔════╝██║   ██║██╔════╝██║     ██╔═══██╗    ║
    ║     ███████║██║    ██║  ██║█████╗  ██║   ██║█████╗  ██║     ██║   ██║    ║
    ║     ██╔══██║██║    ██║  ██║██╔══╝  ╚██╗ ██╔╝██╔══╝  ██║     ██║   ██║    ║
    ║     ██║  ██║██║    ██████╔╝███████╗ ╚████╔╝ ███████╗███████╗╚██████╔╝    ║
    ║     ╚═╝  ╚═╝╚═╝    ╚═════╝ ╚══════╝  ╚═══╝  ╚══════╝╚══════╝ ╚═════╝     ║
    ║                                                                           ║
    ║     ███████╗ █████╗ ███████╗██╗   ██╗                                    ║
    ║     ██╔════╝██╔══██╗██╔════╝╚██╗ ██╔╝                                    ║
    ║     █████╗  ███████║███████╗ ╚████╔╝                                     ║
    ║     ██╔══╝  ██╔══██║╚════██║  ╚██╔╝                                      ║
    ║     ███████╗██║  ██║███████║   ██║                                       ║
    ║     ╚══════╝╚═╝  ╚═╝╚══════╝   ╚═╝                                       ║
    ║                                                                           ║
    ║     Multi-Agent Software Development Framework                            ║
    ║                                                                           ║
    ╚═══════════════════════════════════════════════════════════════════════════╝
");
        Console.ResetColor();
    }

    public void ShowPaths(string codingStandardsPath, string storiesPath, string outputPath, string logsPath)
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        if (File.Exists(codingStandardsPath))
            Console.WriteLine($"  Coding Standards: {codingStandardsPath}");
        Console.WriteLine($"  Storys: {storiesPath}");
        Console.WriteLine($"  Output: {outputPath}");
        Console.WriteLine($"  Logs: {logsPath}");
        Console.ResetColor();
        Console.WriteLine();
    }

    public void Clear() => Console.Clear();

    #region Stories Menu

    public void ShowStoriesMenu(IReadOnlyList<StoryInfo> stories)
    {
        Console.WriteLine();
        Console.WriteLine(Separator);
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine("                    📋 REQUIREMENTS                              ");
        Console.ResetColor();
        Console.WriteLine(Separator);
        Console.WriteLine();

        for (int i = 0; i < stories.Count; i++)
        {
            var req = stories[i];

            Console.ForegroundColor = req.Status.ToColor();
            Console.Write($"  [{i + 1}] ");
            Console.ResetColor();

            Console.ForegroundColor = ConsoleColor.White;
            Console.Write($"{req.FileName,-40}");
            Console.ResetColor();

            Console.ForegroundColor = req.Status.ToColor();
            Console.WriteLine(req.Status.ToDisplayString());
            Console.ResetColor();
        }

        Console.WriteLine();
        Console.WriteLine(ThinSeparator);
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("  [0] Exit  |  [R] Refresh  |  [number] Select story");
        Console.ResetColor();
        Console.WriteLine();
    }

    public StoryInfo? SelectStory(IReadOnlyList<StoryInfo> stories)
    {
        while (true)
        {
            Console.Write("  Select: ");
            var input = Console.ReadLine()?.Trim().ToLower();

            if (string.IsNullOrEmpty(input) || input == "0" || input == "exit" || input == "q")
                return null;

            if (input == "r" || input == "refresh")
            {
                foreach (var req in stories)
                    req.RefreshStatus();
                ShowStoriesMenu(stories);
                continue;
            }

            if (int.TryParse(input, out var index) && index >= 1 && index <= stories.Count)
            {
                return stories[index - 1];
            }

            ShowWarning("  Invalid selection. Please try again.");
        }
    }

    #endregion

    #region Phase Display

    public void ShowPhase(string phaseName, int? phaseNumber = null)
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Yellow;
        if (phaseNumber.HasValue)
        {
            Console.WriteLine($"┌{'─'.ToString().PadRight(61, '─')}┐");
            Console.WriteLine($"│  📌 PHASE {phaseNumber}: {phaseName.PadRight(47)}│");
            Console.WriteLine($"└{'─'.ToString().PadRight(61, '─')}┘");
        }
        else
        {
            Console.WriteLine($"  ▶ {phaseName}");
        }
        Console.ResetColor();
    }

    public void ShowPhaseComplete(string phaseName)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"  ✓ {phaseName} completed");
        Console.ResetColor();
    }

    #endregion

    #region Status Messages

    public void ShowSuccess(string message)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(message);
        Console.ResetColor();
    }

    public void ShowError(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine(message);
        Console.ResetColor();
    }

    public void ShowWarning(string message)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine(message);
        Console.ResetColor();
    }

    public void ShowInfo(string message)
    {
        Console.WriteLine(message);
    }

    public void ShowProgress(string message)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"  ⏳ {message}");
        Console.ResetColor();
    }

    #endregion

    #region Section Display

    public void ShowSectionHeader(string title)
    {
        Console.WriteLine();
        Console.WriteLine(Separator);
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine($"  {title}");
        Console.ResetColor();
        Console.WriteLine(Separator);
    }

    public void ShowSubSection(string title)
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"  {title}:");
        Console.ResetColor();
        Console.WriteLine(ThinSeparator);
    }

    #endregion

    #region Task Display

    public void ShowTask(int index, string title, IEnumerable<string>? files = null)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"\n    Task {index}: {title}");
        Console.ResetColor();
        if (files != null)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"      Files: {string.Join(", ", files)}");
            Console.ResetColor();
        }
    }

    public void ShowTaskProgress(int index, string title)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write($"    [{index}] {title} ");
        Console.ResetColor();
    }

    public void ShowTaskResult(bool success, string? message = null)
    {
        if (success)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(message ?? "✓");
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(message ?? "✗");
        }
        Console.ResetColor();
    }

    #endregion

    public void ShowGeneratedTasks(IEnumerable<(int Index, string Title, IEnumerable<string> Files)> tasks)
    {
        ShowSubSection("Generated Tasks");
        foreach (var task in tasks)
        {
            ShowTask(task.Index, task.Title, task.Files);
        }
    }

    #region Approvals

    public bool ConfirmPlanApproval(string tasksPath)
    {
        Console.WriteLine();
        ShowWarning($"  📁 Tasks saved to: {tasksPath}");
        ShowInfo("     You can edit the task files before approving.");
        Console.WriteLine();
        return Confirm("  Approve plan and continue to coding?", true);
    }

    public bool ConfirmCodingStart()
    {
        Console.WriteLine();
        return Confirm("  Start code generation?", true);
    }

    public bool ConfirmDebugStart()
    {
        Console.WriteLine();
        ShowInfo("  Code generation completed. Ready for compilation check.");
        return Confirm("  Run debugger/compiler check?", true);
    }

    public bool ConfirmTestStart()
    {
        Console.WriteLine();
        return Confirm("  Run unit tests?", true);
    }

    public bool ConfirmReviewStart()
    {
        Console.WriteLine();
        return Confirm("  Run code review?", true);
    }

    #endregion

    #region Results Display

    public void ShowCompilationResult(bool success, string? output = null)
    {
        Console.WriteLine();
        if (success)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("  ┌─────────────────────────────────────────┐");
            Console.WriteLine("  │  ✅ COMPILATION SUCCESSFUL              │");
            Console.WriteLine("  └─────────────────────────────────────────┘");
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("  ┌─────────────────────────────────────────┐");
            Console.WriteLine("  │  ❌ COMPILATION FAILED                  │");
            Console.WriteLine("  └─────────────────────────────────────────┘");
        }
        Console.ResetColor();

        if (!string.IsNullOrEmpty(output))
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine();
            Console.WriteLine(output);
            Console.ResetColor();
        }
    }

    public void ShowTestResults(bool success, int passed, int failed, string? details = null)
    {
        Console.WriteLine();
        Console.ForegroundColor = success ? ConsoleColor.Green : ConsoleColor.Red;
        Console.WriteLine("  ┌─────────────────────────────────────────┐");
        Console.WriteLine($"  │  🧪 TEST RESULTS                        │");
        Console.WriteLine("  ├─────────────────────────────────────────┤");
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"  │  ✓ Passed: {passed,-28}│");
        Console.ForegroundColor = failed > 0 ? ConsoleColor.Red : ConsoleColor.DarkGray;
        Console.WriteLine($"  │  ✗ Failed: {failed,-28}│");
        Console.ForegroundColor = success ? ConsoleColor.Green : ConsoleColor.Red;
        Console.WriteLine("  └─────────────────────────────────────────┘");
        Console.ResetColor();

        if (!string.IsNullOrEmpty(details))
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine();
            Console.WriteLine(details);
            Console.ResetColor();
        }
    }

    public void ShowReviewResults(string verdict, bool approved, string? report = null)
    {
        Console.WriteLine();
        Console.ForegroundColor = approved ? ConsoleColor.Green : ConsoleColor.Yellow;
        Console.WriteLine("  ┌─────────────────────────────────────────┐");
        Console.WriteLine($"  │  📝 CODE REVIEW                         │");
        Console.WriteLine("  ├─────────────────────────────────────────┤");
        Console.WriteLine($"  │  Verdict: {verdict,-29}│");
        Console.WriteLine($"  │  Approved: {(approved ? "Yes ✓" : "No ✗"),-28}│");
        Console.WriteLine("  └─────────────────────────────────────────┘");
        Console.ResetColor();

        if (!string.IsNullOrEmpty(report))
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.DarkGray;
            var lines = report.Split('\n').Take(10);
            foreach (var line in lines)
                Console.WriteLine($"  {line}");
            if (report.Split('\n').Length > 10)
                Console.WriteLine("  ... (see full report in output folder)");
            Console.ResetColor();
        }
    }

    public void ShowFinalSummary(string outputPath, int fileCount)
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("  ╔═════════════════════════════════════════════════════════════╗");
        Console.WriteLine("  ║                    ✅ PROCESSING COMPLETE                   ║");
        Console.WriteLine("  ╠═════════════════════════════════════════════════════════════╣");
        Console.WriteLine($"  ║  📁 Output: {outputPath.PadRight(46)}║");
        Console.WriteLine($"  ║  📄 Files:  {fileCount.ToString().PadRight(46)}║");
        Console.WriteLine("  ╚═════════════════════════════════════════════════════════════╝");
        Console.ResetColor();
    }

    #endregion

    #region General Prompts

    public string? Prompt(string message)
    {
        Console.Write(message);
        return Console.ReadLine()?.Trim();
    }

    public bool Confirm(string message, bool defaultValue = true)
    {
        var defaultHint = defaultValue ? "[Y/n]" : "[y/N]";
        Console.Write($"{message} {defaultHint}: ");
        var input = Console.ReadLine()?.Trim().ToLower();

        if (string.IsNullOrEmpty(input))
            return defaultValue;

        return input == "y" || input == "yes";
    }

    public void PressAnyKeyToContinue(string message = "Press any key to continue...")
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write($"  {message}");
        Console.ResetColor();
        Console.ReadKey(true);
        Console.WriteLine();
    }

    public void ShowCompleted()
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(Separator);
        Console.WriteLine("                    ✅ ALL COMPLETED                            ");
        Console.WriteLine(Separator);
        Console.ResetColor();
    }

    public void ShowNoStoriesFound(string storiesPath)
    {
        Console.WriteLine();
        ShowWarning("  ⚠️ No story files found!");
        Console.WriteLine();
        Console.WriteLine($"  Add files to: {storiesPath}");
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("    Supported formats: *.txt, *.md, *.json");
        Console.ResetColor();
    }

    #endregion
}
