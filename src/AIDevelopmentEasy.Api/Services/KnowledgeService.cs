using System.Text.RegularExpressions;
using AIDevelopmentEasy.Api.Repositories.Interfaces;
using AIDevelopmentEasy.Api.Services.Interfaces;
using AIDevelopmentEasy.Core.Models;

namespace AIDevelopmentEasy.Api.Services;

/// <summary>
/// Knowledge Base service implementation.
/// Provides high-level operations for capturing and utilizing knowledge
/// from the pipeline execution.
/// </summary>
public class KnowledgeService : IKnowledgeService
{
    private readonly IKnowledgeRepository _knowledgeRepository;
    private readonly ILogger<KnowledgeService> _logger;

    public KnowledgeService(
        IKnowledgeRepository knowledgeRepository,
        ILogger<KnowledgeService> logger)
    {
        _knowledgeRepository = knowledgeRepository;
        _logger = logger;
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // Pattern Capture
    // ═══════════════════════════════════════════════════════════════════════════════

    public async Task CapturePatternFromStoryAsync(
        string storyId,
        string taskTitle,
        string taskDescription,
        string solutionCode,
        List<string>? tags = null,
        CancellationToken cancellationToken = default)
    {
        // Skip if code is too short (likely not a meaningful pattern)
        if (string.IsNullOrWhiteSpace(solutionCode) || solutionCode.Length < 100)
        {
            _logger.LogDebug("[Knowledge] Skipping pattern capture - code too short");
            return;
        }

        // Detect subcategory from task title/description
        var subcategory = DetectPatternSubcategory(taskTitle, taskDescription, solutionCode);

        // Auto-generate tags if not provided
        tags ??= ExtractTags(taskTitle, taskDescription, solutionCode);

        var request = new CapturePatternRequest
        {
            Title = taskTitle,
            ProblemDescription = taskDescription,
            SolutionCode = solutionCode,
            Subcategory = subcategory,
            Tags = tags,
            Language = DetectLanguage(solutionCode),
            Context = $"Generated from story: {storyId}",
            ApplicableScenarios = ExtractScenarios(taskDescription),
            SourceStoryId = storyId
        };

        await _knowledgeRepository.CreatePatternAsync(request, cancellationToken);

        _logger.LogInformation("[Knowledge] Captured pattern from story {StoryId}: {Title}",
            storyId, taskTitle);
    }

    public async Task CaptureFromCompletedPipelineAsync(
        string storyId,
        Dictionary<string, string> generatedFiles,
        CancellationToken cancellationToken = default)
    {
        if (generatedFiles == null || !generatedFiles.Any())
        {
            _logger.LogDebug("[Knowledge] No files to capture from pipeline");
            return;
        }

        var capturedCount = 0;

        foreach (var (filePath, content) in generatedFiles)
        {
            // Skip test files and very short files
            if (IsTestFile(filePath) || content.Length < 200)
                continue;

            // Extract meaningful patterns from the file
            var patterns = ExtractPatternsFromCode(filePath, content);

            foreach (var pattern in patterns)
            {
                var request = new CapturePatternRequest
                {
                    Title = pattern.Title,
                    ProblemDescription = pattern.Description,
                    SolutionCode = pattern.Code,
                    Subcategory = pattern.Subcategory,
                    Tags = pattern.Tags,
                    Language = DetectLanguage(content),
                    Context = $"Extracted from {filePath} in story {storyId}",
                    SourceStoryId = storyId
                };

                await _knowledgeRepository.CreatePatternAsync(request, cancellationToken);
                capturedCount++;
            }
        }

        _logger.LogInformation("[Knowledge] Captured {Count} patterns from pipeline {StoryId}",
            capturedCount, storyId);
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // Error Capture
    // ═══════════════════════════════════════════════════════════════════════════════

    public async Task CaptureErrorFixAsync(
        string storyId,
        string errorMessage,
        ErrorType errorType,
        string fixDescription,
        string? fixCode = null,
        CancellationToken cancellationToken = default)
    {
        // Check if this error already exists
        var existingMatch = await _knowledgeRepository.FindMatchingErrorAsync(
            errorMessage, errorType, cancellationToken);

        if (existingMatch.Found && existingMatch.MatchScore > 0.9)
        {
            // Increment occurrence count for existing error
            await _knowledgeRepository.IncrementErrorOccurrenceAsync(
                existingMatch.Error!.Id, cancellationToken);

            _logger.LogDebug("[Knowledge] Incremented occurrence for existing error: {Id}",
                existingMatch.Error.Id);
            return;
        }

        // Create new error entry
        var request = new CaptureErrorRequest
        {
            Title = GenerateErrorTitle(errorMessage, errorType),
            ErrorType = errorType,
            ErrorPattern = GenerateErrorPattern(errorMessage),
            ErrorMessage = errorMessage,
            RootCause = ExtractRootCause(errorMessage),
            FixDescription = fixDescription,
            FixCode = fixCode,
            Tags = ExtractErrorTags(errorMessage, errorType),
            Language = "csharp",
            PreventionTips = GeneratePreventionTips(errorType, errorMessage),
            SourceStoryId = storyId
        };

        await _knowledgeRepository.CreateErrorAsync(request, cancellationToken);

        _logger.LogInformation("[Knowledge] Captured error fix from story {StoryId}: {Title}",
            storyId, request.Title);
    }

    public async Task CaptureTestFailureFixAsync(
        string storyId,
        string testName,
        string errorMessage,
        string fixDescription,
        string? fixCode = null,
        CancellationToken cancellationToken = default)
    {
        await CaptureErrorFixAsync(
            storyId,
            $"Test '{testName}' failed: {errorMessage}",
            ErrorType.TestFailure,
            fixDescription,
            fixCode,
            cancellationToken);
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // Knowledge Utilization
    // ═══════════════════════════════════════════════════════════════════════════════

    public async Task<List<SuccessfulPattern>> FindRelevantPatternsAsync(
        string taskDescription,
        int limit = 3,
        CancellationToken cancellationToken = default)
    {
        var result = await _knowledgeRepository.FindSimilarPatternsAsync(
            taskDescription, limit, cancellationToken);

        // Only return patterns with reasonable relevance
        return result.Patterns
            .Where(p => result.RelevanceScores.TryGetValue(p.Id, out var score) && score > 0.2)
            .ToList();
    }

    public async Task<CommonError?> FindKnownErrorFixAsync(
        string errorMessage,
        ErrorType? errorType = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _knowledgeRepository.FindMatchingErrorAsync(
            errorMessage, errorType, cancellationToken);

        if (result.Found && result.MatchScore > 0.5)
        {
            // Record that we're using this knowledge
            await _knowledgeRepository.RecordUsageAsync(result.Error!.Id, cancellationToken);
            return result.Error;
        }

        return null;
    }

    public async Task<string?> GenerateKnowledgeContextAsync(
        string taskDescription,
        int maxPatterns = 3,
        CancellationToken cancellationToken = default)
    {
        var patterns = await FindRelevantPatternsAsync(taskDescription, maxPatterns, cancellationToken);

        if (!patterns.Any())
            return null;

        var context = "## Relevant Patterns from Knowledge Base\n\n";
        context += "The following patterns from previous successful implementations may be helpful:\n\n";

        foreach (var pattern in patterns)
        {
            context += $"### {pattern.Title}\n";
            context += $"**Problem:** {pattern.ProblemDescription}\n";

            if (pattern.ApplicableScenarios.Any())
            {
                context += $"**Applicable to:** {string.Join(", ", pattern.ApplicableScenarios)}\n";
            }

            context += $"\n```{pattern.Language}\n{TruncateCode(pattern.SolutionCode, 500)}\n```\n\n";
        }

        return context;
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // Verification
    // ═══════════════════════════════════════════════════════════════════════════════

    public async Task VerifyStoryKnowledgeAsync(string storyId, CancellationToken cancellationToken = default)
    {
        var allEntries = await _knowledgeRepository.GetAllAsync(cancellationToken);
        var storyEntries = allEntries.Where(e => e.SourceStoryId == storyId).ToList();

        foreach (var entry in storyEntries)
        {
            await _knowledgeRepository.MarkAsVerifiedAsync(entry.Id, cancellationToken);
        }

        _logger.LogInformation("[Knowledge] Verified {Count} entries from story {StoryId}",
            storyEntries.Count, storyId);
    }

    public async Task RecordKnowledgeUsageAsync(
        string knowledgeId,
        bool wasSuccessful,
        CancellationToken cancellationToken = default)
    {
        await _knowledgeRepository.RecordUsageAsync(knowledgeId, cancellationToken);
        await _knowledgeRepository.UpdateSuccessRateAsync(knowledgeId, wasSuccessful, cancellationToken);
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // Helper Methods
    // ═══════════════════════════════════════════════════════════════════════════════

    private static PatternSubcategory DetectPatternSubcategory(string title, string description, string code)
    {
        var combined = $"{title} {description} {code}".ToLower();

        if (combined.Contains("log") || combined.Contains("serilog") || combined.Contains("ilogger"))
            return PatternSubcategory.Logging;

        if (combined.Contains("repository") || combined.Contains("dataaccess") || combined.Contains("dbcontext"))
            return PatternSubcategory.Repository;

        if (combined.Contains("valid") || combined.Contains("fluentvalidation"))
            return PatternSubcategory.Validation;

        if (combined.Contains("exception") || combined.Contains("error handling") || combined.Contains("try") || combined.Contains("catch"))
            return PatternSubcategory.ErrorHandling;

        if (combined.Contains("dependency injection") || combined.Contains("iservicecollection") || combined.Contains("addsingleton") || combined.Contains("addscoped"))
            return PatternSubcategory.DependencyInjection;

        if (combined.Contains("controller") || combined.Contains("api") || combined.Contains("endpoint") || combined.Contains("httpget"))
            return PatternSubcategory.ApiDesign;

        if (combined.Contains("test") || combined.Contains("nunit") || combined.Contains("xunit") || combined.Contains("assert"))
            return PatternSubcategory.Testing;

        if (combined.Contains("config") || combined.Contains("appsettings") || combined.Contains("ioptions"))
            return PatternSubcategory.Configuration;

        if (combined.Contains("file") || combined.Contains("stream") || combined.Contains("read") || combined.Contains("write"))
            return PatternSubcategory.FileIO;

        return PatternSubcategory.Other;
    }

    private static List<string> ExtractTags(string title, string description, string code)
    {
        var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Extract from title
        var titleWords = Regex.Split(title, @"[\s\-_]+")
            .Where(w => w.Length > 2)
            .Take(3);
        foreach (var word in titleWords)
            tags.Add(word.ToLower());

        // Detect common technologies/patterns
        var combined = $"{title} {description} {code}";

        var techPatterns = new Dictionary<string, string[]>
        {
            ["async"] = new[] { "async", "await", "Task<" },
            ["linq"] = new[] { ".Where(", ".Select(", ".OrderBy(" },
            ["di"] = new[] { "IServiceCollection", "AddSingleton", "AddScoped" },
            ["logging"] = new[] { "ILogger", "LogInformation", "LogError" },
            ["api"] = new[] { "[HttpGet]", "[HttpPost]", "Controller" },
            ["ef"] = new[] { "DbContext", "DbSet", "Entity Framework" },
            ["validation"] = new[] { "FluentValidation", "DataAnnotations", "Validate" }
        };

        foreach (var (tag, patterns) in techPatterns)
        {
            if (patterns.Any(p => combined.Contains(p, StringComparison.OrdinalIgnoreCase)))
                tags.Add(tag);
        }

        return tags.Take(10).ToList();
    }

    private static List<string> ExtractScenarios(string description)
    {
        var scenarios = new List<string>();

        // Extract bullet points or numbered items
        var matches = Regex.Matches(description, @"[-•*]\s*(.+?)(?=[-•*]|$)", RegexOptions.Multiline);
        foreach (Match match in matches)
        {
            var scenario = match.Groups[1].Value.Trim();
            if (scenario.Length > 10 && scenario.Length < 200)
                scenarios.Add(scenario);
        }

        return scenarios.Take(5).ToList();
    }

    private static string DetectLanguage(string code)
    {
        if (code.Contains("namespace ") || code.Contains("public class ") || code.Contains("using System"))
            return "csharp";
        if (code.Contains("import ") && code.Contains("from "))
            return "typescript";
        if (code.Contains("function ") || code.Contains("const ") || code.Contains("=>"))
            return "javascript";
        return "csharp";
    }

    private static bool IsTestFile(string filePath)
    {
        var fileName = Path.GetFileName(filePath).ToLower();
        return fileName.Contains("test") || fileName.Contains("spec") ||
               filePath.Contains("Tests", StringComparison.OrdinalIgnoreCase);
    }

    private static List<ExtractedPattern> ExtractPatternsFromCode(string filePath, string content)
    {
        var patterns = new List<ExtractedPattern>();

        // Extract classes with interfaces (dependency injection patterns)
        var classMatches = Regex.Matches(content,
            @"public\s+class\s+(\w+)\s*:\s*(I\w+)",
            RegexOptions.Multiline);

        foreach (Match match in classMatches)
        {
            var className = match.Groups[1].Value;
            var interfaceName = match.Groups[2].Value;

            // Extract the class body
            var classStart = match.Index;
            var classBody = ExtractClassBody(content, classStart);

            if (classBody.Length > 100)
            {
                patterns.Add(new ExtractedPattern
                {
                    Title = $"{className} implementing {interfaceName}",
                    Description = $"Implementation of {interfaceName} interface",
                    Code = classBody,
                    Subcategory = DetectPatternSubcategory(className, interfaceName, classBody),
                    Tags = new List<string> { className.ToLower(), interfaceName.ToLower(), "interface-implementation" }
                });
            }
        }

        return patterns.Take(3).ToList(); // Limit to 3 patterns per file
    }

    private static string ExtractClassBody(string content, int startIndex)
    {
        var braceCount = 0;
        var started = false;
        var endIndex = startIndex;

        for (var i = startIndex; i < content.Length && i < startIndex + 5000; i++)
        {
            if (content[i] == '{')
            {
                braceCount++;
                started = true;
            }
            else if (content[i] == '}')
            {
                braceCount--;
                if (started && braceCount == 0)
                {
                    endIndex = i + 1;
                    break;
                }
            }
        }

        return content.Substring(startIndex, Math.Min(endIndex - startIndex, 2000));
    }

    private static string GenerateErrorTitle(string errorMessage, ErrorType errorType)
    {
        var prefix = errorType switch
        {
            ErrorType.Compilation => "Build Error",
            ErrorType.TestFailure => "Test Failure",
            ErrorType.Runtime => "Runtime Error",
            _ => "Error"
        };

        // Extract key part of error message
        var shortMessage = errorMessage.Length > 60
            ? errorMessage.Substring(0, 60) + "..."
            : errorMessage;

        // Remove file paths and line numbers
        shortMessage = Regex.Replace(shortMessage, @"[A-Za-z]:\\[^\s]+", "[path]");
        shortMessage = Regex.Replace(shortMessage, @"\(\d+,\d+\)", "");

        return $"{prefix}: {shortMessage.Trim()}";
    }

    private static string GenerateErrorPattern(string errorMessage)
    {
        // Create a regex pattern from the error message
        var pattern = Regex.Escape(errorMessage);

        // Replace specific values with wildcards
        pattern = Regex.Replace(pattern, @"'[^']+?'", "'[^']+'"); // Quoted strings
        pattern = Regex.Replace(pattern, @"\d+", @"\d+"); // Numbers
        pattern = Regex.Replace(pattern, @"[A-Za-z]:\\\S+", @"[^\s]+"); // File paths

        return pattern;
    }

    private static string ExtractRootCause(string errorMessage)
    {
        // Common root cause patterns
        if (errorMessage.Contains("not found", StringComparison.OrdinalIgnoreCase))
            return "Missing reference or type";
        if (errorMessage.Contains("cannot convert", StringComparison.OrdinalIgnoreCase))
            return "Type mismatch";
        if (errorMessage.Contains("ambiguous", StringComparison.OrdinalIgnoreCase))
            return "Ambiguous reference";
        if (errorMessage.Contains("null", StringComparison.OrdinalIgnoreCase))
            return "Null reference issue";
        if (errorMessage.Contains("does not contain", StringComparison.OrdinalIgnoreCase))
            return "Missing member";

        return "See error message for details";
    }

    private static List<string> ExtractErrorTags(string errorMessage, ErrorType errorType)
    {
        var tags = new List<string> { errorType.ToString().ToLower() };

        if (errorMessage.Contains("CS", StringComparison.OrdinalIgnoreCase))
        {
            var match = Regex.Match(errorMessage, @"CS\d{4}");
            if (match.Success)
                tags.Add(match.Value.ToLower());
        }

        if (errorMessage.Contains("NullReference"))
            tags.Add("null-reference");
        if (errorMessage.Contains("FileNotFound"))
            tags.Add("file-not-found");
        if (errorMessage.Contains("Assert"))
            tags.Add("assertion");

        return tags;
    }

    private static List<string> GeneratePreventionTips(ErrorType errorType, string errorMessage)
    {
        var tips = new List<string>();

        switch (errorType)
        {
            case ErrorType.Compilation:
                tips.Add("Ensure all required using statements are present");
                tips.Add("Check for typos in type names");
                break;
            case ErrorType.TestFailure:
                tips.Add("Review test assertions and expected values");
                tips.Add("Ensure test setup/teardown is correct");
                break;
            case ErrorType.Runtime:
                tips.Add("Add null checks for potential null references");
                tips.Add("Validate input parameters");
                break;
        }

        return tips;
    }

    private static string TruncateCode(string code, int maxLength)
    {
        if (code.Length <= maxLength)
            return code;

        return code.Substring(0, maxLength) + "\n// ... (truncated)";
    }

    private class ExtractedPattern
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public PatternSubcategory Subcategory { get; set; }
        public List<string> Tags { get; set; } = new();
    }
}
