using System.Text.Json;
using AIDevelopmentEasy.Core.Agents.Base;
using AIDevelopmentEasy.Core.Models;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Logging;

namespace AIDevelopmentEasy.Core.Agents;

/// <summary>
/// Requirement Analyst Agent - Responsible for analyzing raw requirements,
/// generating clarifying questions, refining requirements, and decomposing into stories.
/// 
/// This agent implements the Requirement Wizard workflow:
/// 1. Analyze raw requirement → Generate questions
/// 2. Refine requirement with answers → Create final requirement document
/// 3. Decompose requirement → Generate stories for pipeline
/// </summary>
public class RequirementAnalystAgent : BaseAgent
{
    public override string Name => "RequirementAnalyst";
    public override string Role => "Senior Systems Analyst - Analyzes requirements and decomposes into stories";
    protected override string? PromptFileName => "requirement";

    public RequirementAnalystAgent(OpenAIClient openAIClient, string deploymentName, ILogger<RequirementAnalystAgent>? logger = null)
        : base(openAIClient, deploymentName, logger)
    {
    }

    /// <summary>
    /// Analyze raw requirement and generate clarifying questions.
    /// Phase 2 of the wizard.
    /// </summary>
    /// <param name="rawRequirement">The raw requirement text from user</param>
    /// <param name="requirementType">Type of requirement (Feature, Improvement, etc.)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Analysis result with questions</returns>
    public async Task<AnalysisResult> AnalyzeAsync(
        string rawRequirement,
        RequirementType requirementType,
        CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("[RequirementAnalyst] Starting analysis for {Type} requirement",
            requirementType);

        try
        {
            var systemPrompt = GetSystemPrompt();
            var userPrompt = BuildAnalysisPrompt(rawRequirement, requirementType);

            var (content, tokens) = await CallLLMAsync(
                systemPrompt,
                userPrompt,
                temperature: 0.3f,
                maxTokens: 4000,
                cancellationToken);

            _logger?.LogInformation("[RequirementAnalyst] Analysis response received, parsing questions...");

            // Parse the questions from response
            var questions = ParseQuestions(content);

            return new AnalysisResult
            {
                Success = true,
                RawResponse = content,
                Questions = questions,
                TokensUsed = tokens
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[RequirementAnalyst] Error during analysis");
            return new AnalysisResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    /// <summary>
    /// Refine the requirement using user's answers to create a final requirement document.
    /// Phase 4 of the wizard.
    /// </summary>
    /// <param name="rawRequirement">Original raw requirement</param>
    /// <param name="requirementType">Type of requirement</param>
    /// <param name="answers">User's answers to questions</param>
    /// <param name="aiNotes">Additional notes from user</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Refined requirement document</returns>
    public async Task<RefineResult> RefineAsync(
        string rawRequirement,
        RequirementType requirementType,
        AnswerSet answers,
        string? aiNotes,
        CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("[RequirementAnalyst] Refining requirement with {AnswerCount} answers",
            answers.Answers.Count);

        try
        {
            var systemPrompt = GetSystemPrompt();
            var userPrompt = BuildRefinePrompt(rawRequirement, requirementType, answers, aiNotes);

            var (content, tokens) = await CallLLMAsync(
                systemPrompt,
                userPrompt,
                temperature: 0.3f,
                maxTokens: 6000,
                cancellationToken);

            _logger?.LogInformation("[RequirementAnalyst] Refinement complete");

            return new RefineResult
            {
                Success = true,
                FinalRequirement = content,
                TokensUsed = tokens
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[RequirementAnalyst] Error during refinement");
            return new RefineResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    /// <summary>
    /// Decompose the final requirement into implementable stories.
    /// Phase 5 of the wizard.
    /// </summary>
    /// <param name="finalRequirement">The refined requirement document</param>
    /// <param name="requirementType">Type of requirement</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of story definitions</returns>
    public async Task<DecomposeResult> DecomposeAsync(
        string finalRequirement,
        RequirementType requirementType,
        CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("[RequirementAnalyst] Decomposing requirement into stories");

        try
        {
            var systemPrompt = GetSystemPrompt();
            var userPrompt = BuildDecomposePrompt(finalRequirement, requirementType);

            var (content, tokens) = await CallLLMAsync(
                systemPrompt,
                userPrompt,
                temperature: 0.3f,
                maxTokens: 6000,
                cancellationToken);

            _logger?.LogInformation("[RequirementAnalyst] Decomposition complete, parsing stories...");

            // Parse stories from response
            var stories = ParseStories(content);

            return new DecomposeResult
            {
                Success = true,
                RawResponse = content,
                Stories = stories,
                TokensUsed = tokens
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[RequirementAnalyst] Error during decomposition");
            return new DecomposeResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    public override async Task<AgentResponse> RunAsync(AgentRequest request, CancellationToken cancellationToken = default)
    {
        // This method is not typically used directly - use the specific methods instead
        // But we implement it for interface compliance
        
        var result = await AnalyzeAsync(
            request.Input,
            RequirementType.Feature,
            cancellationToken);

        return new AgentResponse
        {
            Success = result.Success,
            Output = result.RawResponse ?? "",
            Error = result.Error,
            TokensUsed = result.TokensUsed,
            Data = result.Questions != null
                ? new Dictionary<string, object> { ["questions"] = result.Questions }
                : new Dictionary<string, object>()
        };
    }

    protected override string GetFallbackPrompt()
    {
        return @"You are a Senior Systems Analyst and Agile Requirements Engineer.

You are an expert in:
- ISO/IEC/IEEE 29148 requirement quality criteria
- Writing clear, testable requirements for LLM-assisted software development
- Breaking down complex requirements into implementable stories

Your role is to help transform raw business needs into well-structured requirements and development stories.";
    }

    #region Prompt Builders

    private string BuildAnalysisPrompt(string rawRequirement, RequirementType type)
    {
        return $@"# TASK: Analyze Requirement and Generate Questions

## Requirement Type: {type}

## Raw Requirement:
{rawRequirement}

## Instructions:

1. Analyze the raw requirement above
2. Identify ALL missing information needed to make this requirement:
   - Testable (clear acceptance criteria possible)
   - Developable (enough detail for implementation)
   - Complete (no ambiguous terms or undefined behavior)

3. Generate clarifying questions for each gap
4. Provide sensible options where possible (prefer multiple choice over free text)

## Output Format:

Return ONLY a JSON object with this structure:

```json
{{
  ""questions"": [
    {{
      ""id"": ""Q1"",
      ""category"": ""Functional"",
      ""question"": ""What should happen when...?"",
      ""type"": ""single"",
      ""options"": [""Option A"", ""Option B"", ""Option C""],
      ""required"": true,
      ""context"": ""This is needed to determine the error handling behavior""
    }},
    {{
      ""id"": ""Q2"",
      ""category"": ""Technical"",
      ""question"": ""Which data format should be used?"",
      ""type"": ""multiple"",
      ""options"": [""JSON"", ""XML"", ""CSV""],
      ""required"": true,
      ""context"": ""Affects the implementation approach""
    }},
    {{
      ""id"": ""Q3"",
      ""category"": ""Business"",
      ""question"": ""Any additional context about the use case?"",
      ""type"": ""text"",
      ""options"": [],
      ""required"": false,
      ""context"": ""Optional: Helps understand the business context better""
    }}
  ]
}}
```

## Question Categories:
- **Functional**: What the system should do
- **NonFunctional**: Performance, security, scalability requirements
- **Technical**: Implementation details, technologies, integrations
- **Business**: Business rules, constraints, priorities
- **UX**: User experience, interface requirements

## Question Types:
- **single**: Radio buttons - user selects ONE option
- **multiple**: Checkboxes - user can select MULTIPLE options
- **text**: Free text input (use sparingly, only when options are not practical)

## Guidelines:
- Generate 3-8 questions (focus on the most critical gaps)
- Prefer multiple choice questions with well-thought options
- Include a ""context"" explaining why each question matters
- Mark questions as required: true if they are essential
- Order questions by importance (most critical first)

Output ONLY the JSON, no additional text.";
    }

    private string BuildRefinePrompt(string rawRequirement, RequirementType type, AnswerSet answers, string? aiNotes)
    {
        var answersText = FormatAnswersForPrompt(answers);

        return $@"# TASK: Create Final Requirement Document

## Requirement Type: {type}

## Original Raw Requirement:
{rawRequirement}

## User's Answers to Questions:
{answersText}

{(string.IsNullOrEmpty(aiNotes) ? "" : $@"## Additional Notes from User:
{aiNotes}
")}

## Instructions:

Using ALL the information above, create a complete, well-structured requirement document.

## Output Format:

Create the requirement document in this EXACT format:

---

**Title:** [Clear, descriptive title]

**Type:** {type}

**Context:**
[Business context and background - why this is needed]

**Intent:**
[What this requirement aims to achieve]

**Scope:**
[What is in scope and out of scope]

**Constraints:**
[Any technical or business constraints]

**Functional Requirements:**
- FR-1: [Actor] SHALL [action] [object] [condition]
- FR-2: ...
(Use SHALL for mandatory, SHOULD for recommended, MAY for optional)

**Non-Functional Requirements:**
- NFR-1: ...

**Acceptance Criteria:**
- AC-1: Given [precondition], When [action], Then [expected result]
- AC-2: ...

**Test Scenarios:**
- TS-1: [Test scenario description]
- TS-2: ...

**AI Implementation Notes:**
- [Important considerations for AI-assisted implementation]
- [Edge cases to handle]
- [Dependencies or integrations to consider]

---

## Guidelines:
- Use clear, unambiguous language
- Every functional requirement should be testable
- Include ALL information from the user's answers
- Use ISO 29148 style (SHALL/SHOULD/MAY) for requirements
- Make acceptance criteria specific and measurable";
    }

    private string BuildDecomposePrompt(string finalRequirement, RequirementType type)
    {
        return $@"# TASK: Decompose Requirement into Stories

## Requirement Type: {type}

## Final Requirement Document:
{finalRequirement}

## Instructions:

Break down this requirement into implementable development stories. Each story should:
1. Be independently deployable
2. Deliver user value or technical foundation
3. Be small enough to complete in one development cycle
4. Have clear acceptance criteria

## Output Format:

Return ONLY a JSON object with this structure:

```json
{{
  ""stories"": [
    {{
      ""id"": ""STR-1"",
      ""title"": ""Short descriptive title"",
      ""description"": ""Detailed description of what this story delivers. Include specific implementation details."",
      ""acceptanceCriteria"": [
        ""Given [precondition], When [action], Then [expected result]"",
        ""Given [precondition], When [action], Then [expected result]""
      ],
      ""estimatedComplexity"": ""Small"",
      ""dependencies"": [],
      ""technicalNotes"": ""Implementation hints and considerations""
    }},
    {{
      ""id"": ""STR-2"",
      ""title"": ""Another story"",
      ""description"": ""..."",
      ""acceptanceCriteria"": [""...""],
      ""estimatedComplexity"": ""Medium"",
      ""dependencies"": [""STR-1""],
      ""technicalNotes"": ""...""
    }}
  ]
}}
```

## Story Complexity:
- **Small**: 1-2 tasks, straightforward implementation
- **Medium**: 3-5 tasks, some complexity
- **Large**: 5+ tasks, significant complexity

## Decomposition Guidelines:

1. **Foundational stories first**: Infrastructure, models, interfaces
2. **Core functionality second**: Main business logic
3. **Integration third**: Connecting components
4. **Polish last**: UI, validation, error handling

5. **Include technical stories** when needed:
   - Database schema changes
   - API endpoint creation
   - Configuration setup

6. **Each story should have**:
   - Clear title (what it delivers)
   - Detailed description (how to implement)
   - 2-4 acceptance criteria
   - Dependencies on other stories (if any)
   - Technical notes for implementation

7. **Story count**: Aim for 3-8 stories (fewer for simple requirements, more for complex)

Output ONLY the JSON, no additional text.";
    }

    #endregion

    #region Parsers

    private QuestionSet ParseQuestions(string response)
    {
        var questionSet = new QuestionSet();

        try
        {
            var json = ExtractJson(response);
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("questions", out var questionsArray))
            {
                foreach (var q in questionsArray.EnumerateArray())
                {
                    var question = new Question
                    {
                        Id = q.TryGetProperty("id", out var id) ? id.GetString() ?? "" : "",
                        Text = q.TryGetProperty("question", out var text) ? text.GetString() ?? "" : "",
                        Required = q.TryGetProperty("required", out var req) && req.GetBoolean(),
                        Context = q.TryGetProperty("context", out var ctx) ? ctx.GetString() : null
                    };

                    // Parse category
                    if (q.TryGetProperty("category", out var cat))
                    {
                        var catStr = cat.GetString() ?? "Functional";
                        question.Category = catStr.ToLower() switch
                        {
                            "nonfunctional" => QuestionCategory.NonFunctional,
                            "technical" => QuestionCategory.Technical,
                            "business" => QuestionCategory.Business,
                            "ux" => QuestionCategory.UX,
                            _ => QuestionCategory.Functional
                        };
                    }

                    // Parse type
                    if (q.TryGetProperty("type", out var type))
                    {
                        var typeStr = type.GetString() ?? "single";
                        question.Type = typeStr.ToLower() switch
                        {
                            "multiple" => QuestionType.Multiple,
                            "text" => QuestionType.Text,
                            _ => QuestionType.Single
                        };
                    }

                    // Parse options
                    if (q.TryGetProperty("options", out var options))
                    {
                        foreach (var opt in options.EnumerateArray())
                        {
                            var optStr = opt.GetString();
                            if (!string.IsNullOrEmpty(optStr))
                                question.Options.Add(optStr);
                        }
                    }

                    questionSet.Questions.Add(question);
                }
            }

            _logger?.LogInformation("[RequirementAnalyst] Parsed {Count} questions", questionSet.Questions.Count);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "[RequirementAnalyst] Error parsing questions, returning empty set");
        }

        return questionSet;
    }

    private List<StoryDefinition> ParseStories(string response)
    {
        var stories = new List<StoryDefinition>();

        try
        {
            var json = ExtractJson(response);
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("stories", out var storiesArray))
            {
                foreach (var s in storiesArray.EnumerateArray())
                {
                    var story = new StoryDefinition
                    {
                        Id = s.TryGetProperty("id", out var id) ? id.GetString() ?? "" : "",
                        Title = s.TryGetProperty("title", out var title) ? title.GetString() ?? "" : "",
                        Description = s.TryGetProperty("description", out var desc) ? desc.GetString() ?? "" : "",
                        TechnicalNotes = s.TryGetProperty("technicalNotes", out var notes) ? notes.GetString() : null,
                        Selected = true // Default to selected
                    };

                    // Parse complexity
                    if (s.TryGetProperty("estimatedComplexity", out var complexity))
                    {
                        var compStr = complexity.GetString() ?? "Medium";
                        story.EstimatedComplexity = compStr.ToLower() switch
                        {
                            "small" => StoryComplexity.Small,
                            "large" => StoryComplexity.Large,
                            _ => StoryComplexity.Medium
                        };
                    }

                    // Parse acceptance criteria
                    if (s.TryGetProperty("acceptanceCriteria", out var criteria))
                    {
                        foreach (var ac in criteria.EnumerateArray())
                        {
                            var acStr = ac.GetString();
                            if (!string.IsNullOrEmpty(acStr))
                                story.AcceptanceCriteria.Add(acStr);
                        }
                    }

                    // Parse dependencies
                    if (s.TryGetProperty("dependencies", out var deps))
                    {
                        foreach (var dep in deps.EnumerateArray())
                        {
                            var depStr = dep.GetString();
                            if (!string.IsNullOrEmpty(depStr))
                                story.Dependencies.Add(depStr);
                        }
                    }

                    stories.Add(story);
                }
            }

            _logger?.LogInformation("[RequirementAnalyst] Parsed {Count} stories", stories.Count);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "[RequirementAnalyst] Error parsing stories, returning empty list");
        }

        return stories;
    }

    private string FormatAnswersForPrompt(AnswerSet answers)
    {
        var lines = new List<string>();

        foreach (var answer in answers.Answers)
        {
            if (answer.SelectedOptions.Any())
            {
                lines.Add($"- {answer.QuestionId}: {string.Join(", ", answer.SelectedOptions)}");
            }
            else if (!string.IsNullOrEmpty(answer.TextResponse))
            {
                lines.Add($"- {answer.QuestionId}: {answer.TextResponse}");
            }
        }

        return string.Join("\n", lines);
    }

    #endregion
}

#region Result Models

/// <summary>
/// Result of requirement analysis (question generation)
/// </summary>
public class AnalysisResult
{
    public bool Success { get; set; }
    public string? RawResponse { get; set; }
    public QuestionSet? Questions { get; set; }
    public string? Error { get; set; }
    public int TokensUsed { get; set; }
}

/// <summary>
/// Result of requirement refinement
/// </summary>
public class RefineResult
{
    public bool Success { get; set; }
    public string? FinalRequirement { get; set; }
    public string? Error { get; set; }
    public int TokensUsed { get; set; }
}

/// <summary>
/// Result of requirement decomposition
/// </summary>
public class DecomposeResult
{
    public bool Success { get; set; }
    public string? RawResponse { get; set; }
    public List<StoryDefinition> Stories { get; set; } = new();
    public string? Error { get; set; }
    public int TokensUsed { get; set; }
}

#endregion
