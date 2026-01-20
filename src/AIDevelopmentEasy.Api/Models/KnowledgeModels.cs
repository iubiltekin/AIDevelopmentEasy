using AIDevelopmentEasy.Core.Models;

namespace AIDevelopmentEasy.Api.Models;

// ═══════════════════════════════════════════════════════════════════════════════
// Knowledge Base DTOs
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Knowledge entry summary for list views
/// </summary>
public class KnowledgeEntryDto
{
    public string Id { get; set; } = string.Empty;
    public KnowledgeCategoryDto Category { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public string Language { get; set; } = "csharp";
    public DateTime CreatedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public int UsageCount { get; set; }
    public double SuccessRate { get; set; }
    public bool IsVerified { get; set; }
    public bool IsManual { get; set; }
    public string? SourceStoryId { get; set; }
}

/// <summary>
/// Successful pattern DTO
/// </summary>
public class SuccessfulPatternDto : KnowledgeEntryDto
{
    public PatternSubcategoryDto Subcategory { get; set; }
    public string ProblemDescription { get; set; } = string.Empty;
    public string SolutionCode { get; set; } = string.Empty;
    public List<string> ApplicableScenarios { get; set; } = new();
    public string? ExampleUsage { get; set; }
    public List<string> Dependencies { get; set; } = new();
    public List<string> RelatedPatterns { get; set; } = new();
}

/// <summary>
/// Common error DTO
/// </summary>
public class CommonErrorDto : KnowledgeEntryDto
{
    public ErrorTypeDto ErrorType { get; set; }
    public string ErrorPattern { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public string RootCause { get; set; } = string.Empty;
    public string FixDescription { get; set; } = string.Empty;
    public string? FixCode { get; set; }
    public int OccurrenceCount { get; set; }
    public List<string> PreventionTips { get; set; } = new();
}

/// <summary>
/// Project template DTO
/// </summary>
public class ProjectTemplateDto : KnowledgeEntryDto
{
    public string TemplateType { get; set; } = string.Empty;
    public string TargetFramework { get; set; } = string.Empty;
    public List<TemplateFileDto> TemplateFiles { get; set; } = new();
    public List<PackageInfoDto> Packages { get; set; } = new();
    public string? SetupInstructions { get; set; }
}

/// <summary>
/// Template file DTO
/// </summary>
public class TemplateFileDto
{
    public string Path { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool IsRequired { get; set; } = true;
}

/// <summary>
/// Package info DTO
/// </summary>
public class PackageInfoDto
{
    public string Name { get; set; } = string.Empty;
    public string? Version { get; set; }
    public bool IsRequired { get; set; } = true;
}

/// <summary>
/// Knowledge base statistics DTO
/// </summary>
public class KnowledgeStatsDto
{
    public int TotalEntries { get; set; }
    public int PatternsCount { get; set; }
    public int ErrorsCount { get; set; }
    public int TemplatesCount { get; set; }
    public int InsightsCount { get; set; }
    public int VerifiedCount { get; set; }
    public List<KnowledgeUsageStatDto> MostUsed { get; set; } = new();
    public List<KnowledgeEntrySummaryDto> RecentlyAdded { get; set; } = new();
    public Dictionary<string, int> TopTags { get; set; } = new();
}

/// <summary>
/// Usage stat DTO
/// </summary>
public class KnowledgeUsageStatDto
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public KnowledgeCategoryDto Category { get; set; }
    public int UsageCount { get; set; }
    public double SuccessRate { get; set; }
}

/// <summary>
/// Entry summary DTO
/// </summary>
public class KnowledgeEntrySummaryDto
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public KnowledgeCategoryDto Category { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<string> Tags { get; set; } = new();
}

/// <summary>
/// Error match result DTO
/// </summary>
public class ErrorMatchResultDto
{
    public bool Found { get; set; }
    public CommonErrorDto? Error { get; set; }
    public double MatchScore { get; set; }
    public string? MatchedOn { get; set; }
}

/// <summary>
/// Pattern search result DTO
/// </summary>
public class PatternSearchResultDto
{
    public List<SuccessfulPatternDto> Patterns { get; set; } = new();
    public Dictionary<string, double> RelevanceScores { get; set; } = new();
}

// ═══════════════════════════════════════════════════════════════════════════════
// Request DTOs
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Request to create a pattern
/// </summary>
public class CreatePatternRequest
{
    public string Title { get; set; } = string.Empty;
    public string ProblemDescription { get; set; } = string.Empty;
    public string SolutionCode { get; set; } = string.Empty;
    public PatternSubcategoryDto Subcategory { get; set; }
    public List<string> Tags { get; set; } = new();
    public string Language { get; set; } = "csharp";
    public string? Context { get; set; }
    public List<string> ApplicableScenarios { get; set; } = new();
    public string? ExampleUsage { get; set; }
    public List<string> Dependencies { get; set; } = new();
}

/// <summary>
/// Request to create an error entry
/// </summary>
public class CreateErrorRequest
{
    public string Title { get; set; } = string.Empty;
    public ErrorTypeDto ErrorType { get; set; }
    public string ErrorPattern { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public string RootCause { get; set; } = string.Empty;
    public string FixDescription { get; set; } = string.Empty;
    public string? FixCode { get; set; }
    public List<string> Tags { get; set; } = new();
    public string Language { get; set; } = "csharp";
    public List<string> PreventionTips { get; set; } = new();
}

/// <summary>
/// Request to search knowledge base
/// </summary>
public class SearchKnowledgeRequest
{
    public string? Query { get; set; }
    public KnowledgeCategoryDto? Category { get; set; }
    public List<string>? Tags { get; set; }
    public string? Language { get; set; }
    public bool? IsVerified { get; set; }
    public int Limit { get; set; } = 20;
    public int Offset { get; set; } = 0;
}

/// <summary>
/// Request to find matching error
/// </summary>
public class FindErrorRequest
{
    public string ErrorMessage { get; set; } = string.Empty;
    public ErrorTypeDto? ErrorType { get; set; }
}

/// <summary>
/// Request to find similar patterns
/// </summary>
public class FindPatternsRequest
{
    public string ProblemDescription { get; set; } = string.Empty;
    public int Limit { get; set; } = 5;
}

/// <summary>
/// Request to update an entry
/// </summary>
public class UpdateKnowledgeRequest
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public string? Context { get; set; }
}

// ═══════════════════════════════════════════════════════════════════════════════
// Enums
// ═══════════════════════════════════════════════════════════════════════════════

public enum KnowledgeCategoryDto
{
    Pattern = 0,
    Error = 1,
    Template = 2,
    AgentInsight = 3
}

public enum PatternSubcategoryDto
{
    Logging = 0,
    Repository = 1,
    Validation = 2,
    ErrorHandling = 3,
    DependencyInjection = 4,
    ApiDesign = 5,
    Testing = 6,
    Configuration = 7,
    FileIO = 8,
    Other = 9
}

public enum ErrorTypeDto
{
    Compilation = 0,
    Runtime = 1,
    TestFailure = 2,
    Integration = 3,
    Configuration = 4,
    Dependency = 5
}

// ═══════════════════════════════════════════════════════════════════════════════
// Mapping Extensions
// ═══════════════════════════════════════════════════════════════════════════════

public static class KnowledgeMappingExtensions
{
    public static KnowledgeEntryDto ToDto(this KnowledgeEntry entry)
    {
        return entry switch
        {
            SuccessfulPattern pattern => pattern.ToDto(),
            CommonError error => error.ToDto(),
            ProjectTemplate template => template.ToDto(),
            _ => new KnowledgeEntryDto
            {
                Id = entry.Id,
                Category = (KnowledgeCategoryDto)entry.Category,
                Title = entry.Title,
                Description = entry.Description,
                Tags = entry.Tags,
                Language = entry.Language,
                CreatedAt = entry.CreatedAt,
                LastUsedAt = entry.LastUsedAt,
                UsageCount = entry.UsageCount,
                SuccessRate = entry.SuccessRate,
                IsVerified = entry.IsVerified,
                IsManual = entry.IsManual,
                SourceStoryId = entry.SourceStoryId
            }
        };
    }

    public static SuccessfulPatternDto ToDto(this SuccessfulPattern pattern)
    {
        return new SuccessfulPatternDto
        {
            Id = pattern.Id,
            Category = KnowledgeCategoryDto.Pattern,
            Title = pattern.Title,
            Description = pattern.Description,
            Tags = pattern.Tags,
            Language = pattern.Language,
            CreatedAt = pattern.CreatedAt,
            LastUsedAt = pattern.LastUsedAt,
            UsageCount = pattern.UsageCount,
            SuccessRate = pattern.SuccessRate,
            IsVerified = pattern.IsVerified,
            IsManual = pattern.IsManual,
            SourceStoryId = pattern.SourceStoryId,
            Subcategory = (PatternSubcategoryDto)pattern.Subcategory,
            ProblemDescription = pattern.ProblemDescription,
            SolutionCode = pattern.SolutionCode,
            ApplicableScenarios = pattern.ApplicableScenarios,
            ExampleUsage = pattern.ExampleUsage,
            Dependencies = pattern.Dependencies,
            RelatedPatterns = pattern.RelatedPatterns
        };
    }

    public static CommonErrorDto ToDto(this CommonError error)
    {
        return new CommonErrorDto
        {
            Id = error.Id,
            Category = KnowledgeCategoryDto.Error,
            Title = error.Title,
            Description = error.Description,
            Tags = error.Tags,
            Language = error.Language,
            CreatedAt = error.CreatedAt,
            LastUsedAt = error.LastUsedAt,
            UsageCount = error.UsageCount,
            SuccessRate = error.SuccessRate,
            IsVerified = error.IsVerified,
            IsManual = error.IsManual,
            SourceStoryId = error.SourceStoryId,
            ErrorType = (ErrorTypeDto)error.ErrorType,
            ErrorPattern = error.ErrorPattern,
            ErrorMessage = error.ErrorMessage,
            RootCause = error.RootCause,
            FixDescription = error.FixDescription,
            FixCode = error.FixCode,
            OccurrenceCount = error.OccurrenceCount,
            PreventionTips = error.PreventionTips
        };
    }

    public static ProjectTemplateDto ToDto(this ProjectTemplate template)
    {
        return new ProjectTemplateDto
        {
            Id = template.Id,
            Category = KnowledgeCategoryDto.Template,
            Title = template.Title,
            Description = template.Description,
            Tags = template.Tags,
            Language = template.Language,
            CreatedAt = template.CreatedAt,
            LastUsedAt = template.LastUsedAt,
            UsageCount = template.UsageCount,
            SuccessRate = template.SuccessRate,
            IsVerified = template.IsVerified,
            IsManual = template.IsManual,
            SourceStoryId = template.SourceStoryId,
            TemplateType = template.TemplateType,
            TargetFramework = template.TargetFramework,
            TemplateFiles = template.TemplateFiles.Select(f => new TemplateFileDto
            {
                Path = f.Path,
                Content = f.Content,
                IsRequired = f.IsRequired
            }).ToList(),
            Packages = template.Packages.Select(p => new PackageInfoDto
            {
                Name = p.Name,
                Version = p.Version,
                IsRequired = p.IsRequired
            }).ToList(),
            SetupInstructions = template.SetupInstructions
        };
    }

    public static KnowledgeStatsDto ToDto(this KnowledgeStats stats)
    {
        return new KnowledgeStatsDto
        {
            TotalEntries = stats.TotalEntries,
            PatternsCount = stats.PatternsCount,
            ErrorsCount = stats.ErrorsCount,
            TemplatesCount = stats.TemplatesCount,
            InsightsCount = stats.InsightsCount,
            VerifiedCount = stats.VerifiedCount,
            MostUsed = stats.MostUsed.Select(u => new KnowledgeUsageStatDto
            {
                Id = u.Id,
                Title = u.Title,
                Category = (KnowledgeCategoryDto)u.Category,
                UsageCount = u.UsageCount,
                SuccessRate = u.SuccessRate
            }).ToList(),
            RecentlyAdded = stats.RecentlyAdded.Select(e => new KnowledgeEntrySummaryDto
            {
                Id = e.Id,
                Title = e.Title,
                Category = (KnowledgeCategoryDto)e.Category,
                CreatedAt = e.CreatedAt,
                Tags = e.Tags
            }).ToList(),
            TopTags = stats.TopTags
        };
    }

    public static CapturePatternRequest ToCoreRequest(this CreatePatternRequest request)
    {
        return new CapturePatternRequest
        {
            Title = request.Title,
            ProblemDescription = request.ProblemDescription,
            SolutionCode = request.SolutionCode,
            Subcategory = (PatternSubcategory)request.Subcategory,
            Tags = request.Tags,
            Language = request.Language,
            Context = request.Context,
            ApplicableScenarios = request.ApplicableScenarios,
            ExampleUsage = request.ExampleUsage
        };
    }

    public static CaptureErrorRequest ToCoreRequest(this CreateErrorRequest request)
    {
        return new CaptureErrorRequest
        {
            Title = request.Title,
            ErrorType = (ErrorType)request.ErrorType,
            ErrorPattern = request.ErrorPattern,
            ErrorMessage = request.ErrorMessage,
            RootCause = request.RootCause,
            FixDescription = request.FixDescription,
            FixCode = request.FixCode,
            Tags = request.Tags,
            Language = request.Language,
            PreventionTips = request.PreventionTips
        };
    }

    public static KnowledgeSearchParams ToCoreParams(this SearchKnowledgeRequest request)
    {
        return new KnowledgeSearchParams
        {
            Query = request.Query,
            Category = request.Category.HasValue ? (KnowledgeCategory)request.Category.Value : null,
            Tags = request.Tags,
            Language = request.Language,
            IsVerified = request.IsVerified,
            Limit = request.Limit,
            Offset = request.Offset
        };
    }
}
