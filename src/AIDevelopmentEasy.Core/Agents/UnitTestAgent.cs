using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using AIDevelopmentEasy.Core.Models;
using Microsoft.Extensions.Logging;

namespace AIDevelopmentEasy.Core.Agents;

/// <summary>
/// Agent for running unit tests on deployed code using NUnit Console Runner.
/// Supports parallel execution of multiple test projects and filtering by specific test classes.
/// Designed for .NET Framework projects built with MSBuild.
/// </summary>
public class UnitTestAgent
{
    private readonly ILogger<UnitTestAgent>? _logger;
    private readonly int _maxParallelProjects;
    private readonly TimeSpan _testTimeout;
    private readonly string _configuration;
    private readonly string _nunitConsolePath;

    /// <summary>
    /// Default path to NUnit Console Runner
    /// </summary>
    public const string DefaultNUnitConsolePath = @"C:\ProgramData\AIDevelopmentEasy\tools\NUnit.ConsoleRunner\tools\nunit3-console.exe";

    /// <summary>
    /// Create a new UnitTestAgent
    /// </summary>
    /// <param name="logger">Logger instance</param>
    /// <param name="maxParallelProjects">Maximum number of test projects to run in parallel</param>
    /// <param name="testTimeoutSeconds">Timeout for each test project in seconds</param>
    /// <param name="configuration">Build configuration (default: LocalTest)</param>
    /// <param name="nunitConsolePath">Path to nunit3-console.exe</param>
    public UnitTestAgent(
        ILogger<UnitTestAgent>? logger = null,
        int maxParallelProjects = 4,
        int testTimeoutSeconds = 300,
        string configuration = "LocalTest",
        string? nunitConsolePath = null)
    {
        _logger = logger;
        _maxParallelProjects = maxParallelProjects;
        _testTimeout = TimeSpan.FromSeconds(testTimeoutSeconds);
        _configuration = configuration;
        _nunitConsolePath = nunitConsolePath ?? DefaultNUnitConsolePath;
    }

    /// <summary>
    /// Run tests for all affected test projects in parallel.
    /// Only runs tests for new/modified test classes.
    /// </summary>
    public async Task<TestExecutionSummary> RunTestsAsync(
        CodebaseAnalysis codebaseAnalysis,
        DeploymentResult deploymentResult,
        CancellationToken cancellationToken = default)
    {
        var summary = new TestExecutionSummary
        {
            StartedAt = DateTime.UtcNow
        };

        _logger?.LogInformation("[UnitTestAgent] Starting test execution for deployment");
        _logger?.LogInformation("[UnitTestAgent] Using NUnit Console: {Path}", _nunitConsolePath);
        _logger?.LogInformation("[UnitTestAgent] Configuration: {Config}", _configuration);

        // Verify NUnit Console Runner exists
        if (!File.Exists(_nunitConsolePath))
        {
            _logger?.LogError("[UnitTestAgent] NUnit Console Runner not found at: {Path}", _nunitConsolePath);
            summary.Success = false;
            summary.Error = $"NUnit Console Runner not found at: {_nunitConsolePath}";
            summary.CompletedAt = DateTime.UtcNow;
            return summary;
        }

        try
        {
            // Step 1: Find affected test projects and their new/modified test classes
            var testProjectInfos = FindAffectedTestProjects(codebaseAnalysis, deploymentResult);

            if (testProjectInfos.Count == 0)
            {
                _logger?.LogInformation("[UnitTestAgent] No test projects affected by deployment");
                summary.Skipped = true;
                summary.SkipReason = "No test projects affected";
                summary.CompletedAt = DateTime.UtcNow;
                return summary;
            }

            _logger?.LogInformation("[UnitTestAgent] Found {Count} affected test project(s)", testProjectInfos.Count);

            // Step 2: Run tests for each project in parallel
            var results = new ConcurrentBag<TestProjectResult>();
            var semaphore = new SemaphoreSlim(_maxParallelProjects);

            var tasks = testProjectInfos.Select(async projectInfo =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    var result = await RunTestProjectAsync(projectInfo, cancellationToken);
                    results.Add(result);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);

            // Step 3: Aggregate results
            foreach (var result in results)
            {
                summary.ProjectResults.Add(result);
                summary.TotalTests += result.TotalTests;
                summary.Passed += result.Passed;
                summary.Failed += result.Failed;
                summary.SkippedTestsCount += result.Skipped;
                summary.NewTestsPassed += result.NewTestsPassed;
                summary.NewTestsFailed += result.NewTestsFailed;

                if (result.ExistingTestsFailed > 0)
                {
                    summary.ExistingTestsFailed += result.ExistingTestsFailed;
                    summary.IsBreakingChange = true;
                }

                summary.FailedTests.AddRange(result.FailedTests);
            }

            summary.Success = summary.Failed == 0;
            summary.CompletedAt = DateTime.UtcNow;

            _logger?.LogInformation(
                "[UnitTestAgent] Test execution completed: {Passed}/{Total} passed, {Failed} failed, Breaking: {Breaking}",
                summary.Passed, summary.TotalTests, summary.Failed, summary.IsBreakingChange);

            return summary;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[UnitTestAgent] Test execution failed");
            summary.Success = false;
            summary.Error = ex.Message;
            summary.CompletedAt = DateTime.UtcNow;
            return summary;
        }
    }

    /// <summary>
    /// Find test projects affected by deployment and identify new/modified test classes
    /// </summary>
    private List<TestProjectInfo> FindAffectedTestProjects(
        CodebaseAnalysis codebaseAnalysis,
        DeploymentResult deploymentResult)
    {
        var testProjectInfos = new List<TestProjectInfo>();

        // Find all deployed test files
        var deployedTestFiles = deploymentResult.CopiedFiles
            .Where(f => IsTestFile(f.TargetPath))
            .ToList();

        if (deployedTestFiles.Count == 0)
        {
            _logger?.LogInformation("[UnitTestAgent] No test files were deployed");
            return testProjectInfos;
        }

        _logger?.LogInformation("[UnitTestAgent] Found {Count} deployed test file(s)", deployedTestFiles.Count);

        // Group by test project
        var testProjects = codebaseAnalysis.Projects
            .Where(p => p.IsTestProject)
            .ToList();

        foreach (var testProject in testProjects)
        {
            var projectPath = Path.Combine(codebaseAnalysis.CodebasePath, testProject.RelativePath);
            var projectDir = Path.GetDirectoryName(projectPath) ?? "";

            // Find test files that belong to this project
            var projectTestFiles = deployedTestFiles
                .Where(f => f.TargetPath?.StartsWith(projectDir, StringComparison.OrdinalIgnoreCase) == true)
                .ToList();

            if (projectTestFiles.Count == 0)
                continue;

            // Extract test class names (with namespace) from the deployed files
            var testClassInfos = new List<TestClassInfo>();
            foreach (var testFile in projectTestFiles)
            {
                var classInfos = ExtractTestClassInfos(testFile.TargetPath);
                testClassInfos.AddRange(classInfos);
            }

            if (testClassInfos.Count > 0)
            {
                // Find the DLL path (built with LocalTest configuration)
                var dllPath = FindTestDllPath(projectDir, testProject.Name);

                if (string.IsNullOrEmpty(dllPath))
                {
                    _logger?.LogWarning("[UnitTestAgent] Could not find DLL for project: {Project}", testProject.Name);
                    continue;
                }

                testProjectInfos.Add(new TestProjectInfo
                {
                    ProjectName = testProject.Name,
                    ProjectPath = projectPath,
                    DllPath = dllPath,
                    TestClassInfos = testClassInfos.DistinctBy(c => c.FullName).ToList(),
                    TestClassNames = testClassInfos.Select(c => c.ClassName).Distinct().ToList(),
                    TestFilePaths = projectTestFiles.Select(f => f.TargetPath!).ToList(),
                    TestFramework = TestFramework.NUnit
                });

                _logger?.LogInformation(
                    "[UnitTestAgent] Project {Name}: {Count} test class(es) to run, DLL: {Dll}",
                    testProject.Name, testClassInfos.Count, dllPath);
            }
        }

        return testProjectInfos;
    }

    /// <summary>
    /// Find the test DLL path for a project (LocalTest configuration)
    /// </summary>
    private string? FindTestDllPath(string projectDir, string projectName)
    {
        // Try common output paths for .NET Framework projects
        var possiblePaths = new[]
        {
            Path.Combine(projectDir, "bin", _configuration, $"{projectName}.dll"),
            Path.Combine(projectDir, "bin", _configuration, "net462", $"{projectName}.dll"),
            Path.Combine(projectDir, "bin", _configuration, "net48", $"{projectName}.dll"),
            Path.Combine(projectDir, "bin", "Debug", $"{projectName}.dll"),
            Path.Combine(projectDir, "bin", "Release", $"{projectName}.dll")
        };

        foreach (var path in possiblePaths)
        {
            if (File.Exists(path))
            {
                _logger?.LogDebug("[UnitTestAgent] Found DLL at: {Path}", path);
                return path;
            }
        }

        // Search recursively in bin folder
        var binDir = Path.Combine(projectDir, "bin");
        if (Directory.Exists(binDir))
        {
            var dllFiles = Directory.GetFiles(binDir, $"{projectName}.dll", SearchOption.AllDirectories);
            if (dllFiles.Length > 0)
            {
                // Prefer LocalTest configuration
                var localTestDll = dllFiles.FirstOrDefault(f => f.Contains(_configuration, StringComparison.OrdinalIgnoreCase));
                return localTestDll ?? dllFiles[0];
            }
        }

        return null;
    }

    /// <summary>
    /// Run tests for a specific test project using NUnit Console Runner
    /// </summary>
    private async Task<TestProjectResult> RunTestProjectAsync(
        TestProjectInfo projectInfo,
        CancellationToken cancellationToken)
    {
        var result = new TestProjectResult
        {
            ProjectName = projectInfo.ProjectName,
            ProjectPath = projectInfo.ProjectPath,
            StartedAt = DateTime.UtcNow
        };

        _logger?.LogInformation(
            "[UnitTestAgent] Running tests for {Project} ({Count} classes)",
            projectInfo.ProjectName, projectInfo.TestClassInfos.Count);

        try
        {
            // Build the NUnit filter for specific classes
            var filter = BuildNUnitFilter(projectInfo.TestClassInfos);

            // Run NUnit Console Runner
            var (exitCode, output, error, xmlResultPath) = await RunNUnitConsoleAsync(
                projectInfo.DllPath!, filter, projectInfo.ProjectName, cancellationToken);

            // Parse results from XML output
            if (File.Exists(xmlResultPath))
            {
                ParseNUnitXmlResults(xmlResultPath, result, projectInfo.TestClassInfos);
            }
            else
            {
                // Fallback: parse console output
                ParseNUnitConsoleOutput(output, error, result, projectInfo.TestClassInfos);
            }

            result.Success = exitCode == 0 && result.Failed == 0;
            result.RawOutput = output;
            result.CompletedAt = DateTime.UtcNow;

            _logger?.LogInformation(
                "[UnitTestAgent] {Project}: {Passed} passed, {Failed} failed, {Skipped} skipped",
                projectInfo.ProjectName, result.Passed, result.Failed, result.Skipped);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[UnitTestAgent] Failed to run tests for {Project}", projectInfo.ProjectName);
            result.Success = false;
            result.Error = ex.Message;
            result.CompletedAt = DateTime.UtcNow;
        }

        return result;
    }

    /// <summary>
    /// Run NUnit Console Runner with filter
    /// </summary>
    private async Task<(int ExitCode, string Output, string Error, string XmlResultPath)> RunNUnitConsoleAsync(
        string dllPath,
        string filter,
        string projectName,
        CancellationToken cancellationToken)
    {
        // Create temp directory for results
        var tempDir = Path.Combine(Path.GetTempPath(), "AIDevelopmentEasy", "TestResults");
        Directory.CreateDirectory(tempDir);

        var xmlResultPath = Path.Combine(tempDir, $"{projectName}_{DateTime.Now:yyyyMMdd_HHmmss}.xml");

        var arguments = new StringBuilder();
        arguments.Append($"\"{dllPath}\"");

        // Add filter if specified (to run only specific test classes)
        if (!string.IsNullOrEmpty(filter))
        {
            arguments.Append($" --where \"{filter}\"");
        }

        // Output result XML
        arguments.Append($" --result=\"{xmlResultPath}\"");

        // Additional options
        arguments.Append(" --labels=All");  // Show test names as they run
        arguments.Append(" --noheader");    // Skip header

        var workingDir = Path.GetDirectoryName(dllPath) ?? ".";

        _logger?.LogInformation("[UnitTestAgent] Running: \"{NUnit}\" {Args}", _nunitConsolePath, arguments);
        _logger?.LogInformation("[UnitTestAgent] Working directory: {WorkingDir}", workingDir);

        var startInfo = new ProcessStartInfo
        {
            FileName = _nunitConsolePath,
            Arguments = arguments.ToString(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = workingDir
        };

        using var process = new Process { StartInfo = startInfo };
        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                outputBuilder.AppendLine(e.Data);
                _logger?.LogInformation("[UnitTestAgent] > {Output}", e.Data);
            }
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                errorBuilder.AppendLine(e.Data);
                _logger?.LogWarning("[UnitTestAgent] ! {Error}", e.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(_testTimeout);

        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            throw new TimeoutException($"Test execution timed out after {_testTimeout.TotalSeconds} seconds");
        }

        return (process.ExitCode, outputBuilder.ToString(), errorBuilder.ToString(), xmlResultPath);
    }

    /// <summary>
    /// Build NUnit filter string for specific test classes
    /// Format: class == Namespace.ClassName1 || class == Namespace.ClassName2
    /// </summary>
    private string BuildNUnitFilter(List<TestClassInfo> testClassInfos)
    {
        if (testClassInfos.Count == 0)
            return "";

        // Use full class names (with namespace) for accurate filtering
        var filters = testClassInfos.Select(c => $"class == {c.FullName}");
        return string.Join(" || ", filters);
    }

    /// <summary>
    /// Parse NUnit XML result file
    /// </summary>
    private void ParseNUnitXmlResults(string xmlPath, TestProjectResult result, List<TestClassInfo> newTestClasses)
    {
        try
        {
            var doc = XDocument.Load(xmlPath);
            var testRun = doc.Root;

            if (testRun == null)
                return;

            // Get summary from test-run element
            result.TotalTests = int.TryParse(testRun.Attribute("total")?.Value, out var total) ? total : 0;
            result.Passed = int.TryParse(testRun.Attribute("passed")?.Value, out var passed) ? passed : 0;
            result.Failed = int.TryParse(testRun.Attribute("failed")?.Value, out var failed) ? failed : 0;
            result.Skipped = int.TryParse(testRun.Attribute("skipped")?.Value, out var skipped) ? skipped : 0;

            _logger?.LogInformation("[UnitTestAgent] XML Results - Total: {Total}, Passed: {Passed}, Failed: {Failed}, Skipped: {Skipped}",
                result.TotalTests, result.Passed, result.Failed, result.Skipped);

            // Parse failed test cases
            var failedTestCases = testRun.Descendants("test-case")
                .Where(tc => tc.Attribute("result")?.Value == "Failed");

            foreach (var testCase in failedTestCases)
            {
                var fullName = testCase.Attribute("fullname")?.Value ?? "";
                var className = testCase.Attribute("classname")?.Value ?? "";
                var methodName = testCase.Attribute("name")?.Value ?? "";

                // Check if this is a new test
                var isNewTest = newTestClasses.Any(c =>
                    className.Contains(c.ClassName, StringComparison.OrdinalIgnoreCase) ||
                    fullName.Contains(c.FullName, StringComparison.OrdinalIgnoreCase));

                // Get failure message
                var failure = testCase.Element("failure");
                var message = failure?.Element("message")?.Value ?? "Test failed";
                var stackTrace = failure?.Element("stack-trace")?.Value;

                // Get duration
                var durationStr = testCase.Attribute("duration")?.Value;
                var duration = double.TryParse(durationStr, out var d) ? TimeSpan.FromSeconds(d) : TimeSpan.Zero;

                var testResult = new TestResult
                {
                    ClassName = className,
                    MethodName = methodName,
                    FullName = fullName,
                    Passed = false,
                    IsNewTest = isNewTest,
                    ErrorMessage = message,
                    StackTrace = stackTrace,
                    Duration = duration
                };

                result.FailedTests.Add(testResult);

                if (isNewTest)
                    result.NewTestsFailed++;
                else
                    result.ExistingTestsFailed++;

                _logger?.LogWarning("[UnitTestAgent] FAILED: {FullName} - {Message}", fullName, message);
            }

            // Calculate new tests passed
            var newTestCount = newTestClasses.Count;
            result.NewTestsPassed = Math.Max(0, newTestCount - result.NewTestsFailed);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[UnitTestAgent] Failed to parse NUnit XML results from {Path}", xmlPath);
        }
    }

    /// <summary>
    /// Parse NUnit console output (fallback if XML parsing fails)
    /// </summary>
    private void ParseNUnitConsoleOutput(string output, string error, TestProjectResult result, List<TestClassInfo> newTestClasses)
    {
        // Parse summary: "Test Count: X, Passed: Y, Failed: Z, Skipped: W"
        var totalMatch = Regex.Match(output, @"Test Count:\s*(\d+)", RegexOptions.IgnoreCase);
        var passedMatch = Regex.Match(output, @"Passed:\s*(\d+)", RegexOptions.IgnoreCase);
        var failedMatch = Regex.Match(output, @"Failed:\s*(\d+)", RegexOptions.IgnoreCase);
        var skippedMatch = Regex.Match(output, @"(?:Skipped|Inconclusive):\s*(\d+)", RegexOptions.IgnoreCase);

        if (totalMatch.Success) result.TotalTests = int.Parse(totalMatch.Groups[1].Value);
        if (passedMatch.Success) result.Passed = int.Parse(passedMatch.Groups[1].Value);
        if (failedMatch.Success) result.Failed = int.Parse(failedMatch.Groups[1].Value);
        if (skippedMatch.Success) result.Skipped = int.Parse(skippedMatch.Groups[1].Value);

        // Parse failed tests from output
        // Format: "1) Failed : Namespace.ClassName.MethodName"
        var failedPattern = @"Failed\s*:\s*(\S+)\.(\w+)$";
        var failedMatches = Regex.Matches(output, failedPattern, RegexOptions.Multiline);

        foreach (Match match in failedMatches)
        {
            var fullClassName = match.Groups[1].Value;
            var methodName = match.Groups[2].Value;
            var className = fullClassName.Contains('.') ? fullClassName.Split('.').Last() : fullClassName;

            var isNewTest = newTestClasses.Any(c =>
                className.Contains(c.ClassName, StringComparison.OrdinalIgnoreCase));

            result.FailedTests.Add(new TestResult
            {
                ClassName = className,
                MethodName = methodName,
                FullName = $"{fullClassName}.{methodName}",
                Passed = false,
                IsNewTest = isNewTest,
                ErrorMessage = "Test failed - see output for details"
            });

            if (isNewTest)
                result.NewTestsFailed++;
            else
                result.ExistingTestsFailed++;
        }
    }

    /// <summary>
    /// Check if a file path is a test file
    /// </summary>
    private bool IsTestFile(string? filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            return false;

        var fileName = Path.GetFileName(filePath).ToLowerInvariant();
        var directory = filePath.ToLowerInvariant();

        return fileName.EndsWith("tests.cs") ||
               fileName.EndsWith("test.cs") ||
               fileName.Contains("unittest") ||
               directory.Contains("test") ||
               directory.Contains("unittest");
    }

    /// <summary>
    /// Extract test class information (name + namespace) from a C# file
    /// </summary>
    private List<TestClassInfo> ExtractTestClassInfos(string? filePath)
    {
        var classInfos = new List<TestClassInfo>();

        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            return classInfos;

        try
        {
            var content = File.ReadAllText(filePath);

            // Extract namespace
            var namespaceMatch = Regex.Match(content, @"namespace\s+([\w.]+)");
            var fileNamespace = namespaceMatch.Success ? namespaceMatch.Groups[1].Value : "";

            // Match class declarations with test attributes
            var classPattern = @"(?:\[(?:TestFixture|TestClass)\][\s\S]*?)?(?:public|internal)\s+class\s+(\w+)";
            var matches = Regex.Matches(content, classPattern);

            foreach (Match match in matches)
            {
                var className = match.Groups[1].Value;

                // Check if it's a test class
                if (className.EndsWith("Tests", StringComparison.OrdinalIgnoreCase) ||
                    className.EndsWith("Test", StringComparison.OrdinalIgnoreCase) ||
                    content.Contains("[TestFixture]") ||
                    content.Contains("[TestClass]"))
                {
                    var fullName = string.IsNullOrEmpty(fileNamespace)
                        ? className
                        : $"{fileNamespace}.{className}";

                    classInfos.Add(new TestClassInfo
                    {
                        ClassName = className,
                        Namespace = fileNamespace,
                        FullName = fullName,
                        FilePath = filePath
                    });

                    _logger?.LogDebug("[UnitTestAgent] Found test class: {FullName}", fullName);
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "[UnitTestAgent] Failed to extract test class info from {FilePath}", filePath);
        }

        return classInfos;
    }
}

/// <summary>
/// Information about a test class
/// </summary>
public class TestClassInfo
{
    public string ClassName { get; set; } = "";
    public string Namespace { get; set; } = "";
    public string FullName { get; set; } = "";
    public string FilePath { get; set; } = "";
}

/// <summary>
/// Information about a test project to run
/// </summary>
public class TestProjectInfo
{
    public string ProjectName { get; set; } = "";
    public string ProjectPath { get; set; } = "";
    public string? DllPath { get; set; }
    public List<TestClassInfo> TestClassInfos { get; set; } = new();
    public List<string> TestClassNames { get; set; } = new();
    public List<string> TestFilePaths { get; set; } = new();
    public TestFramework TestFramework { get; set; }
}

/// <summary>
/// Test framework type
/// </summary>
public enum TestFramework
{
    NUnit,
    XUnit,
    MSTest
}

/// <summary>
/// Summary of all test execution
/// </summary>
public class TestExecutionSummary
{
    public bool Success { get; set; }
    public bool Skipped { get; set; }
    public string? SkipReason { get; set; }
    public string? Error { get; set; }

    public int TotalTests { get; set; }
    public int Passed { get; set; }
    public int Failed { get; set; }
    public int SkippedTestsCount { get; set; }

    public int NewTestsPassed { get; set; }
    public int NewTestsFailed { get; set; }
    public int ExistingTestsFailed { get; set; }

    public bool IsBreakingChange { get; set; }

    public List<TestProjectResult> ProjectResults { get; set; } = new();
    public List<TestResult> FailedTests { get; set; } = new();

    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public TimeSpan Duration => CompletedAt.HasValue
        ? CompletedAt.Value - StartedAt
        : TimeSpan.Zero;
}

/// <summary>
/// Result of running tests for a single project
/// </summary>
public class TestProjectResult
{
    public string ProjectName { get; set; } = "";
    public string ProjectPath { get; set; } = "";
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? RawOutput { get; set; }

    public int TotalTests { get; set; }
    public int Passed { get; set; }
    public int Failed { get; set; }
    public int Skipped { get; set; }
    public int NewTestsPassed { get; set; }
    public int NewTestsFailed { get; set; }
    public int ExistingTestsFailed { get; set; }

    public List<TestResult> FailedTests { get; set; } = new();

    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

/// <summary>
/// Individual test result
/// </summary>
public class TestResult
{
    public string ClassName { get; set; } = "";
    public string MethodName { get; set; } = "";
    public string FullName { get; set; } = "";
    public bool Passed { get; set; }
    public bool IsNewTest { get; set; }
    public string? ErrorMessage { get; set; }
    public string? StackTrace { get; set; }
    public TimeSpan Duration { get; set; }
}
