using System.Collections.Generic;
using AIDevelopmentEasy.Core.Agents.Base;
using AIDevelopmentEasy.Core.Services;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Logging;

namespace AIDevelopmentEasy.Core.Agents;

/// <summary>
/// Coder Agent - Responsible for code generation.
/// Takes subtasks from the Planner and generates source code for each.
/// Language-agnostic: prompt is loaded from prompts/coder-{language}.md per language (csharp, go, react, python, rust). No fallbacks; missing prompt throws.
/// </summary>
public class CoderAgent : BaseAgent
{
    public override string Name => "Coder";
    public override string Role => "Senior Software Developer - Implements code for given tasks";
    protected override string? PromptFileName => null;

    private readonly string _targetLanguage;

    public CoderAgent(
        OpenAIClient openAIClient,
        string deploymentName,
        string targetLanguage = "csharp",
        ILogger<CoderAgent>? logger = null)
        : base(openAIClient, deploymentName, logger)
    {
        _targetLanguage = targetLanguage ?? "csharp";
    }

    /// <summary>
    /// Resolves target language to prompt file suffix. Throws if language is not supported.
    /// </summary>
    private static string GetCoderPromptNameForLanguage(string targetLanguage)
    {
        var lang = (targetLanguage ?? "").Trim().ToLowerInvariant();
        if (lang is "c#" or "csharp") return "csharp";
        if (lang == "go") return "go";
        if (lang is "react" or "typescript" or "ts") return "react";
        if (lang == "python") return "python";
        if (lang == "rust") return "rust";
        throw new FileNotFoundException(
            $"Unsupported coder language: '{targetLanguage}'. Supported: csharp, go, react, python, rust. Add prompts/coder-{{lang}}.md for the language.");
    }

    /// <summary>
    /// Code block language identifier for modification user prompt (markdown code fence).
    /// </summary>
    private static string GetCodeBlockLanguageForPrompt(string langKey)
    {
        return langKey switch
        {
            "csharp" => "csharp",
            "go" => "go",
            "react" => "tsx",
            "python" => "python",
            "rust" => "rust",
            _ => "text"
        };
    }

    /// <summary>
    /// Primary file extension for the language (for default filename when not inferred).
    /// </summary>
    private static string GetFileExtensionForLanguage(string langKey)
    {
        return langKey switch
        {
            "csharp" => ".cs",
            "go" => ".go",
            "react" => ".tsx",
            "python" => ".py",
            "rust" => ".rs",
            _ => ".txt"
        };
    }

    /// <summary>
    /// Direct LLM call for analysis tasks (e.g., test failure analysis)
    /// Exposed publicly for PipelineService to use
    /// </summary>
    public async Task<(string Content, int Tokens)> CallLLMDirectAsync(
        string systemPrompt,
        string userPrompt,
        float temperature = 0.3f,
        int maxTokens = 4000,
        CancellationToken cancellationToken = default)
    {
        return await CallLLMAsync(systemPrompt, userPrompt, temperature, maxTokens, cancellationToken);
    }

    protected override string GetSystemPrompt()
    {
        var langKey = GetCoderPromptNameForLanguage(_targetLanguage);
        var promptName = $"coder-{langKey}";
        return PromptLoader.Instance.LoadPromptRequired(promptName);
    }

    /// <summary>
    /// Language to use for this request: from context (codebase-aware, per-task) or agent default.
    /// </summary>
    private string GetEffectiveLanguage(AgentRequest request)
    {
        if (request.Context != null && request.Context.TryGetValue("target_language", out var tl) && !string.IsNullOrEmpty(tl))
            return tl.Trim();
        return _targetLanguage;
    }

    public override async Task<AgentResponse> RunAsync(AgentRequest request, CancellationToken cancellationToken = default)
    {
        // Check if this is a modification task
        var isModification = request.Context.TryGetValue("is_modification", out var isMod) && isMod == "true";

        if (isModification)
        {
            return await RunModificationAsync(request, cancellationToken);
        }

        return await RunGenerationAsync(request, cancellationToken);
    }

    /// <summary>
    /// Generate new code (standard mode)
    /// </summary>
    private async Task<AgentResponse> RunGenerationAsync(AgentRequest request, CancellationToken cancellationToken)
    {
        var effectiveLang = GetEffectiveLanguage(request);
        _logger?.LogInformation("[Coder] Starting code generation for task (language: {Lang}): {Task}",
            effectiveLang, request.Input.Length > 100 ? request.Input[..100] + "..." : request.Input);

        try
        {
            // Codebase context (from analysis, per-language) – Coder is codebase-aware like per-language analyzers
            var codebaseSection = "";
            if (request.Context != null && request.Context.TryGetValue("codebase_context", out var codebaseCtx) && !string.IsNullOrEmpty(codebaseCtx))
            {
                codebaseSection = "\n\nCODEBASE (structure, conventions, relevant files – follow these):\n" + codebaseCtx + "\n";
            }

            // Build context from existing files generated so far in this run
            var existingSection = "";
            if (request.ProjectState?.Codebase.Count > 0)
            {
                existingSection = "\n\nEXISTING FILES (already generated this run):\n";
                foreach (var (fname, existingCode) in request.ProjectState.Codebase)
                {
                    existingSection += $"\n--- {fname} ---\n{existingCode}\n";
                }
            }

            // Get any additional context (like plan summary)
            var planContext = "";
            if (request.Context != null && request.Context.TryGetValue("plan_summary", out var summary))
            {
                planContext = $"\n\nPROJECT CONTEXT:\n{summary}\n";
            }

            // Get target namespace - CRITICAL for correct code generation
            var namespaceSection = "";
            if (request.Context != null && request.Context.TryGetValue("target_namespace", out var targetNs) && !string.IsNullOrEmpty(targetNs))
            {
                namespaceSection = $@"

TARGET NAMESPACE (REQUIRED):
You MUST use this exact namespace: {targetNs}

The generated code MUST start with:
namespace {targetNs}
{{
    // Your implementation here
}}

DO NOT use any other namespace. DO NOT shorten or modify this namespace.
";
                _logger?.LogInformation("[Coder] Using target namespace: {Namespace}", targetNs);
            }

            // Get project name for additional context
            var projectContext = "";
            if (request.Context != null && request.Context.TryGetValue("project_name", out var projectName) && !string.IsNullOrEmpty(projectName))
            {
                projectContext = $"\nTARGET PROJECT: {projectName}\n";
            }

            var userPrompt = $@"Please implement the following task:

TASK:
{request.Input}
{projectContext}{namespaceSection}{planContext}{codebaseSection}{existingSection}
Generate the complete code for this task. Make sure to:
1. Include all necessary imports/using statements
2. Use the EXACT namespace specified above (if provided)
3. Match codebase conventions and structure when codebase section is present
4. Handle potential errors
5. Add helpful comments
6. Follow best practices

Output the code in a markdown code block.";

            var langKey = GetCoderPromptNameForLanguage(effectiveLang);
            var systemPrompt = BuildSystemPromptWithStandards(request.ProjectState,
                PromptLoader.Instance.LoadPromptRequired($"coder-{langKey}"));

            var (content, tokens) = await CallLLMAsync(
                systemPrompt,
                userPrompt,
                temperature: 0.2f,  // Lower temperature for more deterministic code
                maxTokens: 4000,
                cancellationToken);

            _logger?.LogInformation("[Coder] Raw response:\n{Content}", content);

            // Extract the code (use code block language for correct fence detection)
            var code = ExtractCode(content, GetCodeBlockLanguageForPrompt(langKey));

            // Try to determine the target filename
            var targetFile = DetermineTargetFile(request, code, effectiveLang);

            // Update project state
            if (request.ProjectState != null)
            {
                request.ProjectState.Codebase[targetFile] = code;
            }

            LogAction(request.ProjectState, "CodeGeneration", request.Input, $"Generated {code.Split('\n').Length} lines -> {targetFile}");

            _logger?.LogInformation("[Coder] Generated {Lines} lines of code for {File}",
                code.Split('\n').Length, targetFile);

            return new AgentResponse
            {
                Success = true,
                Output = code,
                Data = new Dictionary<string, object>
                {
                    ["filename"] = targetFile,
                    ["code"] = code,
                    ["language"] = effectiveLang,
                    ["is_new_file"] = true
                },
                TokensUsed = tokens
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[Coder] Error during code generation");
            return new AgentResponse
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    /// <summary>
    /// Modify existing code (modification mode)
    /// </summary>
    private async Task<AgentResponse> RunModificationAsync(AgentRequest request, CancellationToken cancellationToken)
    {
        var effectiveLang = GetEffectiveLanguage(request);
        _logger?.LogInformation("[Coder] Starting code MODIFICATION for task (language: {Lang}): {Task}",
            effectiveLang, request.Input.Length > 100 ? request.Input[..100] + "..." : request.Input);

        try
        {
            // Get the current file content
            var currentContent = request.Context != null && request.Context.TryGetValue("current_content", out var content) ? content : null;
            var targetFile = request.Context != null && request.Context.TryGetValue("target_file", out var tf) ? tf : null;
            var fullPath = request.Context != null && request.Context.TryGetValue("full_path", out var fp) ? fp : null;

            if (string.IsNullOrEmpty(currentContent))
            {
                return new AgentResponse
                {
                    Success = false,
                    Error = "Modification task requires current_content in context"
                };
            }

            var userPrompt = BuildModificationPrompt(request, request.Input, currentContent, targetFile, effectiveLang);

            // Optional codebase context for modification (same per-language context)
            if (request.Context != null && request.Context.TryGetValue("codebase_context", out var codebaseCtx) && !string.IsNullOrEmpty(codebaseCtx))
            {
                userPrompt = userPrompt + "\n\nCODEBASE (conventions and structure – follow these when modifying):\n" + codebaseCtx;
            }

            var (responseContent, tokens) = await CallLLMAsync(
                GetModificationSystemPrompt(effectiveLang),
                userPrompt,
                temperature: 0.1f,  // Very low temperature for accurate modifications
                maxTokens: 8000,    // Larger for complete file output
                cancellationToken);

            _logger?.LogInformation("[Coder] Modification response:\n{Content}", responseContent);

            var langKey = GetCoderPromptNameForLanguage(effectiveLang);
            // Extract the modified code
            var modifiedCode = ExtractCode(responseContent, GetCodeBlockLanguageForPrompt(langKey));

            // Update project state with modified code
            if (request.ProjectState != null && !string.IsNullOrEmpty(targetFile))
            {
                request.ProjectState.Codebase[targetFile] = modifiedCode;
            }

            LogAction(request.ProjectState, "CodeModification", request.Input,
                $"Modified {targetFile}: {currentContent.Split('\n').Length} -> {modifiedCode.Split('\n').Length} lines");

            _logger?.LogInformation("[Coder] Modified {File}: {OrigLines} -> {NewLines} lines",
                targetFile, currentContent.Split('\n').Length, modifiedCode.Split('\n').Length);

            return new AgentResponse
            {
                Success = true,
                Output = modifiedCode,
                Data = new Dictionary<string, object>
                {
                    ["filename"] = targetFile ?? "unknown",
                    ["full_path"] = fullPath ?? "",
                    ["code"] = modifiedCode,
                    ["original_code"] = currentContent,
                    ["language"] = effectiveLang,
                    ["is_new_file"] = false,
                    ["is_modification"] = true
                },
                TokensUsed = tokens
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[Coder] Error during code modification");
            return new AgentResponse
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    private string GetModificationSystemPrompt(string? language = null)
    {
        var lang = language ?? _targetLanguage;
        var langKey = GetCoderPromptNameForLanguage(lang);
        var promptName = $"coder-modification-{langKey}";
        return PromptLoader.Instance.LoadPromptRequired(promptName);
    }

    private string BuildModificationPrompt(AgentRequest request, string taskDescription, string currentContent, string? fileName, string? effectiveLang = null)
    {
        var lang = effectiveLang ?? _targetLanguage;
        var langKey = GetCoderPromptNameForLanguage(lang);
        var codeLang = GetCodeBlockLanguageForPrompt(langKey);
        var fileNameLine = string.IsNullOrEmpty(fileName) ? "" : $"File: {fileName}\n";

        string targetClassSection = "";
        string targetClassInstructions = "";
        if (request.Context != null
            && request.Context.TryGetValue("target_class", out var tc) && !string.IsNullOrEmpty(tc)
            && request.Context.TryGetValue("target_class_start_line", out var startLine) && !string.IsNullOrEmpty(startLine)
            && request.Context.TryGetValue("target_class_end_line", out var endLine) && !string.IsNullOrEmpty(endLine))
        {
            targetClassSection = $"## TARGET\n\nModify the class **{tc}** (lines {startLine}–{endLine}).";
            targetClassInstructions = "\n6. Prefer making changes only within the indicated class and line range when possible.";
        }

        return PromptLoader.Instance.LoadPromptRequired("coder-modification-user", new Dictionary<string, string>
        {
            ["TASK"] = taskDescription,
            ["FILE_NAME"] = fileNameLine,
            ["CURRENT_CONTENT"] = currentContent,
            ["CODE_LANG"] = codeLang,
            ["TARGET_CLASS_SECTION"] = targetClassSection,
            ["TARGET_CLASS_INSTRUCTIONS"] = targetClassInstructions
        });
    }

    private string DetermineTargetFile(AgentRequest request, string code, string? effectiveLanguage = null)
    {
        var lang = effectiveLanguage ?? _targetLanguage;
        var langKey = GetCoderPromptNameForLanguage(lang);
        var extension = GetFileExtensionForLanguage(langKey);

        // Check if filename is provided in context
        if (request.Context.TryGetValue("target_file", out var contextFile) && !string.IsNullOrEmpty(contextFile))
            return contextFile;

        // Check subtask target files
        if (request.Context.TryGetValue("task_index", out var taskIndexStr) &&
            int.TryParse(taskIndexStr, out var taskIndex) &&
            request.ProjectState?.Plan.Count > taskIndex - 1)
        {
            var task = request.ProjectState.Plan[taskIndex - 1];
            if (task.TargetFiles.Count > 0)
                return task.TargetFiles[0];
        }

        switch (langKey)
        {
            case "csharp":
                if (code.Contains("static void Main(") || code.Contains("static async Task Main("))
                    return "Program.cs";
                var classMatch = System.Text.RegularExpressions.Regex.Match(code, @"(?:public|internal)\s+(?:static\s+)?class\s+(\w+)");
                if (classMatch.Success) return $"{classMatch.Groups[1].Value}.cs";
                var interfaceMatch = System.Text.RegularExpressions.Regex.Match(code, @"(?:public|internal)\s+interface\s+(\w+)");
                if (interfaceMatch.Success) return $"{interfaceMatch.Groups[1].Value}.cs";
                break;
            case "go":
                if (code.Contains("package main") && (code.Contains("func main()") || code.Contains("func main (")))
                    return "main.go";
                break;
            case "react":
                var reactDefaultMatch = System.Text.RegularExpressions.Regex.Match(code, @"export\s+default\s+function\s+(\w+)");
                if (reactDefaultMatch.Success) return $"{reactDefaultMatch.Groups[1].Value}.tsx";
                var reactCompMatch = System.Text.RegularExpressions.Regex.Match(code, @"(?:function|const)\s+(\w+)\s*[:(]\s*(?:.*?)\s*=>\s*\{", System.Text.RegularExpressions.RegexOptions.Singleline);
                if (reactCompMatch.Success) return $"{reactCompMatch.Groups[1].Value}.tsx";
                extension = ".tsx";
                break;
            case "python":
                if (code.Contains("def main(") || code.Contains("if __name__"))
                    return "main.py";
                if (code.Contains("class "))
                {
                    var classIndex = code.IndexOf("class ");
                    if (classIndex >= 0)
                    {
                        var rest = code[(classIndex + 6)..];
                        var endIndex = rest.IndexOfAny(new[] { '(', ':', ' ', '\n' });
                        if (endIndex > 0)
                        {
                            var className = rest[..endIndex].Trim().ToLowerInvariant();
                            return $"{className}.py";
                        }
                    }
                }
                break;
            case "rust":
                if (code.Contains("fn main()") || code.Contains("fn main ("))
                    return "main.rs";
                var modMatch = System.Text.RegularExpressions.Regex.Match(code, @"pub\s+fn\s+(\w+)\s*\(");
                if (modMatch.Success) return $"{modMatch.Groups[1].Value}.rs";
                break;
        }

        var existingCount = request.ProjectState?.Codebase.Count ?? 0;
        return $"Module{existingCount + 1}{extension}";
    }
}
