using System.Text.Json;
using AIDevelopmentEasy.Api.Repositories.Interfaces;

namespace AIDevelopmentEasy.Api.Repositories.FileSystem;

/// <summary>
/// File system based implementation of IOutputRepository.
/// Generated code is stored in output/{timestamp}_{storyId}/ directory.
/// </summary>
public class FileSystemOutputRepository : IOutputRepository
{
    private readonly string _outputPath;
    private readonly ILogger<FileSystemOutputRepository> _logger;

    public FileSystemOutputRepository(string outputPath, ILogger<FileSystemOutputRepository> logger)
    {
        _outputPath = outputPath;
        _logger = logger;

        if (!Directory.Exists(_outputPath))
        {
            Directory.CreateDirectory(_outputPath);
        }
    }

    public async Task<string> SaveOutputAsync(string storyId, string projectName, Dictionary<string, string> files, CancellationToken cancellationToken = default)
    {
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var outputDir = Path.Combine(_outputPath, $"{timestamp}_{storyId}");
        var projectDir = Path.Combine(outputDir, projectName);

        Directory.CreateDirectory(projectDir);

        foreach (var (fileName, content) in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var filePath = Path.Combine(projectDir, fileName);
            var fileDir = Path.GetDirectoryName(filePath);
            
            if (!string.IsNullOrEmpty(fileDir) && !Directory.Exists(fileDir))
            {
                Directory.CreateDirectory(fileDir);
            }

            await File.WriteAllTextAsync(filePath, content, cancellationToken);
            _logger.LogInformation("Saved output file: {FilePath}", filePath);
        }

        _logger.LogInformation("Saved {Count} files to: {OutputDir}", files.Count, outputDir);
        return outputDir;
    }

    public Task<string?> GetOutputPathAsync(string storyId, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_outputPath))
            return Task.FromResult<string?>(null);

        // Find the most recent output directory for this story
        var outputDir = Directory.GetDirectories(_outputPath)
            .Where(d => Path.GetFileName(d).EndsWith($"_{storyId}"))
            .OrderByDescending(d => d)
            .FirstOrDefault();

        return Task.FromResult(outputDir);
    }

    public async Task<Dictionary<string, string>> GetGeneratedFilesAsync(string storyId, CancellationToken cancellationToken = default)
    {
        var files = new Dictionary<string, string>();
        var outputDir = await GetOutputPathAsync(storyId, cancellationToken);

        if (outputDir == null || !Directory.Exists(outputDir))
            return files;

        var allFiles = Directory.GetFiles(outputDir, "*.*", SearchOption.AllDirectories)
            .Where(f => !f.EndsWith("review_report.md"));

        foreach (var file in allFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var relativePath = Path.GetRelativePath(outputDir, file);
            var content = await File.ReadAllTextAsync(file, cancellationToken);
            files[relativePath] = content;
        }

        return files;
    }

    public async Task SaveReviewReportAsync(string storyId, string report, CancellationToken cancellationToken = default)
    {
        var outputDir = await GetOutputPathAsync(storyId, cancellationToken);
        
        if (outputDir == null)
        {
            // Create a new output directory if none exists
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            outputDir = Path.Combine(_outputPath, $"{timestamp}_{storyId}");
            Directory.CreateDirectory(outputDir);
        }

        var reportPath = Path.Combine(outputDir, "review_report.md");
        await File.WriteAllTextAsync(reportPath, report, cancellationToken);

        _logger.LogInformation("Saved review report: {ReportPath}", reportPath);
    }

    public async Task<string?> GetReviewReportAsync(string storyId, CancellationToken cancellationToken = default)
    {
        var outputDir = await GetOutputPathAsync(storyId, cancellationToken);
        
        if (outputDir == null)
            return null;

        var reportPath = Path.Combine(outputDir, "review_report.md");
        
        if (!File.Exists(reportPath))
            return null;

        return await File.ReadAllTextAsync(reportPath, cancellationToken);
    }

    public Task<IEnumerable<string>> ListOutputsAsync(CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_outputPath))
            return Task.FromResult<IEnumerable<string>>(Array.Empty<string>());

        var outputs = Directory.GetDirectories(_outputPath)
            .Select(Path.GetFileName)
            .Where(n => n != null)
            .Cast<string>()
            .OrderByDescending(n => n)
            .ToList();

        return Task.FromResult<IEnumerable<string>>(outputs);
    }

    public async Task SavePipelineHistoryAsync(string storyId, object pipelineStatus, CancellationToken cancellationToken = default)
    {
        var outputDir = await GetOutputPathAsync(storyId, cancellationToken);
        
        if (outputDir == null)
        {
            // Create a new output directory if none exists
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            outputDir = Path.Combine(_outputPath, $"{timestamp}_{storyId}");
            Directory.CreateDirectory(outputDir);
        }

        var historyPath = Path.Combine(outputDir, "pipeline_history.json");
        var jsonOptions = new JsonSerializerOptions 
        { 
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        var json = JsonSerializer.Serialize(pipelineStatus, jsonOptions);
        await File.WriteAllTextAsync(historyPath, json, cancellationToken);

        _logger.LogInformation("Saved pipeline history: {HistoryPath}", historyPath);
    }

    public async Task<string?> GetPipelineHistoryAsync(string storyId, CancellationToken cancellationToken = default)
    {
        var outputDir = await GetOutputPathAsync(storyId, cancellationToken);
        
        if (outputDir == null)
            return null;

        var historyPath = Path.Combine(outputDir, "pipeline_history.json");
        
        if (!File.Exists(historyPath))
            return null;

        return await File.ReadAllTextAsync(historyPath, cancellationToken);
    }
}
