using System.Text.Json;
using AIDevelopmentEasy.Api.Models;
using AIDevelopmentEasy.Api.Repositories.Interfaces;
using AIDevelopmentEasy.Core.Models;

namespace AIDevelopmentEasy.Api.Repositories.FileSystem;

/// <summary>
/// File system based implementation of IRequirementRepository.
/// Requirements are stored as JSON files in the requirements directory.
/// </summary>
public class FileSystemRequirementRepository : IRequirementRepository
{
    private readonly string _requirementsPath;
    private readonly ILogger<FileSystemRequirementRepository> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public FileSystemRequirementRepository(
        string requirementsPath,
        ILogger<FileSystemRequirementRepository> logger)
    {
        _requirementsPath = requirementsPath;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        if (!Directory.Exists(_requirementsPath))
        {
            Directory.CreateDirectory(_requirementsPath);
        }
    }

    #region Basic CRUD

    public async Task<IEnumerable<RequirementDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var requirements = new List<RequirementDto>();

        if (!Directory.Exists(_requirementsPath))
            return requirements;

        // Only get requirement files, exclude .wizard.json files
        var files = Directory.GetFiles(_requirementsPath, "REQ-*.json")
            .Where(f => !f.EndsWith(".wizard.json", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(f => File.GetCreationTimeUtc(f));

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var entity = await LoadEntityFromFileAsync(file, cancellationToken);
                if (entity != null)
                {
                    requirements.Add(MapToDto(entity));
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error reading requirement file: {File}", file);
            }
        }

        return requirements;
    }

    public async Task<RequirementDetailDto?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var entity = await GetEntityAsync(id, cancellationToken);
        if (entity == null)
            return null;

        return MapToDetailDto(entity);
    }

    public async Task<RequirementDto> CreateAsync(CreateRequirementRequest request, CancellationToken cancellationToken = default)
    {
        // Generate ID
        var id = GenerateId();

        // Create title if not provided
        var title = string.IsNullOrWhiteSpace(request.Title)
            ? GenerateTitleFromContent(request.RawContent)
            : request.Title;

        var entity = new Requirement
        {
            Id = id,
            Title = title,
            RawContent = request.RawContent,
            Type = MapTypeFromDto(request.Type),
            Status = RequirementStatus.Draft,
            CurrentPhase = WizardPhase.Input,
            CodebaseId = request.CodebaseId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await SaveEntityAsync(entity, cancellationToken);

        _logger.LogInformation("Created requirement: {Id} - {Title}", id, title);

        return MapToDto(entity);
    }

    public async Task<RequirementDto?> UpdateAsync(string id, UpdateRequirementRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await GetEntityAsync(id, cancellationToken);
        if (entity == null)
            return null;

        // Only allow updates when in Draft status
        if (entity.Status != RequirementStatus.Draft)
        {
            _logger.LogWarning("Cannot update requirement {Id} - not in Draft status", id);
            return null;
        }

        if (!string.IsNullOrWhiteSpace(request.Title))
            entity.Title = request.Title;

        if (!string.IsNullOrWhiteSpace(request.RawContent))
            entity.RawContent = request.RawContent;

        if (request.Type.HasValue)
            entity.Type = MapTypeFromDto(request.Type.Value);

        if (request.CodebaseId != null)
            entity.CodebaseId = request.CodebaseId;

        entity.UpdatedAt = DateTime.UtcNow;

        await SaveEntityAsync(entity, cancellationToken);

        _logger.LogInformation("Updated requirement: {Id}", id);

        return MapToDto(entity);
    }

    public Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var filePath = GetFilePath(id);
        if (!File.Exists(filePath))
            return Task.FromResult(false);

        File.Delete(filePath);

        // Delete wizard status file if exists
        var statusPath = GetWizardStatusPath(id);
        if (File.Exists(statusPath))
            File.Delete(statusPath);

        _logger.LogInformation("Deleted requirement: {Id}", id);
        return Task.FromResult(true);
    }

    public Task<bool> ExistsAsync(string id, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(File.Exists(GetFilePath(id)));
    }

    public async Task<Requirement?> GetEntityAsync(string id, CancellationToken cancellationToken = default)
    {
        var filePath = GetFilePath(id);
        if (!File.Exists(filePath))
            return null;

        return await LoadEntityFromFileAsync(filePath, cancellationToken);
    }

    public async Task SaveEntityAsync(Requirement requirement, CancellationToken cancellationToken = default)
    {
        requirement.UpdatedAt = DateTime.UtcNow;
        var filePath = GetFilePath(requirement.Id);
        var json = JsonSerializer.Serialize(requirement, _jsonOptions);
        await File.WriteAllTextAsync(filePath, json, cancellationToken);
    }

    #endregion

    #region Wizard State Operations

    public async Task UpdateStatusAsync(string id, RequirementStatusDto status, CancellationToken cancellationToken = default)
    {
        var entity = await GetEntityAsync(id, cancellationToken);
        if (entity == null)
            return;

        entity.Status = MapStatusFromDto(status);
        entity.UpdatedAt = DateTime.UtcNow;

        if (status == RequirementStatusDto.Completed)
            entity.CompletedAt = DateTime.UtcNow;

        await SaveEntityAsync(entity, cancellationToken);
    }

    public async Task UpdatePhaseAsync(string id, WizardPhaseDto phase, CancellationToken cancellationToken = default)
    {
        var entity = await GetEntityAsync(id, cancellationToken);
        if (entity == null)
            return;

        entity.CurrentPhase = MapPhaseFromDto(phase);
        entity.UpdatedAt = DateTime.UtcNow;

        await SaveEntityAsync(entity, cancellationToken);
    }

    public async Task SaveQuestionsAsync(string id, QuestionSetDto questions, CancellationToken cancellationToken = default)
    {
        var entity = await GetEntityAsync(id, cancellationToken);
        if (entity == null)
            return;

        entity.Questions = MapQuestionSetFromDto(questions);
        entity.UpdatedAt = DateTime.UtcNow;

        await SaveEntityAsync(entity, cancellationToken);
    }

    public async Task SaveAnswersAsync(string id, AnswerSetDto answers, string? aiNotes, CancellationToken cancellationToken = default)
    {
        var entity = await GetEntityAsync(id, cancellationToken);
        if (entity == null)
            return;

        entity.Answers = MapAnswerSetFromDto(answers);
        entity.AiNotes = aiNotes;
        entity.UpdatedAt = DateTime.UtcNow;

        await SaveEntityAsync(entity, cancellationToken);
    }

    public async Task SaveFinalContentAsync(string id, string finalContent, CancellationToken cancellationToken = default)
    {
        var entity = await GetEntityAsync(id, cancellationToken);
        if (entity == null)
            return;

        entity.FinalContent = finalContent;
        entity.UpdatedAt = DateTime.UtcNow;

        await SaveEntityAsync(entity, cancellationToken);
    }

    public async Task SaveGeneratedStoriesAsync(string id, List<StoryDefinitionDto> stories, CancellationToken cancellationToken = default)
    {
        var entity = await GetEntityAsync(id, cancellationToken);
        if (entity == null)
            return;

        entity.GeneratedStories = stories.Select(MapStoryDefinitionFromDto).ToList();
        entity.UpdatedAt = DateTime.UtcNow;

        await SaveEntityAsync(entity, cancellationToken);
    }

    public async Task AddCreatedStoryIdAsync(string id, string storyId, CancellationToken cancellationToken = default)
    {
        var entity = await GetEntityAsync(id, cancellationToken);
        if (entity == null)
            return;

        if (!entity.CreatedStoryIds.Contains(storyId))
        {
            entity.CreatedStoryIds.Add(storyId);
            entity.UpdatedAt = DateTime.UtcNow;
            await SaveEntityAsync(entity, cancellationToken);
        }
    }

    public async Task<IEnumerable<string>> GetCreatedStoryIdsAsync(string id, CancellationToken cancellationToken = default)
    {
        var entity = await GetEntityAsync(id, cancellationToken);
        return entity?.CreatedStoryIds ?? new List<string>();
    }

    #endregion

    #region Wizard History Operations

    public async Task SaveWizardStatusAsync(string id, WizardStatusDto status, CancellationToken cancellationToken = default)
    {
        var statusPath = GetWizardStatusPath(id);
        var json = JsonSerializer.Serialize(status, _jsonOptions);
        await File.WriteAllTextAsync(statusPath, json, cancellationToken);
    }

    public async Task<WizardStatusDto?> GetWizardStatusAsync(string id, CancellationToken cancellationToken = default)
    {
        var statusPath = GetWizardStatusPath(id);
        if (!File.Exists(statusPath))
            return null;

        try
        {
            var json = await File.ReadAllTextAsync(statusPath, cancellationToken);
            return JsonSerializer.Deserialize<WizardStatusDto>(json, _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error reading wizard status for: {Id}", id);
            return null;
        }
    }

    #endregion

    #region Helper Methods

    private string GetFilePath(string id) => Path.Combine(_requirementsPath, $"{id}.json");

    private string GetWizardStatusPath(string id) => Path.Combine(_requirementsPath, $"{id}.wizard.json");

    private string GenerateId()
    {
        // Format: REQ-YYYYMMDD-XXXX
        var date = DateTime.UtcNow.ToString("yyyyMMdd");
        var random = Guid.NewGuid().ToString("N")[..4].ToUpper();
        return $"REQ-{date}-{random}";
    }

    private string GenerateTitleFromContent(string content)
    {
        // Take first line or first 50 chars
        var firstLine = content.Split('\n').FirstOrDefault()?.Trim() ?? content;
        if (firstLine.Length > 50)
            firstLine = firstLine[..50] + "...";
        return firstLine;
    }

    private async Task<Requirement?> LoadEntityFromFileAsync(string filePath, CancellationToken cancellationToken)
    {
        try
        {
            var json = await File.ReadAllTextAsync(filePath, cancellationToken);
            return JsonSerializer.Deserialize<Requirement>(json, _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error loading requirement from: {Path}", filePath);
            return null;
        }
    }

    #endregion

    #region Mapping Methods

    private RequirementDto MapToDto(Requirement entity)
    {
        return new RequirementDto
        {
            Id = entity.Id,
            Title = entity.Title,
            RawContent = entity.RawContent,
            FinalContent = entity.FinalContent,
            Type = MapTypeToDto(entity.Type),
            Status = MapStatusToDto(entity.Status),
            CurrentPhase = MapPhaseToDto(entity.CurrentPhase),
            CodebaseId = entity.CodebaseId,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
            CompletedAt = entity.CompletedAt,
            StoryCount = entity.CreatedStoryIds.Count,
            CreatedStoryIds = entity.CreatedStoryIds
        };
    }

    private RequirementDetailDto MapToDetailDto(Requirement entity)
    {
        return new RequirementDetailDto
        {
            Id = entity.Id,
            Title = entity.Title,
            RawContent = entity.RawContent,
            FinalContent = entity.FinalContent,
            Type = MapTypeToDto(entity.Type),
            Status = MapStatusToDto(entity.Status),
            CurrentPhase = MapPhaseToDto(entity.CurrentPhase),
            CodebaseId = entity.CodebaseId,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
            CompletedAt = entity.CompletedAt,
            StoryCount = entity.CreatedStoryIds.Count,
            CreatedStoryIds = entity.CreatedStoryIds,
            Questions = entity.Questions != null ? MapQuestionSetToDto(entity.Questions) : null,
            Answers = entity.Answers != null ? MapAnswerSetToDto(entity.Answers) : null,
            AiNotes = entity.AiNotes,
            GeneratedStories = entity.GeneratedStories.Select(MapStoryDefinitionToDto).ToList()
        };
    }

    // Type mappings
    private static RequirementTypeDto MapTypeToDto(RequirementType type) => type switch
    {
        RequirementType.Feature => RequirementTypeDto.Feature,
        RequirementType.Improvement => RequirementTypeDto.Improvement,
        RequirementType.Defect => RequirementTypeDto.Defect,
        RequirementType.TechDebt => RequirementTypeDto.TechDebt,
        _ => RequirementTypeDto.Feature
    };

    private static RequirementType MapTypeFromDto(RequirementTypeDto type) => type switch
    {
        RequirementTypeDto.Feature => RequirementType.Feature,
        RequirementTypeDto.Improvement => RequirementType.Improvement,
        RequirementTypeDto.Defect => RequirementType.Defect,
        RequirementTypeDto.TechDebt => RequirementType.TechDebt,
        _ => RequirementType.Feature
    };

    // Status mappings
    private static RequirementStatusDto MapStatusToDto(RequirementStatus status) => status switch
    {
        RequirementStatus.Draft => RequirementStatusDto.Draft,
        RequirementStatus.InProgress => RequirementStatusDto.InProgress,
        RequirementStatus.Completed => RequirementStatusDto.Completed,
        RequirementStatus.Cancelled => RequirementStatusDto.Cancelled,
        RequirementStatus.Failed => RequirementStatusDto.Failed,
        _ => RequirementStatusDto.Draft
    };

    private static RequirementStatus MapStatusFromDto(RequirementStatusDto status) => status switch
    {
        RequirementStatusDto.Draft => RequirementStatus.Draft,
        RequirementStatusDto.InProgress => RequirementStatus.InProgress,
        RequirementStatusDto.Completed => RequirementStatus.Completed,
        RequirementStatusDto.Cancelled => RequirementStatus.Cancelled,
        RequirementStatusDto.Failed => RequirementStatus.Failed,
        _ => RequirementStatus.Draft
    };

    // Phase mappings
    private static WizardPhaseDto MapPhaseToDto(WizardPhase phase) => phase switch
    {
        WizardPhase.Input => WizardPhaseDto.Input,
        WizardPhase.Analysis => WizardPhaseDto.Analysis,
        WizardPhase.Questions => WizardPhaseDto.Questions,
        WizardPhase.Refinement => WizardPhaseDto.Refinement,
        WizardPhase.Decomposition => WizardPhaseDto.Decomposition,
        WizardPhase.Review => WizardPhaseDto.Review,
        WizardPhase.Completed => WizardPhaseDto.Completed,
        _ => WizardPhaseDto.Input
    };

    private static WizardPhase MapPhaseFromDto(WizardPhaseDto phase) => phase switch
    {
        WizardPhaseDto.Input => WizardPhase.Input,
        WizardPhaseDto.Analysis => WizardPhase.Analysis,
        WizardPhaseDto.Questions => WizardPhase.Questions,
        WizardPhaseDto.Refinement => WizardPhase.Refinement,
        WizardPhaseDto.Decomposition => WizardPhase.Decomposition,
        WizardPhaseDto.Review => WizardPhase.Review,
        WizardPhaseDto.Completed => WizardPhase.Completed,
        _ => WizardPhase.Input
    };

    // QuestionSet mappings
    private QuestionSetDto MapQuestionSetToDto(QuestionSet set)
    {
        return new QuestionSetDto
        {
            Questions = set.Questions.Select(q => new QuestionDto
            {
                Id = q.Id,
                Category = MapQuestionCategoryToDto(q.Category),
                Text = q.Text,
                Type = MapQuestionTypeToDto(q.Type),
                Options = q.Options,
                Required = q.Required,
                Context = q.Context
            }).ToList()
        };
    }

    private QuestionSet MapQuestionSetFromDto(QuestionSetDto dto)
    {
        return new QuestionSet
        {
            Questions = dto.Questions.Select(q => new Question
            {
                Id = q.Id,
                Category = MapQuestionCategoryFromDto(q.Category),
                Text = q.Text,
                Type = MapQuestionTypeFromDto(q.Type),
                Options = q.Options,
                Required = q.Required,
                Context = q.Context
            }).ToList()
        };
    }

    // AnswerSet mappings
    private AnswerSetDto MapAnswerSetToDto(AnswerSet set)
    {
        return new AnswerSetDto
        {
            Answers = set.Answers.Select(a => new AnswerDto
            {
                QuestionId = a.QuestionId,
                SelectedOptions = a.SelectedOptions,
                TextResponse = a.TextResponse
            }).ToList()
        };
    }

    private AnswerSet MapAnswerSetFromDto(AnswerSetDto dto)
    {
        return new AnswerSet
        {
            Answers = dto.Answers.Select(a => new Answer
            {
                QuestionId = a.QuestionId,
                SelectedOptions = a.SelectedOptions,
                TextResponse = a.TextResponse
            }).ToList()
        };
    }

    // StoryDefinition mappings
    private StoryDefinitionDto MapStoryDefinitionToDto(StoryDefinition def)
    {
        return new StoryDefinitionDto
        {
            Id = def.Id,
            Title = def.Title,
            Description = def.Description,
            AcceptanceCriteria = def.AcceptanceCriteria,
            EstimatedComplexity = MapComplexityToDto(def.EstimatedComplexity),
            Dependencies = def.Dependencies,
            TechnicalNotes = def.TechnicalNotes,
            Selected = def.Selected
        };
    }

    private StoryDefinition MapStoryDefinitionFromDto(StoryDefinitionDto dto)
    {
        return new StoryDefinition
        {
            Id = dto.Id,
            Title = dto.Title,
            Description = dto.Description,
            AcceptanceCriteria = dto.AcceptanceCriteria,
            EstimatedComplexity = MapComplexityFromDto(dto.EstimatedComplexity),
            Dependencies = dto.Dependencies,
            TechnicalNotes = dto.TechnicalNotes,
            Selected = dto.Selected
        };
    }

    // Question category mappings
    private static QuestionCategoryDto MapQuestionCategoryToDto(QuestionCategory cat) => cat switch
    {
        QuestionCategory.Functional => QuestionCategoryDto.Functional,
        QuestionCategory.NonFunctional => QuestionCategoryDto.NonFunctional,
        QuestionCategory.Technical => QuestionCategoryDto.Technical,
        QuestionCategory.Business => QuestionCategoryDto.Business,
        QuestionCategory.UX => QuestionCategoryDto.UX,
        _ => QuestionCategoryDto.Functional
    };

    private static QuestionCategory MapQuestionCategoryFromDto(QuestionCategoryDto cat) => cat switch
    {
        QuestionCategoryDto.Functional => QuestionCategory.Functional,
        QuestionCategoryDto.NonFunctional => QuestionCategory.NonFunctional,
        QuestionCategoryDto.Technical => QuestionCategory.Technical,
        QuestionCategoryDto.Business => QuestionCategory.Business,
        QuestionCategoryDto.UX => QuestionCategory.UX,
        _ => QuestionCategory.Functional
    };

    // Question type mappings
    private static QuestionTypeDto MapQuestionTypeToDto(QuestionType type) => type switch
    {
        QuestionType.Single => QuestionTypeDto.Single,
        QuestionType.Multiple => QuestionTypeDto.Multiple,
        QuestionType.Text => QuestionTypeDto.Text,
        _ => QuestionTypeDto.Single
    };

    private static QuestionType MapQuestionTypeFromDto(QuestionTypeDto type) => type switch
    {
        QuestionTypeDto.Single => QuestionType.Single,
        QuestionTypeDto.Multiple => QuestionType.Multiple,
        QuestionTypeDto.Text => QuestionType.Text,
        _ => QuestionType.Single
    };

    // Complexity mappings
    private static StoryComplexityDto MapComplexityToDto(StoryComplexity complexity) => complexity switch
    {
        StoryComplexity.Small => StoryComplexityDto.Small,
        StoryComplexity.Medium => StoryComplexityDto.Medium,
        StoryComplexity.Large => StoryComplexityDto.Large,
        _ => StoryComplexityDto.Medium
    };

    private static StoryComplexity MapComplexityFromDto(StoryComplexityDto complexity) => complexity switch
    {
        StoryComplexityDto.Small => StoryComplexity.Small,
        StoryComplexityDto.Medium => StoryComplexity.Medium,
        StoryComplexityDto.Large => StoryComplexity.Large,
        _ => StoryComplexity.Medium
    };

    #endregion
}
