using Microsoft.Extensions.Logging;

namespace AIDevelopmentEasy.Core.Analysis;

/// <summary>
/// Resolves <see cref="ICodebaseAnalyzer"/> by language id and provides all analyzers for polyglot detection.
/// </summary>
public class CodebaseAnalyzerFactory
{
    private readonly IReadOnlyDictionary<string, ICodebaseAnalyzer> _analyzers;
    private readonly ILogger<CodebaseAnalyzerFactory>? _logger;

    public CodebaseAnalyzerFactory(IEnumerable<ICodebaseAnalyzer> analyzers, ILogger<CodebaseAnalyzerFactory>? logger = null)
    {
        _analyzers = analyzers.ToDictionary(a => a.LanguageId, StringComparer.OrdinalIgnoreCase);
        _logger = logger;
    }

    /// <summary>
    /// Gets the analyzer for the given language id (e.g. "csharp", "go").
    /// Returns null if not found.
    /// </summary>
    public ICodebaseAnalyzer? GetAnalyzer(string languageId)
    {
        var key = (languageId ?? "").Trim();
        if (string.IsNullOrEmpty(key))
            return null;
        return _analyzers.TryGetValue(key, out var a) ? a : null;
    }

    /// <summary>
    /// Gets all registered analyzers (for polyglot: run each that CanAnalyze(path)).
    /// </summary>
    public IReadOnlyList<ICodebaseAnalyzer> GetAllAnalyzers()
    {
        return _analyzers.Values.ToList();
    }

    /// <summary>
    /// Gets analyzers that can analyze the given path.
    /// </summary>
    public IReadOnlyList<ICodebaseAnalyzer> GetApplicableAnalyzers(string codebasePath)
    {
        return _analyzers.Values.Where(a => a.CanAnalyze(codebasePath)).ToList();
    }
}
