using Azure;
using Azure.AI.OpenAI;
using AIDevelopmentEasy.Api.Hubs;
using AIDevelopmentEasy.Api.Repositories.FileSystem;
using AIDevelopmentEasy.Api.Repositories.Interfaces;
using AIDevelopmentEasy.Api.Services;
using AIDevelopmentEasy.Api.Services.Interfaces;
using AIDevelopmentEasy.Core.Agents;
using AIDevelopmentEasy.Core.Services;
using Microsoft.Extensions.FileProviders;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ════════════════════════════════════════════════════════════════════════════
// Windows Service Support
// ════════════════════════════════════════════════════════════════════════════
builder.Host.UseWindowsService(options =>
{
    options.ServiceName = "AIDevelopmentEasy";
});

// When running as a service, use the executable's directory as content root
if (WindowsServiceHelpers.IsWindowsService())
{
    var exePath = Environment.ProcessPath;
    if (!string.IsNullOrEmpty(exePath))
    {
        var exeDir = Path.GetDirectoryName(exePath);
        if (!string.IsNullOrEmpty(exeDir))
        {
            builder.Host.UseContentRoot(exeDir);
        }
    }
}

// ════════════════════════════════════════════════════════════════════════════
// Configuration
// ════════════════════════════════════════════════════════════════════════════
builder.Configuration
    .AddJsonFile("appsettings.json", optional: true)
    .AddJsonFile("appsettings.Local.json", optional: true)
    .AddEnvironmentVariables();

// ════════════════════════════════════════════════════════════════════════════
// Serilog
// ════════════════════════════════════════════════════════════════════════════
var logPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
    "AIDevelopmentEasy", "logs", "api-.log");

Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.File(logPath, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 7)
    .CreateLogger();

builder.Host.UseSerilog();

// ════════════════════════════════════════════════════════════════════════════
// Path Configuration - All data stored in ProgramData
// ════════════════════════════════════════════════════════════════════════════
var programDataDir = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
var appDataDir = Path.Combine(programDataDir, "AIDevelopmentEasy");

var requirementsPath = Path.Combine(appDataDir, "requirements");
var outputPath = Path.Combine(appDataDir, "output");
var promptsPath = Path.Combine(appDataDir, "prompts");
var codebasesPath = Path.Combine(appDataDir, "codebases");

// Ensure all directories exist
Directory.CreateDirectory(appDataDir);
Directory.CreateDirectory(requirementsPath);
Directory.CreateDirectory(outputPath);
Directory.CreateDirectory(promptsPath);
Directory.CreateDirectory(codebasesPath);

// Copy default prompts if prompts directory is empty
CopyDefaultPromptsIfNeeded(promptsPath);

// Initialize PromptLoader
PromptLoader.Initialize(promptsPath);

// ════════════════════════════════════════════════════════════════════════════
// Azure OpenAI Configuration
// ════════════════════════════════════════════════════════════════════════════
var azureEndpoint = builder.Configuration["AzureOpenAI:Endpoint"] 
    ?? throw new InvalidOperationException("AzureOpenAI:Endpoint not configured");
var azureApiKey = builder.Configuration["AzureOpenAI:ApiKey"] 
    ?? throw new InvalidOperationException("AzureOpenAI:ApiKey not configured");
var deploymentName = builder.Configuration["AzureOpenAI:DeploymentName"] ?? "gpt-4o";
var targetLanguage = builder.Configuration["AIDevelopmentEasy:TargetLanguage"] ?? "csharp";
var debugMaxRetries = int.Parse(builder.Configuration["AIDevelopmentEasy:DebugMaxRetries"] ?? "3");

var openAIClient = new OpenAIClient(new Uri(azureEndpoint), new AzureKeyCredential(azureApiKey));

// ════════════════════════════════════════════════════════════════════════════
// Services Registration
// ════════════════════════════════════════════════════════════════════════════

// Controllers
builder.Services.AddControllers();

// SignalR
builder.Services.AddSignalR();

// CORS (for development with separate React dev server)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp", policy =>
    {
        policy.WithOrigins("http://localhost:3000", "http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// OpenAPI/Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "AIDevelopmentEasy API", Version = "v1" });
});

// Repositories (File-based - can be swapped for DB implementations)
builder.Services.AddSingleton<IApprovalRepository>(sp =>
    new FileSystemApprovalRepository(requirementsPath, sp.GetRequiredService<ILogger<FileSystemApprovalRepository>>()));

builder.Services.AddSingleton<ITaskRepository>(sp =>
    new FileSystemTaskRepository(requirementsPath, sp.GetRequiredService<ILogger<FileSystemTaskRepository>>()));

builder.Services.AddSingleton<IRequirementRepository>(sp =>
    new FileSystemRequirementRepository(
        requirementsPath,
        sp.GetRequiredService<IApprovalRepository>(),
        sp.GetRequiredService<ITaskRepository>(),
        sp.GetRequiredService<ILogger<FileSystemRequirementRepository>>()));

builder.Services.AddSingleton<IOutputRepository>(sp =>
    new FileSystemOutputRepository(outputPath, sp.GetRequiredService<ILogger<FileSystemOutputRepository>>()));

builder.Services.AddSingleton<ICodebaseRepository>(sp =>
    new FileSystemCodebaseRepository(codebasesPath, sp.GetRequiredService<ILogger<FileSystemCodebaseRepository>>()));

// Agents
builder.Services.AddSingleton(sp =>
    new CodeAnalysisAgent(sp.GetRequiredService<ILogger<CodeAnalysisAgent>>()));
builder.Services.AddSingleton(sp =>
    new PlannerAgent(openAIClient, deploymentName, sp.GetRequiredService<ILogger<PlannerAgent>>()));

builder.Services.AddSingleton(sp =>
    new CoderAgent(openAIClient, deploymentName, targetLanguage, sp.GetRequiredService<ILogger<CoderAgent>>()));

builder.Services.AddSingleton(sp =>
    new DebuggerAgent(openAIClient, deploymentName, debugMaxRetries, targetLanguage, sp.GetRequiredService<ILogger<DebuggerAgent>>()));

builder.Services.AddSingleton(sp =>
    new ReviewerAgent(openAIClient, deploymentName, sp.GetRequiredService<ILogger<ReviewerAgent>>()));

builder.Services.AddSingleton(sp =>
    new DeploymentAgent(sp.GetRequiredService<ILogger<DeploymentAgent>>()));

builder.Services.AddSingleton(sp =>
    new UnitTestAgent(
        sp.GetRequiredService<ILogger<UnitTestAgent>>(),
        maxParallelProjects: 4,
        testTimeoutSeconds: 300,
        configuration: "LocalTest",
        nunitConsolePath: UnitTestAgent.DefaultNUnitConsolePath));

// Pipeline Services
builder.Services.AddSingleton<IPipelineNotificationService, SignalRPipelineNotificationService>();
builder.Services.AddSingleton<IPipelineService, PipelineService>();

var app = builder.Build();

// ════════════════════════════════════════════════════════════════════════════
// Middleware Pipeline
// ════════════════════════════════════════════════════════════════════════════

// Swagger (always available, useful for debugging)
app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "AIDevelopmentEasy API v1"));

// CORS (for development)
app.UseCors("AllowReactApp");

// Serve React static files from wwwroot
var wwwrootPath = Path.Combine(AppContext.BaseDirectory, "wwwroot");
if (Directory.Exists(wwwrootPath))
{
    app.UseDefaultFiles(); // Serves index.html by default
    app.UseStaticFiles();  // Serves static files from wwwroot
    Log.Information("Serving React UI from: {Path}", wwwrootPath);
}

// Routing
app.UseRouting();

// Endpoints
app.MapControllers();
app.MapHub<PipelineHub>("/hubs/pipeline");

// Health check
app.MapGet("/health", () => new { Status = "Healthy", Timestamp = DateTime.UtcNow });

// SPA fallback - serve index.html for any unknown routes (for React Router)
if (Directory.Exists(wwwrootPath))
{
    app.MapFallbackToFile("index.html");
}

Log.Information("════════════════════════════════════════════════════════════");
Log.Information("  AIDevelopmentEasy Service Started");
Log.Information("════════════════════════════════════════════════════════════");
Log.Information("  Mode: {Mode}", WindowsServiceHelpers.IsWindowsService() ? "Windows Service" : "Console");
Log.Information("  Requirements: {Path}", requirementsPath);
Log.Information("  Codebases: {Path}", codebasesPath);
Log.Information("  Output: {Path}", outputPath);
Log.Information("  API: /swagger");
Log.Information("  Web UI: / (if wwwroot exists)");
Log.Information("════════════════════════════════════════════════════════════");

app.Run();

// ════════════════════════════════════════════════════════════════════════════
// Helper Methods
// ════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Copy default prompts to ProgramData if prompts directory is empty.
/// Tries multiple source locations in order of priority.
/// </summary>
static void CopyDefaultPromptsIfNeeded(string promptsPath)
{
    // Skip if prompts already exist
    if (Directory.EnumerateFiles(promptsPath, "*.md").Any())
        return;

    // Try sources in priority order:
    // 1. Application directory (deployed scenario)
    // 2. Solution directory (development scenario)
    var possibleSources = new[]
    {
        Path.Combine(AppContext.BaseDirectory, "prompts"),
        FindSolutionPromptsPath()
    };

    foreach (var sourcePath in possibleSources.Where(p => p != null && Directory.Exists(p)))
    {
        var promptFiles = Directory.GetFiles(sourcePath!, "*.md");
        if (promptFiles.Length > 0)
        {
            foreach (var file in promptFiles)
            {
                var destFile = Path.Combine(promptsPath, Path.GetFileName(file));
                if (!File.Exists(destFile))
                {
                    File.Copy(file, destFile);
                    Log.Information("Copied default prompt: {FileName}", Path.GetFileName(file));
                }
            }
            Log.Information("Copied {Count} default prompts from {Source}", promptFiles.Length, sourcePath);
            return;
        }
    }
}

/// <summary>
/// Find prompts directory in solution (for development mode)
/// </summary>
static string? FindSolutionPromptsPath()
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir != null)
    {
        if (dir.GetFiles("*.sln").Length > 0)
        {
            var promptsPath = Path.Combine(dir.FullName, "prompts");
            if (Directory.Exists(promptsPath))
                return promptsPath;
        }
        dir = dir.Parent;
    }
    return null;
}

/// <summary>
/// Helper class for Windows Service detection
/// </summary>
public static class WindowsServiceHelpers
{
    public static bool IsWindowsService()
    {
        if (!OperatingSystem.IsWindows())
            return false;
            
        // Check if parent process is services.exe
        try
        {
            using var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
            return currentProcess.SessionId == 0;
        }
        catch
        {
            return false;
        }
    }
}
