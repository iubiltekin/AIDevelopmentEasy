using AIDevelopmentEasy.Core.Models;

namespace AIDevelopmentEasy.Core.Analysis;

/// <summary>
/// Analyzes a codebase (or a part of it) and produces a language-agnostic <see cref="CodebaseAnalysis"/>.
/// Each implementation covers one language (C#, Go, Rust, TypeScript/React, etc.).
/// For polyglot repos, multiple analyzers run and results are merged.
/// </summary>
public interface ICodebaseAnalyzer
{
    /// <summary>
    /// Unique language identifier (e.g. "csharp", "go", "rust", "typescript").
    /// Used by the factory and for merging polyglot results.
    /// </summary>
    string LanguageId { get; }

    /// <summary>
    /// Whether this analyzer can handle the given codebase path (e.g. contains .sln, go.mod, Cargo.toml).
    /// Used to decide which analyzers to run for a repo.
    /// </summary>
    bool CanAnalyze(string codebasePath);

    /// <summary>
    /// Performs static analysis and returns a partial analysis (only this language's projects).
    /// Projects must have <see cref="ProjectInfo.LanguageId"/> set to this analyzer's <see cref="LanguageId"/>.
    /// </summary>
    Task<CodebaseAnalysis> AnalyzeAsync(string codebasePath, string codebaseName, CancellationToken cancellationToken = default);
}
