using AIDevelopmentEasy.Core.Agents.Base;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Logging;

namespace AIDevelopmentEasy.Core.Agents;

/// <summary>
/// Coder Agent - Responsible for code generation.
/// Takes subtasks from the Planner and generates source code for each.
/// </summary>
public class CoderAgent : BaseAgent
{
    public override string Name => "Coder";
    public override string Role => "Senior Software Developer - Implements code for given tasks";
    protected override string? PromptFileName => IsCSharp ? "coder-csharp" : "coder-generic";

    private readonly string _targetLanguage;
    private bool IsCSharp => _targetLanguage.ToLower() == "csharp" || _targetLanguage.ToLower() == "c#";

    public CoderAgent(
        OpenAIClient openAIClient,
        string deploymentName,
        string targetLanguage = "csharp",
        ILogger<CoderAgent>? logger = null)
        : base(openAIClient, deploymentName, logger)
    {
        _targetLanguage = targetLanguage;
    }

    protected override string GetSystemPrompt()
    {
        if (IsCSharp)
        {
            return base.GetSystemPrompt();
        }

        // For non-C# languages, use generic prompt with variable substitution
        try
        {
            return Services.PromptLoader.Instance.LoadPrompt(
                "coder-generic",
                new Dictionary<string, string> { ["LANGUAGE"] = _targetLanguage },
                GetFallbackPrompt());
        }
        catch
        {
            return GetFallbackPrompt();
        }
    }

    protected override string GetFallbackPrompt()
    {
        if (IsCSharp)
        {
            return @"You are a Senior C# Developer Agent specializing in .NET Framework 4.6.2. Your job is to write clean, efficient, and well-documented code.

Your responsibilities:
1. Implement the given task completely
2. Follow C# and .NET best practices
3. Add XML documentation comments
4. Handle exceptions gracefully
5. Write modular, reusable code

Guidelines:
- Target .NET Framework 4.6.2 (NOT .NET Core or .NET 5+)
- Use meaningful variable and method names (PascalCase for public, camelCase for private)
- Keep methods small and focused (Single Responsibility Principle)
- Use proper C# conventions (properties, events, etc.)
- Include necessary using statements at the top
- Use explicit types (avoid var when type is not obvious)
- Implement IDisposable when managing unmanaged resources

Namespace Handling (CRITICAL):
- When a target namespace is specified, ALWAYS use it exactly as provided
- The namespace should match the folder structure (e.g., Picus.Common.Helpers for Helpers/ folder)
- Do NOT invent or modify the namespace

.NET Framework 4.6.2 Specific:
- Use System.Net.Http.HttpClient (not HttpClientFactory)
- Use Task-based async/await patterns
- Use System.IO for file operations
- Use Newtonsoft.Json for JSON (NOT System.Text.Json)
- Console apps should have static void Main() or static async Task Main()

Modern C# Features (supported via MSBuild with latest LangVersion):
- Use nameof() operator for parameter names
- Use string interpolation ($"")
- Use null-conditional operators (?. and ??)
- Use expression-bodied members where appropriate
- Use auto-property initializers
- Use pattern matching where helpful

Testing with NUnit and FluentAssertions:
- Use NUnit framework: using NUnit.Framework;
- Use FluentAssertions: using FluentAssertions;
- Mark test classes with [TestFixture] attribute
- Mark test methods with [Test] attribute
- Use [TestCase(arg1, arg2)] for parameterized tests
- Use FluentAssertions: result.Should().Be(expected), result.Should().NotBeNull(), action.Should().Throw<T>()
- Follow Arrange-Act-Assert pattern
- Naming: MethodName_Scenario_ExpectedResult
- For console demo apps, use static void Main() with Console.WriteLine

Output Format:
- Output ONLY the code in a markdown code block
- Include ALL necessary using statements
- If it's a class, include the full class definition WITH CORRECT NAMESPACE
- If modifying existing code, output the complete updated file

```csharp
using System;

namespace ProjectName.FolderName  // Use the provided namespace exactly
{
    /// <summary>
    /// XML documentation
    /// </summary>
    public class ClassName
    {
        // Your implementation here
    }
}
```

IMPORTANT: Output ONLY code in a single code block. No explanations before or after unless as code comments.";
        }

        // Generic language fallback
        return $@"You are a Senior {_targetLanguage.ToUpper()} Developer Agent. Your job is to write clean, efficient, and well-documented code.

Your responsibilities:
1. Implement the given task completely
2. Follow best practices and coding standards
3. Add appropriate comments and docstrings
4. Handle edge cases and errors gracefully
5. Write modular, reusable code

Guidelines:
- Use meaningful variable and function names
- Keep functions small and focused
- Include type hints where applicable
- Consider performance implications
- Follow {_targetLanguage} conventions and idioms

Output Format:
- Output ONLY the code
- Use markdown code blocks with language identifier
- Include necessary imports at the top
- If modifying existing code, output the complete updated file

Example:
```{_targetLanguage}
# Your code here
```

IMPORTANT: Output ONLY code in a single code block. No explanations before or after unless as code comments.";
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
        _logger?.LogInformation("[Coder] Starting code generation for task: {Task}",
            request.Input.Length > 100 ? request.Input[..100] + "..." : request.Input);

        try
        {
            // Build context from existing codebase
            var contextSection = "";
            if (request.ProjectState?.Codebase.Count > 0)
            {
                contextSection = "\n\nEXISTING CODEBASE:\n";
                foreach (var (fname, existingCode) in request.ProjectState.Codebase)
                {
                    contextSection += $"\n--- {fname} ---\n{existingCode}\n";
                }
            }

            // Get any additional context (like plan summary)
            var planContext = "";
            if (request.Context.TryGetValue("plan_summary", out var summary))
            {
                planContext = $"\n\nPROJECT CONTEXT:\n{summary}\n";
            }

            // Get target namespace - CRITICAL for correct code generation
            var namespaceSection = "";
            if (request.Context.TryGetValue("target_namespace", out var targetNs) && !string.IsNullOrEmpty(targetNs))
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
            if (request.Context.TryGetValue("project_name", out var projectName) && !string.IsNullOrEmpty(projectName))
            {
                projectContext = $"\nTARGET PROJECT: {projectName}\n";
            }

            var userPrompt = $@"Please implement the following task:

TASK:
{request.Input}
{projectContext}{namespaceSection}{planContext}{contextSection}
Generate the complete code for this task. Make sure to:
1. Include all necessary imports/using statements
2. Use the EXACT namespace specified above (if provided)
3. Handle potential errors
4. Add helpful comments
5. Follow best practices

Output the code in a markdown code block.";

            var (content, tokens) = await CallLLMAsync(
                BuildSystemPromptWithStandards(request.ProjectState),
                userPrompt,
                temperature: 0.2f,  // Lower temperature for more deterministic code
                maxTokens: 4000,
                cancellationToken);

            _logger?.LogInformation("[Coder] Raw response:\n{Content}", content);

            // Extract the code
            var code = ExtractCode(content, _targetLanguage);

            // Try to determine the target filename
            var targetFile = DetermineTargetFile(request, code);

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
                    ["language"] = _targetLanguage,
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
        _logger?.LogInformation("[Coder] Starting code MODIFICATION for task: {Task}",
            request.Input.Length > 100 ? request.Input[..100] + "..." : request.Input);

        try
        {
            // Get the current file content
            var currentContent = request.Context.TryGetValue("current_content", out var content) ? content : null;
            var targetFile = request.Context.TryGetValue("target_file", out var tf) ? tf : null;
            var fullPath = request.Context.TryGetValue("full_path", out var fp) ? fp : null;

            if (string.IsNullOrEmpty(currentContent))
            {
                return new AgentResponse
                {
                    Success = false,
                    Error = "Modification task requires current_content in context"
                };
            }

            var userPrompt = BuildModificationPrompt(request.Input, currentContent, targetFile);

            var (responseContent, tokens) = await CallLLMAsync(
                GetModificationSystemPrompt(),
                userPrompt,
                temperature: 0.1f,  // Very low temperature for accurate modifications
                maxTokens: 8000,    // Larger for complete file output
                cancellationToken);

            _logger?.LogInformation("[Coder] Modification response:\n{Content}", responseContent);

            // Extract the modified code
            var modifiedCode = ExtractCode(responseContent, _targetLanguage);

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
                    ["language"] = _targetLanguage,
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

    private string GetModificationSystemPrompt()
    {
        return @"You are a **Senior Code Modification Specialist**. Your task is to modify existing source code files according to specific requirements while preserving all existing functionality.

## Critical Rules

1. **PRESERVE ALL EXISTING CODE**: Do not remove or change any existing code unless specifically instructed to
2. **COMPLETE FILE OUTPUT**: Always output the ENTIRE modified file, not just the changes
3. **MAINTAIN FORMATTING**: Keep the same indentation, spacing, and style as the original file
4. **KEEP IMPORTS**: Preserve all existing using/import statements, add new ones if needed
5. **NO COMMENTS ABOUT CHANGES**: Don't add comments like ""// Modified"" or ""// Added"" - just make the changes

## Modification Guidelines

When adding new code:
- Add new methods after existing methods in the same class
- Add new properties after existing properties
- Add new using statements at the top, alphabetically sorted
- Follow the naming conventions visible in the existing code

When updating existing code:
- Keep the same method signature unless explicitly asked to change it
- Preserve all existing functionality
- Update only the specific parts mentioned in the task

## Output Format

Output ONLY the complete modified file in a code block:

```csharp
// The complete file with all modifications applied
// Including ALL original code plus changes
```

**IMPORTANT**: 
- Output ONLY code in a single code block
- The code block must contain the ENTIRE file, not just changed parts
- No explanations before or after the code block";
    }

    private string BuildModificationPrompt(string taskDescription, string currentContent, string? fileName)
    {
        return $@"# MODIFICATION TASK

{taskDescription}

# CURRENT FILE CONTENT
{(string.IsNullOrEmpty(fileName) ? "" : $"File: {fileName}\n")}
```csharp
{currentContent}
```

# INSTRUCTIONS

1. Read the current file content above carefully
2. Apply the modifications described in the task
3. Output the COMPLETE modified file (not just the changes)
4. Preserve ALL existing code that is not being modified
5. Maintain the same code style and formatting

Output the complete modified file in a markdown code block.";
    }

    private string DetermineTargetFile(AgentRequest request, string code)
    {
        var isCSharp = _targetLanguage.ToLower() == "csharp" || _targetLanguage.ToLower() == "c#";
        var extension = isCSharp ? ".cs" : ".py";

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

        if (isCSharp)
        {
            // Try to infer from C# code content
            if (code.Contains("static void Main(") || code.Contains("static async Task Main("))
                return "Program.cs";

            // Try to extract class name
            var classMatch = System.Text.RegularExpressions.Regex.Match(code, @"(?:public|internal)\s+(?:static\s+)?class\s+(\w+)");
            if (classMatch.Success)
            {
                return $"{classMatch.Groups[1].Value}.cs";
            }

            // Try to extract interface name
            var interfaceMatch = System.Text.RegularExpressions.Regex.Match(code, @"(?:public|internal)\s+interface\s+(\w+)");
            if (interfaceMatch.Success)
            {
                return $"{interfaceMatch.Groups[1].Value}.cs";
            }
        }
        else
        {
            // Python inference
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
                        var className = rest[..endIndex].ToLower();
                        return $"{className}.py";
                    }
                }
            }
        }

        // Default filename based on task number
        var existingCount = request.ProjectState?.Codebase.Count ?? 0;
        return $"Module{existingCount + 1}{extension}";
    }
}
