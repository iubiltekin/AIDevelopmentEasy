using Azure.AI.OpenAI;
using AIDevelopmentEasy.Api.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace AIDevelopmentEasy.Api.Services;

/// <summary>
/// Uses the configured LLM to generate a short, standard story title from content.
/// Falls back to first-line truncation if the LLM call fails.
/// </summary>
public class StoryNameGenerator : IStoryNameGenerator
{
    private readonly OpenAIClient _openAIClient;
    private readonly string _deploymentName;
    private readonly ILogger<StoryNameGenerator> _logger;
    private const int MaxNameLength = 60;

    public StoryNameGenerator(OpenAIClient openAIClient, string deploymentName, ILogger<StoryNameGenerator> logger)
    {
        _openAIClient = openAIClient;
        _deploymentName = deploymentName;
        _logger = logger;
    }

    public async Task<string> GenerateStoryNameAsync(string content, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(content))
            return "Untitled Story";

        try
        {
            var systemPrompt = "You are a helper that generates short, clear titles for development stories. " +
                "Given the story description, respond with ONLY a concise title in the same language as the content. " +
                "Use title case, no quotes, no period, maximum 50 characters. No explanation.";
            var userPrompt = content.Length > 800 ? content[..800] + "..." : content;

            var options = new ChatCompletionsOptions
            {
                DeploymentName = _deploymentName,
                Messages =
                {
                    new ChatRequestSystemMessage(systemPrompt),
                    new ChatRequestUserMessage(userPrompt)
                },
                Temperature = 0.3f,
                MaxTokens = 80
            };

            var response = await _openAIClient.GetChatCompletionsAsync(options, cancellationToken);
            var name = (response.Value.Choices[0].Message.Content ?? "").Trim();
            if (name.Length > MaxNameLength)
                name = name[..MaxNameLength].Trim();
            if (!string.IsNullOrEmpty(name))
            {
                _logger.LogDebug("Generated story name: {Name}", name);
                return name;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Story name generation failed, using fallback");
        }

        return GetFallbackName(content);
    }

    private static string GetFallbackName(string content)
    {
        var firstLine = content.Split('\n').FirstOrDefault()?.Trim() ?? content;
        if (firstLine.Length > MaxNameLength)
            firstLine = firstLine[..MaxNameLength] + "...";
        return string.IsNullOrWhiteSpace(firstLine) ? "Untitled Story" : firstLine;
    }
}
