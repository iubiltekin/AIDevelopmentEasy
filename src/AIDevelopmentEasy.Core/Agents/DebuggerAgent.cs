using System.Diagnostics;
using System.Text;
using AIDevelopmentEasy.Core.Agents.Base;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Logging;

namespace AIDevelopmentEasy.Core.Agents;

/// <summary>
/// Debugger Agent - Responsible for testing and error correction.
/// Executes code, catches errors, and iteratively fixes them.
/// Supports both Python and C# (.NET Framework 4.6.2)
/// </summary>
public class DebuggerAgent : BaseAgent
{
    public override string Name => "Debugger";
    public override string Role => "Code Debugging Specialist - Tests code and fixes errors";
    protected override string? PromptFileName => IsCSharp ? "debugger-csharp" : "debugger-generic";

    private readonly int _maxRetries;
    private readonly string _targetLanguage;

    public DebuggerAgent(
        OpenAIClient openAIClient,
        string deploymentName,
        int maxRetries = 3,
        string targetLanguage = "csharp",
        ILogger<DebuggerAgent>? logger = null)
        : base(openAIClient, deploymentName, logger)
    {
        _maxRetries = maxRetries;
        _targetLanguage = targetLanguage;
    }

    private bool IsCSharp => _targetLanguage.ToLower() == "csharp" || _targetLanguage.ToLower() == "c#";

    protected override string GetFallbackPrompt()
    {
        if (IsCSharp)
        {
            return @"You are a C# Code Debugging Agent specializing in .NET Framework 4.6.2. Your job is to identify and fix bugs in code.

Your responsibilities:
1. Analyze compiler errors and runtime exceptions
2. Identify the root cause of bugs
3. Propose minimal, targeted fixes
4. Ensure fixes don't introduce new bugs

Guidelines:
- Make the smallest change necessary to fix the issue
- Don't refactor or change working code
- Preserve the original code structure and style
- Add proper exception handling if missing
- Consider edge cases
- Ensure .NET Framework 4.6.2 compatibility

Common .NET Framework 4.6.2 Issues:
- Missing using statements
- Incorrect namespaces
- Missing assembly references
- Async/await issues (use ConfigureAwait(false) where appropriate)
- Null reference exceptions (add null checks)

Modern C# Features (supported via MSBuild with latest LangVersion):
- nameof() operator is fully supported
- String interpolation ($"") is supported
- Null-conditional operators (?. and ??) are supported
- Expression-bodied members are supported
- Auto-property initializers are supported
- Pattern matching is supported

MSTest Support:
- MSTest.TestFramework NuGet package is automatically added
- [TestClass] and [TestMethod] attributes are supported
- Assert class methods are available

When given code and an error:
1. First explain what the error means (1-2 sentences)
2. Identify the specific line/method causing it
3. Provide the COMPLETE fixed code

Output Format:
```csharp
using System;
// Complete fixed code here
```

IMPORTANT: Always output the COMPLETE fixed file, not just the changed lines.";
        }

        // Generic language fallback
        return @"You are a Code Debugging Agent. Your job is to identify and fix bugs in code.

Your responsibilities:
1. Analyze error messages and tracebacks
2. Identify the root cause of bugs
3. Propose minimal, targeted fixes
4. Ensure fixes don't introduce new bugs

Guidelines:
- Make the smallest change necessary to fix the issue
- Don't refactor or change working code
- Preserve the original code structure and style
- Add error handling if missing
- Consider edge cases

When given code and an error:
1. First explain what the error means (1-2 sentences)
2. Identify the specific line/function causing it
3. Provide the COMPLETE fixed code

Output Format:
```python
# Complete fixed code here
```

IMPORTANT: Always output the COMPLETE fixed file, not just the changed lines.";
    }

    protected override string GetSystemPrompt()
    {
        return base.GetSystemPrompt();
    }

    public override async Task<AgentResponse> RunAsync(AgentRequest request, CancellationToken cancellationToken = default)
    {
        var code = request.Input;
        var filename = request.Context.GetValueOrDefault("filename", "code.py");

        _logger?.LogInformation("[Debugger] Starting debug cycle for: {Filename}", filename);

        var currentCode = code;
        var attempts = 0;
        var allLogs = new List<ExecutionLog>();

        while (attempts < _maxRetries)
        {
            attempts++;
            _logger?.LogInformation("[Debugger] Attempt {Attempt}/{Max}", attempts, _maxRetries);

            // Execute the code
            var (success, output, error) = await ExecuteCodeAsync(currentCode, cancellationToken);

            allLogs.Add(new ExecutionLog
            {
                Code = currentCode,
                Success = success,
                Output = output,
                Error = error
            });

            if (success)
            {
                _logger?.LogInformation("[Debugger] Code executed successfully!");

                // Update project state
                if (request.ProjectState != null)
                {
                    request.ProjectState.Codebase[filename] = currentCode;
                    request.ProjectState.ExecutionLogs.AddRange(allLogs);
                }

                LogAction(request.ProjectState, "DebugSuccess", $"File: {filename}", $"Passed after {attempts} attempt(s)");

                return new AgentResponse
                {
                    Success = true,
                    Output = currentCode,
                    Data = new Dictionary<string, object>
                    {
                        ["filename"] = filename,
                        ["code"] = currentCode,
                        ["attempts"] = attempts,
                        ["execution_output"] = output ?? ""
                    }
                };
            }

            // Code failed - try to fix it
            _logger?.LogWarning("[Debugger] Execution failed. Error: {Error}", error);

            var fixResponse = await TryFixCodeAsync(currentCode, error ?? "Unknown error", cancellationToken);

            if (!fixResponse.Success)
            {
                _logger?.LogError("[Debugger] Failed to generate fix");
                break;
            }

            var fixedCode = fixResponse.Output;

            // Check if the fix is the same as before (avoid infinite loops)
            if (fixedCode.Trim() == currentCode.Trim())
            {
                _logger?.LogWarning("[Debugger] Fix is identical to previous code. Breaking loop.");
                break;
            }

            currentCode = fixedCode;
        }

        // Failed after all retries
        _logger?.LogError("[Debugger] Failed to fix code after {Attempts} attempts", attempts);

        if (request.ProjectState != null)
        {
            request.ProjectState.ExecutionLogs.AddRange(allLogs);
        }

        LogAction(request.ProjectState, "DebugFailed", $"File: {filename}", $"Failed after {attempts} attempts");

        return new AgentResponse
        {
            Success = false,
            Output = currentCode,
            Error = $"Failed to fix code after {attempts} attempts",
            Data = new Dictionary<string, object>
            {
                ["filename"] = filename,
                ["code"] = currentCode,
                ["attempts"] = attempts,
                ["logs"] = allLogs
            }
        };
    }

    private async Task<(bool Success, string? Output, string? Error)> ExecuteCodeAsync(
        string code,
        CancellationToken cancellationToken)
    {
        if (IsCSharp)
        {
            return await ExecuteCSharpCodeAsync(code, cancellationToken);
        }
        return await ExecutePythonCodeAsync(code, cancellationToken);
    }

    private async Task<(bool Success, string? Output, string? Error)> ExecuteCSharpCodeAsync(
        string code,
        CancellationToken cancellationToken)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"aideveasy_cs_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var sourceFile = Path.Combine(tempDir, "Program.cs");
            var projectFile = Path.Combine(tempDir, "TempProject.csproj");
            var exeFile = Path.Combine(tempDir, "bin", "Debug", "TempProject.exe");

            // Write source code
            await File.WriteAllTextAsync(sourceFile, code, cancellationToken);

            // Detect test frameworks
            bool usesNUnit = code.Contains("NUnit.Framework") ||
                             code.Contains("[TestFixture]") ||
                             code.Contains("[Test]") ||
                             code.Contains("[TestCase");
            
            bool usesMSTest = code.Contains("Microsoft.VisualStudio.TestTools.UnitTesting") ||
                              code.Contains("[TestClass]") ||
                              code.Contains("[TestMethod]");
            
            bool usesFluentAssertions = code.Contains("FluentAssertions") ||
                                        code.Contains(".Should()");

            // Detect if code uses Newtonsoft.Json
            bool usesNewtonsoft = code.Contains("Newtonsoft.Json") ||
                                  code.Contains("JsonConvert");
            
            bool isTestProject = usesNUnit || usesMSTest;

            // Create .csproj file for .NET Framework 4.6.2 with NuGet support
            var csprojContent = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<Project ToolsVersion=""15.0"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
  <Import Project=""$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props"" Condition=""Exists('$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props')"" />
  <PropertyGroup>
    <Configuration Condition="" '$(Configuration)' == '' "">Debug</Configuration>
    <Platform Condition="" '$(Platform)' == '' "">AnyCPU</Platform>
    <OutputType>{(isTestProject ? "Library" : "Exe")}</OutputType>
    <RootNamespace>TempProject</RootNamespace>
    <AssemblyName>TempProject</AssemblyName>
    <TargetFrameworkVersion>v4.6.2</TargetFrameworkVersion>
    <LangVersion>latest</LangVersion>
    <AutoGenerateBindingRedirects>true</AutoGenerateBindingRedirects>
    <RestoreProjectStyle>PackageReference</RestoreProjectStyle>
  </PropertyGroup>
  <PropertyGroup Condition="" '$(Configuration)|$(Platform)' == 'Debug|AnyCPU' "">
    <DebugSymbols>true</DebugSymbols>
    <DebugType>full</DebugType>
    <Optimize>false</Optimize>
    <OutputPath>bin\Debug\</OutputPath>
    <DefineConstants>DEBUG;TRACE</DefineConstants>
    <ErrorReport>prompt</ErrorReport>
    <WarningLevel>4</WarningLevel>
  </PropertyGroup>
  <ItemGroup>
    <Reference Include=""System"" />
    <Reference Include=""System.Core"" />
    <Reference Include=""System.IO.Compression"" />
    <Reference Include=""System.IO.Compression.FileSystem"" />
    <Reference Include=""System.Net.Http"" />
    <Reference Include=""Microsoft.CSharp"" />
  </ItemGroup>
  <ItemGroup>
    <Compile Include=""Program.cs"" />
  </ItemGroup>
  <ItemGroup>
    {(usesNUnit ? @"<PackageReference Include=""NUnit"" Version=""3.14.0"" />" : "")}
    {(usesNUnit ? @"<PackageReference Include=""NUnit3TestAdapter"" Version=""4.5.0"" />" : "")}
    {(usesMSTest ? @"<PackageReference Include=""MSTest.TestFramework"" Version=""3.1.1"" />" : "")}
    {(usesFluentAssertions ? @"<PackageReference Include=""FluentAssertions"" Version=""6.12.0"" />" : "")}
    {(usesNewtonsoft ? @"<PackageReference Include=""Newtonsoft.Json"" Version=""13.0.3"" />" : "")}
  </ItemGroup>
  <Import Project=""$(MSBuildToolsPath)\Microsoft.CSharp.targets"" />
</Project>";

            await File.WriteAllTextAsync(projectFile, csprojContent, cancellationToken);

            // Find MSBuild
            var msbuildPath = FindMSBuildPath();
            if (string.IsNullOrEmpty(msbuildPath))
            {
                _logger?.LogWarning("[Debugger] MSBuild not found, performing syntax check only");
                return await Task.FromResult(ValidateCSharpSyntax(code));
            }

            // Compile with MSBuild
            var compileResult = await CompileWithMSBuildAsync(msbuildPath, projectFile, cancellationToken);
            if (!compileResult.Success)
            {
                return (false, null, compileResult.Error);
            }

            // Execute
            return await RunExecutableAsync(exeFile, cancellationToken);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    private string? FindMSBuildPath()
    {
        // Try to find MSBuild in common locations
        var possiblePaths = new[]
        {
            // Visual Studio 2022
            @"C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe",
            @"C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe",
            @"C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe",
            @"C:\Program Files\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe",
            // Visual Studio 2019
            @"C:\Program Files (x86)\Microsoft Visual Studio\2019\Enterprise\MSBuild\Current\Bin\MSBuild.exe",
            @"C:\Program Files (x86)\Microsoft Visual Studio\2019\Professional\MSBuild\Current\Bin\MSBuild.exe",
            @"C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe",
            @"C:\Program Files (x86)\Microsoft Visual Studio\2019\BuildTools\MSBuild\Current\Bin\MSBuild.exe",
            // .NET Framework MSBuild
            @"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe",
            @"C:\Windows\Microsoft.NET\Framework\v4.0.30319\MSBuild.exe",
        };

        foreach (var path in possiblePaths)
        {
            if (File.Exists(path))
            {
                _logger?.LogDebug("[Debugger] Found MSBuild at: {Path}", path);
                return path;
            }
        }

        // Try to find via vswhere
        try
        {
            var vswherePath = @"C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe";
            if (File.Exists(vswherePath))
            {
                var psi = new ProcessStartInfo
                {
                    FileName = vswherePath,
                    Arguments = "-latest -requires Microsoft.Component.MSBuild -find MSBuild\\**\\Bin\\MSBuild.exe",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process != null)
                {
                    var output = process.StandardOutput.ReadToEnd().Trim();
                    process.WaitForExit(5000);

                    if (!string.IsNullOrEmpty(output) && File.Exists(output.Split('\n')[0].Trim()))
                    {
                        var foundPath = output.Split('\n')[0].Trim();
                        _logger?.LogDebug("[Debugger] Found MSBuild via vswhere: {Path}", foundPath);
                        return foundPath;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogDebug("[Debugger] vswhere failed: {Error}", ex.Message);
        }

        return null;
    }

    private async Task<(bool Success, string? Output, string? Error)> CompileWithMSBuildAsync(
        string msbuildPath,
        string projectFile,
        CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo
        {
            FileName = msbuildPath,
            Arguments = $"\"{projectFile}\" /t:Restore;Build /p:Configuration=Debug /nologo /v:minimal",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi };
        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        process.OutputDataReceived += (_, e) => { if (e.Data != null) outputBuilder.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data != null) errorBuilder.AppendLine(e.Data); };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        var completed = await Task.Run(() => process.WaitForExit(120000), cancellationToken);

        if (!completed)
        {
            try { process.Kill(); } catch { }
            return (false, null, "Compilation timed out (120 seconds)");
        }

        var output = outputBuilder.ToString().Trim();
        var error = errorBuilder.ToString().Trim();
        var allOutput = string.Join("\n", output, error).Trim();

        if (process.ExitCode != 0 || allOutput.Contains("error CS") || allOutput.Contains("error MSB"))
        {
            return (false, null, $"Compilation failed:\n{allOutput}");
        }

        _logger?.LogInformation("[Debugger] MSBuild compilation successful");
        return (true, "Compilation successful", null);
    }

    private async Task<(bool Success, string? Output, string? Error)> RunExecutableAsync(
        string exePath,
        CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo
        {
            FileName = exePath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi };
        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        process.OutputDataReceived += (_, e) => { if (e.Data != null) outputBuilder.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data != null) errorBuilder.AppendLine(e.Data); };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        var completed = await Task.Run(() => process.WaitForExit(30000), cancellationToken);

        if (!completed)
        {
            try { process.Kill(); } catch { }
            return (false, null, "Execution timed out (30 seconds)");
        }

        var output = outputBuilder.ToString().Trim();
        var error = errorBuilder.ToString().Trim();

        if (process.ExitCode == 0)
        {
            return (true, string.IsNullOrEmpty(output) ? "Executed successfully (no output)" : output, null);
        }

        return (false, output, string.IsNullOrEmpty(error) ? $"Exit code: {process.ExitCode}" : error);
    }

    private (bool Success, string? Output, string? Error) ValidateCSharpSyntax(string code)
    {
        // Basic syntax validation - MSBuild not available
        var issues = new List<string>();

        if (!code.Contains("using System"))
            issues.Add("Warning: Missing 'using System;' directive");

        if (!code.Contains("class ") && !code.Contains("struct ") && !code.Contains("interface "))
            issues.Add("Warning: No class, struct, or interface definition found");

        if (code.Contains("static void Main(") || code.Contains("static async Task Main("))
        {
            // It's a console app, looks okay
            return (true, "Syntax appears valid (MSBuild not available for full compilation)", null);
        }

        if (issues.Count > 0)
        {
            return (false, null, string.Join("\n", issues));
        }

        return (true, "Syntax appears valid (MSBuild not available for full compilation)", null);
    }

    private async Task<(bool Success, string? Output, string? Error)> ExecutePythonCodeAsync(
        string code,
        CancellationToken cancellationToken)
    {
        try
        {
            var tempFile = Path.Combine(Path.GetTempPath(), $"aideveasy_debug_{Guid.NewGuid():N}.py");
            await File.WriteAllTextAsync(tempFile, code, cancellationToken);

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "python",
                    Arguments = $"\"{tempFile}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = new Process { StartInfo = psi };
                var outputBuilder = new StringBuilder();
                var errorBuilder = new StringBuilder();

                process.OutputDataReceived += (_, e) => { if (e.Data != null) outputBuilder.AppendLine(e.Data); };
                process.ErrorDataReceived += (_, e) => { if (e.Data != null) errorBuilder.AppendLine(e.Data); };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                var completed = await Task.Run(() => process.WaitForExit(30000), cancellationToken);

                if (!completed)
                {
                    try { process.Kill(); } catch { }
                    return (false, null, "Execution timed out (30 seconds)");
                }

                var output = outputBuilder.ToString().Trim();
                var error = errorBuilder.ToString().Trim();

                if (process.ExitCode == 0 && string.IsNullOrEmpty(error))
                {
                    return (true, output, null);
                }

                return (false, output, string.IsNullOrEmpty(error) ? $"Exit code: {process.ExitCode}" : error);
            }
            finally
            {
                try { File.Delete(tempFile); } catch { }
            }
        }
        catch (Exception ex)
        {
            return (false, null, $"Execution error: {ex.Message}");
        }
    }

    private async Task<AgentResponse> TryFixCodeAsync(string code, string error, CancellationToken cancellationToken)
    {
        var lang = IsCSharp ? "csharp" : "python";
        var userPrompt = $@"The following code has an error. Please fix it.

CODE:
```{lang}
{code}
```

ERROR:
{error}

Analyze the error and provide the COMPLETE fixed code. Only output the fixed code in a code block.";

        try
        {
            var (content, tokens) = await CallLLMAsync(
                GetSystemPrompt(),
                userPrompt,
                temperature: 0.2f,
                maxTokens: 4000,
                cancellationToken);

            _logger?.LogDebug("[Debugger] Fix response:\n{Content}", content);

            var fixedCode = ExtractCode(content, lang);

            return new AgentResponse
            {
                Success = true,
                Output = fixedCode,
                TokensUsed = tokens
            };
        }
        catch (Exception ex)
        {
            return new AgentResponse
            {
                Success = false,
                Error = ex.Message
            };
        }
    }
}
