using Azure;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using AIDevelopmentEasy.CLI.Configuration;
using AIDevelopmentEasy.CLI.Services;
using AIDevelopmentEasy.CLI.Services.Interfaces;
using AIDevelopmentEasy.Core.Agents;
using AIDevelopmentEasy.Core.Analysis;
using AIDevelopmentEasy.Core.Analysis.CSharp;
using AIDevelopmentEasy.Core.Analysis.Go;
using AIDevelopmentEasy.Core.Analysis.Rust;
using AIDevelopmentEasy.Core.Analysis.Python;
using AIDevelopmentEasy.Core.Analysis.Frontend;

namespace AIDevelopmentEasy.CLI.Extensions;

/// <summary>
/// Extension methods for service registration
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAIDevelopmentEasyServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Configuration
        var appSettings = new AppSettings();
        configuration.Bind(appSettings);
        appSettings.AzureOpenAI.Validate();

        services.AddSingleton(appSettings);
        services.AddSingleton(appSettings.AzureOpenAI);
        services.AddSingleton(appSettings.AIDevelopmentEasy);

        // Resolved paths
        var resolvedPaths = new ResolvedPaths(appSettings.AIDevelopmentEasy);
        services.AddSingleton(resolvedPaths);

        // Azure OpenAI Client
        var credential = new AzureKeyCredential(appSettings.AzureOpenAI.ApiKey);
        var openAIClient = new OpenAIClient(new Uri(appSettings.AzureOpenAI.Endpoint), credential);
        services.AddSingleton(openAIClient);

        // UI Services
        services.AddSingleton<IConsoleUI, ConsoleUI>();
        services.AddSingleton<IStoryLoader, StoryLoader>();

        // Pipeline Runner
        services.AddSingleton<IPipelineRunner, PipelineRunner>();

        return services;
    }

    public static IServiceCollection AddAgents(
        this IServiceCollection services,
        ILoggerFactory loggerFactory)
    {
        services.AddSingleton<ICodebaseAnalyzer, CSharpCodebaseAnalyzer>();
        services.AddSingleton<ICodebaseAnalyzer, GoCodebaseAnalyzer>();
        services.AddSingleton<ICodebaseAnalyzer, RustCodebaseAnalyzer>();
        services.AddSingleton<ICodebaseAnalyzer, PythonCodebaseAnalyzer>();
        services.AddSingleton<ICodebaseAnalyzer, FrontendCodebaseAnalyzer>();
        services.AddSingleton<CodebaseAnalyzerFactory>(sp =>
            new CodebaseAnalyzerFactory(
                sp.GetServices<ICodebaseAnalyzer>(),
                loggerFactory.CreateLogger<CodebaseAnalyzerFactory>()));
        services.AddSingleton<CodeAnalysisAgent>(sp =>
            new CodeAnalysisAgent(
                sp.GetRequiredService<CodebaseAnalyzerFactory>(),
                loggerFactory.CreateLogger<CodeAnalysisAgent>()));

        services.AddSingleton(sp =>
        {
            var client = sp.GetRequiredService<OpenAIClient>();
            var settings = sp.GetRequiredService<AzureOpenAISettings>();
            return new PlannerAgent(client, settings.DeploymentName, sp.GetRequiredService<CodeAnalysisAgent>(), loggerFactory.CreateLogger<PlannerAgent>());
        });

        services.AddSingleton(sp =>
        {
            var client = sp.GetRequiredService<OpenAIClient>();
            var azureSettings = sp.GetRequiredService<AzureOpenAISettings>();
            var appSettings = sp.GetRequiredService<AIDevelopmentEasySettings>();
            return new CoderAgent(client, azureSettings.DeploymentName, appSettings.TargetLanguage, loggerFactory.CreateLogger<CoderAgent>());
        });

        services.AddSingleton(sp =>
        {
            var client = sp.GetRequiredService<OpenAIClient>();
            var azureSettings = sp.GetRequiredService<AzureOpenAISettings>();
            var appSettings = sp.GetRequiredService<AIDevelopmentEasySettings>();
            return new DebuggerAgent(client, azureSettings.DeploymentName, appSettings.DebugMaxRetries, appSettings.TargetLanguage, loggerFactory.CreateLogger<DebuggerAgent>());
        });

        services.AddSingleton(sp =>
        {
            var client = sp.GetRequiredService<OpenAIClient>();
            var settings = sp.GetRequiredService<AzureOpenAISettings>();
            return new ReviewerAgent(client, settings.DeploymentName, loggerFactory.CreateLogger<ReviewerAgent>());
        });

        return services;
    }
}
