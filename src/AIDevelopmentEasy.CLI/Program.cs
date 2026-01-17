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
        Path.Combine(tempPaths.LogsPath, "cli-.log"),
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
var storyLoader = services.GetRequiredService<IStoryLoader>();
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
    console.ShowPaths(paths.CodingStandardsPath, paths.StoriesPath, paths.OutputPath, paths.LogsPath);

    // Check for stories
    if (!storyLoader.HasStories)
    {
        console.ShowNoStoriesFound(paths.StoriesPath);
        console.PressAnyKeyToContinue("Press any key to exit...");
        break;
    }

    // Refresh statuses and show menu
    storyLoader.RefreshStatuses();
    var stories = storyLoader.GetAllStories();
    console.ShowStoriesMenu(stories);

    // Get user selection
    var selectedStory = console.SelectStory(stories);

    if (selectedStory == null)
    {
        // User chose to exit
        break;
    }

    // Process selected story
    try
    {
        await pipelineRunner.ProcessAsync(selectedStory);
    }
    catch (Exception ex)
    {
        console.ShowError($"\n  Error: {ex.Message}");
        Log.Error(ex, "Error processing story {Name}", selectedStory.Name);
    }

    console.PressAnyKeyToContinue("Press any key to return to menu...");
}

console.ShowCompleted();
