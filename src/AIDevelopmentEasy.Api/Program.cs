using Azure;
using Azure.AI.OpenAI;
using AIDevelopmentEasy.Api.Hubs;
using AIDevelopmentEasy.Api.Repositories.FileSystem;
using AIDevelopmentEasy.Api.Repositories.Interfaces;
using AIDevelopmentEasy.Api.Services;
using AIDevelopmentEasy.Api.Services.Interfaces;
using AIDevelopmentEasy.Core.Agents;
using AIDevelopmentEasy.Core.Services;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

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
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .MinimumLevel.Information()
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

// ════════════════════════════════════════════════════════════════════════════
// Path Configuration
// ════════════════════════════════════════════════════════════════════════════
var solutionDir = FindSolutionDirectory();
var requirementsPath = Path.Combine(solutionDir, "requirements");
var outputPath = Path.Combine(solutionDir, "output");
var promptsPath = Path.Combine(solutionDir, "prompts");

Directory.CreateDirectory(requirementsPath);
Directory.CreateDirectory(outputPath);
Directory.CreateDirectory(promptsPath);

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

// CORS (for React frontend)
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

// Agents
builder.Services.AddSingleton(sp =>
    new PlannerAgent(openAIClient, deploymentName, sp.GetRequiredService<ILogger<PlannerAgent>>()));

builder.Services.AddSingleton(sp =>
    new MultiProjectPlannerAgent(openAIClient, deploymentName, sp.GetRequiredService<ILogger<MultiProjectPlannerAgent>>()));

builder.Services.AddSingleton(sp =>
    new CoderAgent(openAIClient, deploymentName, targetLanguage, sp.GetRequiredService<ILogger<CoderAgent>>()));

builder.Services.AddSingleton(sp =>
    new DebuggerAgent(openAIClient, deploymentName, debugMaxRetries, targetLanguage, sp.GetRequiredService<ILogger<DebuggerAgent>>()));

builder.Services.AddSingleton(sp =>
    new ReviewerAgent(openAIClient, deploymentName, sp.GetRequiredService<ILogger<ReviewerAgent>>()));

// Pipeline Services
builder.Services.AddSingleton<IPipelineNotificationService, SignalRPipelineNotificationService>();
builder.Services.AddSingleton<IPipelineService, PipelineService>();

var app = builder.Build();

// ════════════════════════════════════════════════════════════════════════════
// Middleware Pipeline
// ════════════════════════════════════════════════════════════════════════════

// Swagger
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "AIDevelopmentEasy API v1"));
}

// CORS
app.UseCors("AllowReactApp");

// Routing
app.UseRouting();

// Endpoints
app.MapControllers();
app.MapHub<PipelineHub>("/hubs/pipeline");

// Health check
app.MapGet("/health", () => new { Status = "Healthy", Timestamp = DateTime.UtcNow });

Log.Information("AIDevelopmentEasy API started");
Log.Information("Requirements path: {Path}", requirementsPath);
Log.Information("Output path: {Path}", outputPath);
Log.Information("Swagger UI: http://localhost:5000/swagger");

app.Run();

// ════════════════════════════════════════════════════════════════════════════
// Helper Methods
// ════════════════════════════════════════════════════════════════════════════
static string FindSolutionDirectory()
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir != null)
    {
        if (dir.GetFiles("*.sln").Length > 0)
            return dir.FullName;
        dir = dir.Parent;
    }
    return AppContext.BaseDirectory;
}
