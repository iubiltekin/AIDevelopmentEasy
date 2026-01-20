using System.Text.Json.Serialization;

namespace AIDevelopmentEasy.Core.Models;

/// <summary>
/// Knowledge Base entry category
/// </summary>
public enum KnowledgeCategory
{
    /// <summary>Successful code patterns that solved problems</summary>
    Pattern,

    /// <summary>Common errors and their fixes</summary>
    Error,

    /// <summary>Project templates and scaffolding</summary>
    Template,

    /// <summary>Agent performance and configuration insights</summary>
    AgentInsight
}

/// <summary>
/// Subcategory for patterns
/// </summary>
public enum PatternSubcategory
{
    /// <summary>Logging implementations</summary>
    Logging,

    /// <summary>Repository patterns</summary>
    Repository,

    /// <summary>Validation strategies</summary>
    Validation,

    /// <summary>Error handling patterns</summary>
    ErrorHandling,

    /// <summary>Dependency injection patterns</summary>
    DependencyInjection,

    /// <summary>API design patterns</summary>
    ApiDesign,

    /// <summary>Testing patterns</summary>
    Testing,

    /// <summary>Configuration patterns</summary>
    Configuration,

    /// <summary>File I/O patterns</summary>
    FileIO,

    /// <summary>Other patterns</summary>
    Other
}

/// <summary>
/// Error type classification
/// </summary>
public enum ErrorType
{
    /// <summary>Compilation/build errors</summary>
    Compilation,

    /// <summary>Runtime exceptions</summary>
    Runtime,

    /// <summary>Unit test failures</summary>
    TestFailure,

    /// <summary>Integration errors</summary>
    Integration,

    /// <summary>Configuration errors</summary>
    Configuration,

    /// <summary>Dependency errors</summary>
    Dependency
}

/// <summary>
/// Base class for all knowledge entries
/// </summary>
public class KnowledgeEntry
{
    /// <summary>
    /// Unique identifier (e.g., "KB-001")
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Category of this knowledge entry
    /// </summary>
    [JsonPropertyName("category")]
    public KnowledgeCategory Category { get; set; }

    /// <summary>
    /// Short descriptive title
    /// </summary>
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Detailed description of the knowledge
    /// </summary>
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Tags for searching and categorization
    /// </summary>
    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// Programming language (csharp, typescript, etc.)
    /// </summary>
    [JsonPropertyName("language")]
    public string Language { get; set; } = "csharp";

    /// <summary>
    /// Context explaining when this knowledge applies
    /// </summary>
    [JsonPropertyName("context")]
    public string? Context { get; set; }

    /// <summary>
    /// Creation timestamp
    /// </summary>
    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last time this entry was used
    /// </summary>
    [JsonPropertyName("last_used_at")]
    public DateTime? LastUsedAt { get; set; }

    /// <summary>
    /// How many times this knowledge was applied
    /// </summary>
    [JsonPropertyName("usage_count")]
    public int UsageCount { get; set; }

    /// <summary>
    /// Success rate when applied (0.0 to 1.0)
    /// </summary>
    [JsonPropertyName("success_rate")]
    public double SuccessRate { get; set; } = 1.0;

    /// <summary>
    /// Number of times success rate was calculated
    /// </summary>
    [JsonPropertyName("success_attempts")]
    public int SuccessAttempts { get; set; }

    /// <summary>
    /// Story ID that originated this knowledge (if captured automatically)
    /// </summary>
    [JsonPropertyName("source_story_id")]
    public string? SourceStoryId { get; set; }

    /// <summary>
    /// Whether this entry has been verified as working
    /// </summary>
    [JsonPropertyName("is_verified")]
    public bool IsVerified { get; set; }

    /// <summary>
    /// Whether this entry was manually added (vs auto-captured)
    /// </summary>
    [JsonPropertyName("is_manual")]
    public bool IsManual { get; set; }
}

/// <summary>
/// A successful code pattern that solved a problem
/// </summary>
public class SuccessfulPattern : KnowledgeEntry
{
    public SuccessfulPattern()
    {
        Category = KnowledgeCategory.Pattern;
    }

    /// <summary>
    /// Pattern subcategory
    /// </summary>
    [JsonPropertyName("subcategory")]
    public PatternSubcategory Subcategory { get; set; }

    /// <summary>
    /// Description of the problem this pattern solves
    /// </summary>
    [JsonPropertyName("problem_description")]
    public string ProblemDescription { get; set; } = string.Empty;

    /// <summary>
    /// The solution code
    /// </summary>
    [JsonPropertyName("solution_code")]
    public string SolutionCode { get; set; } = string.Empty;

    /// <summary>
    /// Scenarios where this pattern is applicable
    /// </summary>
    [JsonPropertyName("applicable_scenarios")]
    public List<string> ApplicableScenarios { get; set; } = new();

    /// <summary>
    /// Example usage of the pattern
    /// </summary>
    [JsonPropertyName("example_usage")]
    public string? ExampleUsage { get; set; }

    /// <summary>
    /// Dependencies required (NuGet packages, etc.)
    /// </summary>
    [JsonPropertyName("dependencies")]
    public List<string> Dependencies { get; set; } = new();

    /// <summary>
    /// Related patterns
    /// </summary>
    [JsonPropertyName("related_patterns")]
    public List<string> RelatedPatterns { get; set; } = new();
}

/// <summary>
/// A common error and its fix
/// </summary>
public class CommonError : KnowledgeEntry
{
    public CommonError()
    {
        Category = KnowledgeCategory.Error;
    }

    /// <summary>
    /// Type of error
    /// </summary>
    [JsonPropertyName("error_type")]
    public ErrorType ErrorType { get; set; }

    /// <summary>
    /// Error message pattern (can be regex)
    /// </summary>
    [JsonPropertyName("error_pattern")]
    public string ErrorPattern { get; set; } = string.Empty;

    /// <summary>
    /// Exact error message (if not using pattern)
    /// </summary>
    [JsonPropertyName("error_message")]
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Root cause of the error
    /// </summary>
    [JsonPropertyName("root_cause")]
    public string RootCause { get; set; } = string.Empty;

    /// <summary>
    /// Description of the fix
    /// </summary>
    [JsonPropertyName("fix_description")]
    public string FixDescription { get; set; } = string.Empty;

    /// <summary>
    /// Code that fixes the error
    /// </summary>
    [JsonPropertyName("fix_code")]
    public string? FixCode { get; set; }

    /// <summary>
    /// How many times this error was encountered
    /// </summary>
    [JsonPropertyName("occurrence_count")]
    public int OccurrenceCount { get; set; } = 1;

    /// <summary>
    /// Preventive measures to avoid this error
    /// </summary>
    [JsonPropertyName("prevention_tips")]
    public List<string> PreventionTips { get; set; } = new();
}

/// <summary>
/// A project template for scaffolding
/// </summary>
public class ProjectTemplate : KnowledgeEntry
{
    public ProjectTemplate()
    {
        Category = KnowledgeCategory.Template;
    }

    /// <summary>
    /// Template type (WebAPI, Library, Console, etc.)
    /// </summary>
    [JsonPropertyName("template_type")]
    public string TemplateType { get; set; } = string.Empty;

    /// <summary>
    /// Target framework
    /// </summary>
    [JsonPropertyName("target_framework")]
    public string TargetFramework { get; set; } = string.Empty;

    /// <summary>
    /// Files included in this template
    /// </summary>
    [JsonPropertyName("template_files")]
    public List<TemplateFile> TemplateFiles { get; set; } = new();

    /// <summary>
    /// Required NuGet packages
    /// </summary>
    [JsonPropertyName("packages")]
    public List<PackageInfo> Packages { get; set; } = new();

    /// <summary>
    /// Setup instructions
    /// </summary>
    [JsonPropertyName("setup_instructions")]
    public string? SetupInstructions { get; set; }
}

/// <summary>
/// A file in a project template
/// </summary>
public class TemplateFile
{
    /// <summary>
    /// Relative file path
    /// </summary>
    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// File content (with placeholders like {{ProjectName}})
    /// </summary>
    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Whether this file is required
    /// </summary>
    [JsonPropertyName("is_required")]
    public bool IsRequired { get; set; } = true;
}

/// <summary>
/// Package information
/// </summary>
public class PackageInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public string? Version { get; set; }

    [JsonPropertyName("is_required")]
    public bool IsRequired { get; set; } = true;
}

/// <summary>
/// Agent performance insight
/// </summary>
public class AgentInsight : KnowledgeEntry
{
    public AgentInsight()
    {
        Category = KnowledgeCategory.AgentInsight;
    }

    /// <summary>
    /// Which agent this insight is about
    /// </summary>
    [JsonPropertyName("agent_name")]
    public string AgentName { get; set; } = string.Empty;

    /// <summary>
    /// Prompt modification that improved results
    /// </summary>
    [JsonPropertyName("prompt_insight")]
    public string? PromptInsight { get; set; }

    /// <summary>
    /// Optimal temperature setting
    /// </summary>
    [JsonPropertyName("optimal_temperature")]
    public float? OptimalTemperature { get; set; }

    /// <summary>
    /// Scenario where this insight applies
    /// </summary>
    [JsonPropertyName("scenario")]
    public string Scenario { get; set; } = string.Empty;

    /// <summary>
    /// Performance improvement observed
    /// </summary>
    [JsonPropertyName("improvement_description")]
    public string? ImprovementDescription { get; set; }
}

/// <summary>
/// Request to capture a successful pattern
/// </summary>
public class CapturePatternRequest
{
    public string Title { get; set; } = string.Empty;
    public string ProblemDescription { get; set; } = string.Empty;
    public string SolutionCode { get; set; } = string.Empty;
    public PatternSubcategory Subcategory { get; set; }
    public List<string> Tags { get; set; } = new();
    public string Language { get; set; } = "csharp";
    public string? Context { get; set; }
    public List<string> ApplicableScenarios { get; set; } = new();
    public string? ExampleUsage { get; set; }
    public string? SourceStoryId { get; set; }
}

/// <summary>
/// Request to capture an error fix
/// </summary>
public class CaptureErrorRequest
{
    public string Title { get; set; } = string.Empty;
    public ErrorType ErrorType { get; set; }
    public string ErrorPattern { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public string RootCause { get; set; } = string.Empty;
    public string FixDescription { get; set; } = string.Empty;
    public string? FixCode { get; set; }
    public List<string> Tags { get; set; } = new();
    public string Language { get; set; } = "csharp";
    public List<string> PreventionTips { get; set; } = new();
    public string? SourceStoryId { get; set; }
}

/// <summary>
/// Search parameters for knowledge base
/// </summary>
public class KnowledgeSearchParams
{
    public string? Query { get; set; }
    public KnowledgeCategory? Category { get; set; }
    public List<string>? Tags { get; set; }
    public string? Language { get; set; }
    public bool? IsVerified { get; set; }
    public int Limit { get; set; } = 20;
    public int Offset { get; set; } = 0;
}

/// <summary>
/// Knowledge base statistics
/// </summary>
public class KnowledgeStats
{
    [JsonPropertyName("total_entries")]
    public int TotalEntries { get; set; }

    [JsonPropertyName("patterns_count")]
    public int PatternsCount { get; set; }

    [JsonPropertyName("errors_count")]
    public int ErrorsCount { get; set; }

    [JsonPropertyName("templates_count")]
    public int TemplatesCount { get; set; }

    [JsonPropertyName("insights_count")]
    public int InsightsCount { get; set; }

    [JsonPropertyName("verified_count")]
    public int VerifiedCount { get; set; }

    [JsonPropertyName("most_used")]
    public List<KnowledgeUsageStat> MostUsed { get; set; } = new();

    [JsonPropertyName("recently_added")]
    public List<KnowledgeEntrySummary> RecentlyAdded { get; set; } = new();

    [JsonPropertyName("top_tags")]
    public Dictionary<string, int> TopTags { get; set; } = new();
}

/// <summary>
/// Usage statistic for a knowledge entry
/// </summary>
public class KnowledgeUsageStat
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    public KnowledgeCategory Category { get; set; }

    [JsonPropertyName("usage_count")]
    public int UsageCount { get; set; }

    [JsonPropertyName("success_rate")]
    public double SuccessRate { get; set; }
}

/// <summary>
/// Summary of a knowledge entry
/// </summary>
public class KnowledgeEntrySummary
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    public KnowledgeCategory Category { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = new();
}

/// <summary>
/// Result of searching for a matching error fix
/// </summary>
public class ErrorMatchResult
{
    [JsonPropertyName("found")]
    public bool Found { get; set; }

    [JsonPropertyName("error")]
    public CommonError? Error { get; set; }

    [JsonPropertyName("match_score")]
    public double MatchScore { get; set; }

    [JsonPropertyName("matched_on")]
    public string? MatchedOn { get; set; } // "exact", "pattern", "similar"
}

/// <summary>
/// Result of searching for similar patterns
/// </summary>
public class PatternSearchResult
{
    [JsonPropertyName("patterns")]
    public List<SuccessfulPattern> Patterns { get; set; } = new();

    [JsonPropertyName("relevance_scores")]
    public Dictionary<string, double> RelevanceScores { get; set; } = new();
}
