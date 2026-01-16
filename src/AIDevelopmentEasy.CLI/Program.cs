using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using AIDevelopmentEasy.CLI.Configuration;
using AIDevelopmentEasy.CLI.Extensions;
using AIDevelopmentEasy.CLI.Services.Interfaces;

Console.OutputEncoding = Encoding.UTF8;

// ════════════════════════════════════════════════════════════════════════════
// Configuration
// ════════════════════════════════════════════════════════════════════════════
var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true)
    .AddJsonFile("appsettings.Local.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

// ════════════════════════════════════════════════════════════════════════════
// Logging
// ════════════════════════════════════════════════════════════════════════════
var tempSettings = new AIDevelopmentEasySettings();
configuration.GetSection("AIDevelopmentEasy").Bind(tempSettings);
var tempPaths = new ResolvedPaths(tempSettings);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(configuration)
    .MinimumLevel.Information()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        Path.Combine(tempPaths.LogsPath, "aideveasy-.txt"),
        rollingInterval: RollingInterval.Day,
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level:u3}] {Message:lj}{NewLine}{Exception}",
        retainedFileCountLimit: 7)
    .CreateLogger();

using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.ClearProviders();
    builder.AddSerilog(Log.Logger);
});

// ════════════════════════════════════════════════════════════════════════════
// Dependency Injection
// ════════════════════════════════════════════════════════════════════════════
var services = new ServiceCollection()
    .AddAIDevelopmentEasyServices(configuration)
    .AddAgents(loggerFactory)
    .BuildServiceProvider();

// ════════════════════════════════════════════════════════════════════════════
// Resolve Services
// ════════════════════════════════════════════════════════════════════════════
var console = services.GetRequiredService<IConsoleUI>();
var paths = services.GetRequiredService<ResolvedPaths>();
var requirementLoader = services.GetRequiredService<IRequirementLoader>();
var pipelineRunner = services.GetRequiredService<IPipelineRunner>();

// ════════════════════════════════════════════════════════════════════════════
// Initialize
// ════════════════════════════════════════════════════════════════════════════
await paths.LoadCodingStandardsAsync();

// ════════════════════════════════════════════════════════════════════════════
// Main Interactive Loop
// ════════════════════════════════════════════════════════════════════════════
while (true)
{
    console.Clear();
    console.ShowBanner();
    console.ShowPaths(paths.CodingStandardsPath, paths.RequirementsPath, paths.OutputPath, paths.LogsPath);

    // Check for requirements
    if (!requirementLoader.HasRequirements)
    {
        console.ShowNoRequirementsFound(paths.RequirementsPath);
        console.PressAnyKeyToContinue("Press any key to exit...");
        break;
    }

    // Refresh statuses and show menu
    requirementLoader.RefreshStatuses();
    var requirements = requirementLoader.GetAllRequirements();
    console.ShowRequirementsMenu(requirements);

    // Get user selection
    var selectedRequirement = console.SelectRequirement(requirements);

    if (selectedRequirement == null)
    {
        // User chose to exit
        break;
    }

    // Process selected requirement
    try
    {
        await pipelineRunner.ProcessAsync(selectedRequirement);
    }
    catch (Exception ex)
    {
        console.ShowError($"\n  Error: {ex.Message}");
        Log.Error(ex, "Error processing requirement {Name}", selectedRequirement.Name);
    }

    console.PressAnyKeyToContinue("Press any key to return to menu...");
}

console.ShowCompleted();
