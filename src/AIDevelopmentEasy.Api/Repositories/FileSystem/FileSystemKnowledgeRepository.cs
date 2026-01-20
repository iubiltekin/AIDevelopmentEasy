using System.Text.Json;
using System.Text.RegularExpressions;
using AIDevelopmentEasy.Api.Repositories.Interfaces;
using AIDevelopmentEasy.Core.Models;

namespace AIDevelopmentEasy.Api.Repositories.FileSystem;

/// <summary>
/// File system based implementation of IKnowledgeRepository.
/// Knowledge entries are stored as JSON files organized by category.
/// 
/// Directory structure:
/// knowledge-base/
/// ├── patterns/       → SuccessfulPattern entries
/// ├── errors/         → CommonError entries
/// ├── templates/      → ProjectTemplate entries
/// └── insights/       → AgentInsight entries
/// </summary>
public class FileSystemKnowledgeRepository : IKnowledgeRepository
{
    private readonly string _basePath;
    private readonly ILogger<FileSystemKnowledgeRepository> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    private string PatternsPath => Path.Combine(_basePath, "patterns");
    private string ErrorsPath => Path.Combine(_basePath, "errors");
    private string TemplatesPath => Path.Combine(_basePath, "templates");
    private string InsightsPath => Path.Combine(_basePath, "insights");

    private static int _idCounter = 0;

    public FileSystemKnowledgeRepository(
        string knowledgeBasePath,
        ILogger<FileSystemKnowledgeRepository> logger)
    {
        _basePath = knowledgeBasePath;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        EnsureDirectoriesExist();
    }

    private void EnsureDirectoriesExist()
    {
        Directory.CreateDirectory(_basePath);
        Directory.CreateDirectory(PatternsPath);
        Directory.CreateDirectory(ErrorsPath);
        Directory.CreateDirectory(TemplatesPath);
        Directory.CreateDirectory(InsightsPath);
    }

    private string GetPathForCategory(KnowledgeCategory category) => category switch
    {
        KnowledgeCategory.Pattern => PatternsPath,
        KnowledgeCategory.Error => ErrorsPath,
        KnowledgeCategory.Template => TemplatesPath,
        KnowledgeCategory.AgentInsight => InsightsPath,
        _ => _basePath
    };

    private string GenerateId(KnowledgeCategory category)
    {
        var prefix = category switch
        {
            KnowledgeCategory.Pattern => "PAT",
            KnowledgeCategory.Error => "ERR",
            KnowledgeCategory.Template => "TPL",
            KnowledgeCategory.AgentInsight => "INS",
            _ => "KB"
        };

        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd");
        var counter = Interlocked.Increment(ref _idCounter);
        return $"{prefix}-{timestamp}-{counter:D4}";
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // CRUD Operations
    // ═══════════════════════════════════════════════════════════════════════════════

    public async Task<IEnumerable<KnowledgeEntry>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entries = new List<KnowledgeEntry>();

        entries.AddRange(await GetPatternsAsync(cancellationToken));
        entries.AddRange(await GetErrorsAsync(cancellationToken));
        entries.AddRange(await GetTemplatesAsync(cancellationToken));
        entries.AddRange(await GetAgentInsightsAsync(cancellationToken));

        return entries.OrderByDescending(e => e.CreatedAt);
    }

    public async Task<IEnumerable<KnowledgeEntry>> GetByCategoryAsync(KnowledgeCategory category, CancellationToken cancellationToken = default)
    {
        return category switch
        {
            KnowledgeCategory.Pattern => (await GetPatternsAsync(cancellationToken)).Cast<KnowledgeEntry>(),
            KnowledgeCategory.Error => (await GetErrorsAsync(cancellationToken)).Cast<KnowledgeEntry>(),
            KnowledgeCategory.Template => (await GetTemplatesAsync(cancellationToken)).Cast<KnowledgeEntry>(),
            KnowledgeCategory.AgentInsight => (await GetAgentInsightsAsync(cancellationToken)).Cast<KnowledgeEntry>(),
            _ => Enumerable.Empty<KnowledgeEntry>()
        };
    }

    public async Task<KnowledgeEntry?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        // Determine category from ID prefix
        var category = GetCategoryFromId(id);
        var path = GetPathForCategory(category);
        var filePath = Path.Combine(path, $"{id}.json");

        if (!File.Exists(filePath))
        {
            _logger.LogWarning("[Knowledge] Entry not found: {Id}", id);
            return null;
        }

        return await LoadEntryAsync(filePath, category, cancellationToken);
    }

    public async Task<KnowledgeEntry> CreateAsync(KnowledgeEntry entry, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(entry.Id))
        {
            entry.Id = GenerateId(entry.Category);
        }

        entry.CreatedAt = DateTime.UtcNow;

        var path = GetPathForCategory(entry.Category);
        var filePath = Path.Combine(path, $"{entry.Id}.json");

        await SaveEntryAsync(filePath, entry, cancellationToken);

        _logger.LogInformation("[Knowledge] Created entry: {Id} - {Title}", entry.Id, entry.Title);
        return entry;
    }

    public async Task<bool> UpdateAsync(KnowledgeEntry entry, CancellationToken cancellationToken = default)
    {
        var path = GetPathForCategory(entry.Category);
        var filePath = Path.Combine(path, $"{entry.Id}.json");

        if (!File.Exists(filePath))
        {
            _logger.LogWarning("[Knowledge] Entry not found for update: {Id}", entry.Id);
            return false;
        }

        await SaveEntryAsync(filePath, entry, cancellationToken);

        _logger.LogInformation("[Knowledge] Updated entry: {Id}", entry.Id);
        return true;
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var category = GetCategoryFromId(id);
        var path = GetPathForCategory(category);
        var filePath = Path.Combine(path, $"{id}.json");

        if (!File.Exists(filePath))
        {
            _logger.LogWarning("[Knowledge] Entry not found for deletion: {Id}", id);
            return false;
        }

        await Task.Run(() => File.Delete(filePath), cancellationToken);

        _logger.LogInformation("[Knowledge] Deleted entry: {Id}", id);
        return true;
    }

    public Task<bool> ExistsAsync(string id, CancellationToken cancellationToken = default)
    {
        var category = GetCategoryFromId(id);
        var path = GetPathForCategory(category);
        var filePath = Path.Combine(path, $"{id}.json");

        return Task.FromResult(File.Exists(filePath));
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // Pattern Operations
    // ═══════════════════════════════════════════════════════════════════════════════

    public async Task<IEnumerable<SuccessfulPattern>> GetPatternsAsync(CancellationToken cancellationToken = default)
    {
        var patterns = new List<SuccessfulPattern>();

        if (!Directory.Exists(PatternsPath))
            return patterns;

        var files = Directory.GetFiles(PatternsPath, "*.json");

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var pattern = await LoadEntryAsync<SuccessfulPattern>(file, cancellationToken);
            if (pattern != null)
            {
                patterns.Add(pattern);
            }
        }

        return patterns.OrderByDescending(p => p.UsageCount).ThenByDescending(p => p.CreatedAt);
    }

    public async Task<SuccessfulPattern?> GetPatternByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var filePath = Path.Combine(PatternsPath, $"{id}.json");
        if (!File.Exists(filePath))
            return null;

        return await LoadEntryAsync<SuccessfulPattern>(filePath, cancellationToken);
    }

    public async Task<PatternSearchResult> FindSimilarPatternsAsync(
        string problemDescription,
        int limit = 5,
        CancellationToken cancellationToken = default)
    {
        var result = new PatternSearchResult();
        var allPatterns = await GetPatternsAsync(cancellationToken);

        var keywords = ExtractKeywords(problemDescription);

        foreach (var pattern in allPatterns)
        {
            var score = CalculateRelevanceScore(pattern, keywords, problemDescription);
            if (score > 0.1)
            {
                result.RelevanceScores[pattern.Id] = score;
            }
        }

        result.Patterns = allPatterns
            .Where(p => result.RelevanceScores.ContainsKey(p.Id))
            .OrderByDescending(p => result.RelevanceScores[p.Id])
            .Take(limit)
            .ToList();

        return result;
    }

    public async Task<IEnumerable<SuccessfulPattern>> GetPatternsBySubcategoryAsync(
        PatternSubcategory subcategory,
        CancellationToken cancellationToken = default)
    {
        var allPatterns = await GetPatternsAsync(cancellationToken);
        return allPatterns.Where(p => p.Subcategory == subcategory);
    }

    public async Task<SuccessfulPattern> CreatePatternAsync(CapturePatternRequest request, CancellationToken cancellationToken = default)
    {
        var pattern = new SuccessfulPattern
        {
            Id = GenerateId(KnowledgeCategory.Pattern),
            Title = request.Title,
            Description = request.ProblemDescription,
            ProblemDescription = request.ProblemDescription,
            SolutionCode = request.SolutionCode,
            Subcategory = request.Subcategory,
            Tags = request.Tags,
            Language = request.Language,
            Context = request.Context,
            ApplicableScenarios = request.ApplicableScenarios,
            ExampleUsage = request.ExampleUsage,
            SourceStoryId = request.SourceStoryId,
            CreatedAt = DateTime.UtcNow,
            IsManual = string.IsNullOrEmpty(request.SourceStoryId)
        };

        var filePath = Path.Combine(PatternsPath, $"{pattern.Id}.json");
        await SaveEntryAsync(filePath, pattern, cancellationToken);

        _logger.LogInformation("[Knowledge] Created pattern: {Id} - {Title}", pattern.Id, pattern.Title);
        return pattern;
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // Error Operations
    // ═══════════════════════════════════════════════════════════════════════════════

    public async Task<IEnumerable<CommonError>> GetErrorsAsync(CancellationToken cancellationToken = default)
    {
        var errors = new List<CommonError>();

        if (!Directory.Exists(ErrorsPath))
            return errors;

        var files = Directory.GetFiles(ErrorsPath, "*.json");

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var error = await LoadEntryAsync<CommonError>(file, cancellationToken);
            if (error != null)
            {
                errors.Add(error);
            }
        }

        return errors.OrderByDescending(e => e.OccurrenceCount).ThenByDescending(e => e.CreatedAt);
    }

    public async Task<CommonError?> GetErrorByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var filePath = Path.Combine(ErrorsPath, $"{id}.json");
        if (!File.Exists(filePath))
            return null;

        return await LoadEntryAsync<CommonError>(filePath, cancellationToken);
    }

    public async Task<ErrorMatchResult> FindMatchingErrorAsync(
        string errorMessage,
        ErrorType? errorType = null,
        CancellationToken cancellationToken = default)
    {
        var result = new ErrorMatchResult { Found = false };
        var allErrors = await GetErrorsAsync(cancellationToken);

        if (errorType.HasValue)
        {
            allErrors = allErrors.Where(e => e.ErrorType == errorType.Value);
        }

        // First try exact match
        var exactMatch = allErrors.FirstOrDefault(e =>
            !string.IsNullOrEmpty(e.ErrorMessage) &&
            e.ErrorMessage.Equals(errorMessage, StringComparison.OrdinalIgnoreCase));

        if (exactMatch != null)
        {
            result.Found = true;
            result.Error = exactMatch;
            result.MatchScore = 1.0;
            result.MatchedOn = "exact";
            return result;
        }

        // Try regex pattern match
        foreach (var error in allErrors.Where(e => !string.IsNullOrEmpty(e.ErrorPattern)))
        {
            try
            {
                if (Regex.IsMatch(errorMessage, error.ErrorPattern, RegexOptions.IgnoreCase))
                {
                    result.Found = true;
                    result.Error = error;
                    result.MatchScore = 0.9;
                    result.MatchedOn = "pattern";
                    return result;
                }
            }
            catch (RegexMatchTimeoutException)
            {
                // Skip invalid regex patterns
            }
        }

        // Try similarity match
        var bestMatch = allErrors
            .Select(e => new
            {
                Error = e,
                Score = CalculateStringSimilarity(errorMessage, e.ErrorMessage ?? e.ErrorPattern)
            })
            .Where(x => x.Score > 0.5)
            .OrderByDescending(x => x.Score)
            .FirstOrDefault();

        if (bestMatch != null)
        {
            result.Found = true;
            result.Error = bestMatch.Error;
            result.MatchScore = bestMatch.Score;
            result.MatchedOn = "similar";
        }

        return result;
    }

    public async Task<IEnumerable<CommonError>> GetErrorsByTypeAsync(
        ErrorType errorType,
        CancellationToken cancellationToken = default)
    {
        var allErrors = await GetErrorsAsync(cancellationToken);
        return allErrors.Where(e => e.ErrorType == errorType);
    }

    public async Task<CommonError> CreateErrorAsync(CaptureErrorRequest request, CancellationToken cancellationToken = default)
    {
        var error = new CommonError
        {
            Id = GenerateId(KnowledgeCategory.Error),
            Title = request.Title,
            Description = request.FixDescription,
            ErrorType = request.ErrorType,
            ErrorPattern = request.ErrorPattern,
            ErrorMessage = request.ErrorMessage,
            RootCause = request.RootCause,
            FixDescription = request.FixDescription,
            FixCode = request.FixCode,
            Tags = request.Tags,
            Language = request.Language,
            PreventionTips = request.PreventionTips,
            SourceStoryId = request.SourceStoryId,
            CreatedAt = DateTime.UtcNow,
            OccurrenceCount = 1,
            IsManual = string.IsNullOrEmpty(request.SourceStoryId)
        };

        var filePath = Path.Combine(ErrorsPath, $"{error.Id}.json");
        await SaveEntryAsync(filePath, error, cancellationToken);

        _logger.LogInformation("[Knowledge] Created error: {Id} - {Title}", error.Id, error.Title);
        return error;
    }

    public async Task IncrementErrorOccurrenceAsync(string id, CancellationToken cancellationToken = default)
    {
        var error = await GetErrorByIdAsync(id, cancellationToken);
        if (error != null)
        {
            error.OccurrenceCount++;
            error.LastUsedAt = DateTime.UtcNow;
            await UpdateAsync(error, cancellationToken);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // Template Operations
    // ═══════════════════════════════════════════════════════════════════════════════

    public async Task<IEnumerable<ProjectTemplate>> GetTemplatesAsync(CancellationToken cancellationToken = default)
    {
        var templates = new List<ProjectTemplate>();

        if (!Directory.Exists(TemplatesPath))
            return templates;

        var files = Directory.GetFiles(TemplatesPath, "*.json");

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var template = await LoadEntryAsync<ProjectTemplate>(file, cancellationToken);
            if (template != null)
            {
                templates.Add(template);
            }
        }

        return templates.OrderByDescending(t => t.UsageCount).ThenByDescending(t => t.CreatedAt);
    }

    public async Task<ProjectTemplate?> GetTemplateByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var filePath = Path.Combine(TemplatesPath, $"{id}.json");
        if (!File.Exists(filePath))
            return null;

        return await LoadEntryAsync<ProjectTemplate>(filePath, cancellationToken);
    }

    public async Task<IEnumerable<ProjectTemplate>> GetTemplatesByTypeAsync(
        string templateType,
        CancellationToken cancellationToken = default)
    {
        var allTemplates = await GetTemplatesAsync(cancellationToken);
        return allTemplates.Where(t => t.TemplateType.Equals(templateType, StringComparison.OrdinalIgnoreCase));
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // Agent Insight Operations
    // ═══════════════════════════════════════════════════════════════════════════════

    private async Task<IEnumerable<AgentInsight>> GetAgentInsightsAsync(CancellationToken cancellationToken = default)
    {
        var insights = new List<AgentInsight>();

        if (!Directory.Exists(InsightsPath))
            return insights;

        var files = Directory.GetFiles(InsightsPath, "*.json");

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var insight = await LoadEntryAsync<AgentInsight>(file, cancellationToken);
            if (insight != null)
            {
                insights.Add(insight);
            }
        }

        return insights.OrderByDescending(i => i.CreatedAt);
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // Search Operations
    // ═══════════════════════════════════════════════════════════════════════════════

    public async Task<IEnumerable<KnowledgeEntry>> SearchAsync(
        KnowledgeSearchParams searchParams,
        CancellationToken cancellationToken = default)
    {
        IEnumerable<KnowledgeEntry> entries;

        if (searchParams.Category.HasValue)
        {
            entries = await GetByCategoryAsync(searchParams.Category.Value, cancellationToken);
        }
        else
        {
            entries = await GetAllAsync(cancellationToken);
        }

        // Filter by tags
        if (searchParams.Tags?.Any() == true)
        {
            entries = entries.Where(e =>
                searchParams.Tags.Any(t => e.Tags.Contains(t, StringComparer.OrdinalIgnoreCase)));
        }

        // Filter by language
        if (!string.IsNullOrEmpty(searchParams.Language))
        {
            entries = entries.Where(e =>
                e.Language.Equals(searchParams.Language, StringComparison.OrdinalIgnoreCase));
        }

        // Filter by verified status
        if (searchParams.IsVerified.HasValue)
        {
            entries = entries.Where(e => e.IsVerified == searchParams.IsVerified.Value);
        }

        // Filter by query (search in title, description, tags)
        if (!string.IsNullOrEmpty(searchParams.Query))
        {
            var query = searchParams.Query.ToLower();
            entries = entries.Where(e =>
                e.Title.ToLower().Contains(query) ||
                e.Description.ToLower().Contains(query) ||
                e.Tags.Any(t => t.ToLower().Contains(query)));
        }

        // Apply pagination
        return entries
            .Skip(searchParams.Offset)
            .Take(searchParams.Limit);
    }

    public async Task<IEnumerable<KnowledgeEntry>> SearchByTagsAsync(
        List<string> tags,
        KnowledgeCategory? category = null,
        CancellationToken cancellationToken = default)
    {
        return await SearchAsync(new KnowledgeSearchParams
        {
            Tags = tags,
            Category = category
        }, cancellationToken);
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // Usage Tracking
    // ═══════════════════════════════════════════════════════════════════════════════

    public async Task RecordUsageAsync(string id, CancellationToken cancellationToken = default)
    {
        var entry = await GetByIdAsync(id, cancellationToken);
        if (entry != null)
        {
            entry.UsageCount++;
            entry.LastUsedAt = DateTime.UtcNow;
            await UpdateAsync(entry, cancellationToken);
            _logger.LogDebug("[Knowledge] Recorded usage for: {Id}", id);
        }
    }

    public async Task UpdateSuccessRateAsync(string id, bool wasSuccessful, CancellationToken cancellationToken = default)
    {
        var entry = await GetByIdAsync(id, cancellationToken);
        if (entry != null)
        {
            entry.SuccessAttempts++;
            var totalSuccess = entry.SuccessRate * (entry.SuccessAttempts - 1);
            totalSuccess += wasSuccessful ? 1 : 0;
            entry.SuccessRate = totalSuccess / entry.SuccessAttempts;

            await UpdateAsync(entry, cancellationToken);
            _logger.LogDebug("[Knowledge] Updated success rate for {Id}: {Rate:P}", id, entry.SuccessRate);
        }
    }

    public async Task MarkAsVerifiedAsync(string id, CancellationToken cancellationToken = default)
    {
        var entry = await GetByIdAsync(id, cancellationToken);
        if (entry != null)
        {
            entry.IsVerified = true;
            await UpdateAsync(entry, cancellationToken);
            _logger.LogInformation("[Knowledge] Marked as verified: {Id}", id);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // Statistics
    // ═══════════════════════════════════════════════════════════════════════════════

    public async Task<KnowledgeStats> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        var allEntries = (await GetAllAsync(cancellationToken)).ToList();

        var stats = new KnowledgeStats
        {
            TotalEntries = allEntries.Count,
            PatternsCount = allEntries.Count(e => e.Category == KnowledgeCategory.Pattern),
            ErrorsCount = allEntries.Count(e => e.Category == KnowledgeCategory.Error),
            TemplatesCount = allEntries.Count(e => e.Category == KnowledgeCategory.Template),
            InsightsCount = allEntries.Count(e => e.Category == KnowledgeCategory.AgentInsight),
            VerifiedCount = allEntries.Count(e => e.IsVerified),

            MostUsed = allEntries
                .Where(e => e.UsageCount > 0)
                .OrderByDescending(e => e.UsageCount)
                .Take(10)
                .Select(e => new KnowledgeUsageStat
                {
                    Id = e.Id,
                    Title = e.Title,
                    Category = e.Category,
                    UsageCount = e.UsageCount,
                    SuccessRate = e.SuccessRate
                })
                .ToList(),

            RecentlyAdded = allEntries
                .OrderByDescending(e => e.CreatedAt)
                .Take(10)
                .Select(e => new KnowledgeEntrySummary
                {
                    Id = e.Id,
                    Title = e.Title,
                    Category = e.Category,
                    CreatedAt = e.CreatedAt,
                    Tags = e.Tags
                })
                .ToList(),

            TopTags = allEntries
                .SelectMany(e => e.Tags)
                .GroupBy(t => t.ToLower())
                .OrderByDescending(g => g.Count())
                .Take(20)
                .ToDictionary(g => g.Key, g => g.Count())
        };

        return stats;
    }

    public async Task<IEnumerable<string>> GetAllTagsAsync(CancellationToken cancellationToken = default)
    {
        var allEntries = await GetAllAsync(cancellationToken);
        return allEntries
            .SelectMany(e => e.Tags)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(t => t);
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // Helper Methods
    // ═══════════════════════════════════════════════════════════════════════════════

    private KnowledgeCategory GetCategoryFromId(string id)
    {
        if (id.StartsWith("PAT-")) return KnowledgeCategory.Pattern;
        if (id.StartsWith("ERR-")) return KnowledgeCategory.Error;
        if (id.StartsWith("TPL-")) return KnowledgeCategory.Template;
        if (id.StartsWith("INS-")) return KnowledgeCategory.AgentInsight;
        return KnowledgeCategory.Pattern; // Default
    }

    private async Task<T?> LoadEntryAsync<T>(string filePath, CancellationToken cancellationToken) where T : KnowledgeEntry
    {
        try
        {
            var json = await File.ReadAllTextAsync(filePath, cancellationToken);
            return JsonSerializer.Deserialize<T>(json, _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Knowledge] Error loading entry from {Path}", filePath);
            return null;
        }
    }

    private async Task<KnowledgeEntry?> LoadEntryAsync(string filePath, KnowledgeCategory category, CancellationToken cancellationToken)
    {
        return category switch
        {
            KnowledgeCategory.Pattern => await LoadEntryAsync<SuccessfulPattern>(filePath, cancellationToken),
            KnowledgeCategory.Error => await LoadEntryAsync<CommonError>(filePath, cancellationToken),
            KnowledgeCategory.Template => await LoadEntryAsync<ProjectTemplate>(filePath, cancellationToken),
            KnowledgeCategory.AgentInsight => await LoadEntryAsync<AgentInsight>(filePath, cancellationToken),
            _ => await LoadEntryAsync<KnowledgeEntry>(filePath, cancellationToken)
        };
    }

    private async Task SaveEntryAsync(string filePath, KnowledgeEntry entry, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(entry, entry.GetType(), _jsonOptions);
        await File.WriteAllTextAsync(filePath, json, cancellationToken);
    }

    private static List<string> ExtractKeywords(string text)
    {
        var words = Regex.Split(text.ToLower(), @"\W+")
            .Where(w => w.Length > 2)
            .Distinct()
            .ToList();

        // Remove common stop words
        var stopWords = new HashSet<string> { "the", "and", "for", "with", "that", "this", "from", "have", "has" };
        return words.Where(w => !stopWords.Contains(w)).ToList();
    }

    private static double CalculateRelevanceScore(SuccessfulPattern pattern, List<string> keywords, string problemDescription)
    {
        double score = 0;

        // Check title match
        var titleKeywords = ExtractKeywords(pattern.Title);
        var titleMatches = keywords.Count(k => titleKeywords.Contains(k));
        score += titleMatches * 0.3;

        // Check problem description match
        var problemKeywords = ExtractKeywords(pattern.ProblemDescription);
        var problemMatches = keywords.Count(k => problemKeywords.Contains(k));
        score += problemMatches * 0.3;

        // Check tags match
        var tagMatches = keywords.Count(k => pattern.Tags.Any(t => t.ToLower().Contains(k)));
        score += tagMatches * 0.2;

        // Check applicable scenarios
        var scenarioKeywords = pattern.ApplicableScenarios.SelectMany(ExtractKeywords).ToList();
        var scenarioMatches = keywords.Count(k => scenarioKeywords.Contains(k));
        score += scenarioMatches * 0.2;

        // Normalize by total keywords
        if (keywords.Count > 0)
        {
            score /= keywords.Count;
        }

        // Boost verified patterns
        if (pattern.IsVerified)
        {
            score *= 1.2;
        }

        // Boost high usage patterns
        if (pattern.UsageCount > 5)
        {
            score *= 1.1;
        }

        return Math.Min(score, 1.0);
    }

    private static double CalculateStringSimilarity(string s1, string? s2)
    {
        if (string.IsNullOrEmpty(s2)) return 0;

        s1 = s1.ToLower();
        s2 = s2.ToLower();

        var words1 = ExtractKeywords(s1);
        var words2 = ExtractKeywords(s2);

        if (words1.Count == 0 || words2.Count == 0) return 0;

        var intersection = words1.Intersect(words2).Count();
        var union = words1.Union(words2).Count();

        return (double)intersection / union; // Jaccard similarity
    }
}
