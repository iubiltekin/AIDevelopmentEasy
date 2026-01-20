using System.Text.Json.Serialization;

namespace AIDevelopmentEasy.Core.Models;

/// <summary>
/// Complete analysis result of a codebase
/// </summary>
public class CodebaseAnalysis
{
    [JsonPropertyName("codebase_name")]
    public string CodebaseName { get; set; } = string.Empty;

    [JsonPropertyName("codebase_path")]
    public string CodebasePath { get; set; } = string.Empty;

    [JsonPropertyName("analyzed_at")]
    public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("solutions")]
    public List<SolutionInfo> Solutions { get; set; } = new();

    [JsonPropertyName("projects")]
    public List<ProjectInfo> Projects { get; set; } = new();

    [JsonPropertyName("summary")]
    public CodebaseSummary Summary { get; set; } = new();

    [JsonPropertyName("conventions")]
    public CodeConventions Conventions { get; set; } = new();

    /// <summary>
    /// Lightweight context for Requirements Wizard (smaller token count)
    /// Contains only high-level structure: projects, namespaces, patterns
    /// </summary>
    [JsonPropertyName("requirement_context")]
    public RequirementContext RequirementContext { get; set; } = new();

    /// <summary>
    /// Full context for Pipeline/Deployment (detailed)
    /// Contains classes, interfaces, methods for code generation
    /// </summary>
    [JsonPropertyName("pipeline_context")]
    public PipelineContext PipelineContext { get; set; } = new();
}

/// <summary>
/// Lightweight context for Requirements Wizard
/// Designed to minimize LLM token usage while providing essential structure info
/// </summary>
public class RequirementContext
{
    /// <summary>
    /// Human-readable summary text (for LLM prompt injection)
    /// </summary>
    [JsonPropertyName("summary_text")]
    public string SummaryText { get; set; } = string.Empty;

    /// <summary>
    /// Estimated token count
    /// </summary>
    [JsonPropertyName("token_estimate")]
    public int TokenEstimate { get; set; }

    /// <summary>
    /// Project names and their types
    /// </summary>
    [JsonPropertyName("projects")]
    public List<ProjectBrief> Projects { get; set; } = new();

    /// <summary>
    /// Main architectural layers/patterns
    /// </summary>
    [JsonPropertyName("architecture")]
    public List<string> Architecture { get; set; } = new();

    /// <summary>
    /// Key technologies and frameworks
    /// </summary>
    [JsonPropertyName("technologies")]
    public List<string> Technologies { get; set; } = new();

    /// <summary>
    /// Where new features typically go
    /// </summary>
    [JsonPropertyName("extension_points")]
    public List<ExtensionPoint> ExtensionPoints { get; set; } = new();
}

/// <summary>
/// Brief project info for requirement context
/// </summary>
public class ProjectBrief
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty; // API, Library, Tests, Console

    [JsonPropertyName("purpose")]
    public string Purpose { get; set; } = string.Empty;

    [JsonPropertyName("key_namespaces")]
    public List<string> KeyNamespaces { get; set; } = new();
}

/// <summary>
/// Where new code can be added
/// </summary>
public class ExtensionPoint
{
    [JsonPropertyName("layer")]
    public string Layer { get; set; } = string.Empty; // Controllers, Services, Repositories

    [JsonPropertyName("project")]
    public string Project { get; set; } = string.Empty;

    [JsonPropertyName("namespace")]
    public string Namespace { get; set; } = string.Empty;

    [JsonPropertyName("pattern")]
    public string? Pattern { get; set; } // Repository, Service, Controller
}

/// <summary>
/// Full context for Pipeline operations
/// Contains detailed class/interface information for code generation
/// </summary>
public class PipelineContext
{
    /// <summary>
    /// Full context text (for LLM prompt injection)
    /// </summary>
    [JsonPropertyName("full_context_text")]
    public string FullContextText { get; set; } = string.Empty;

    /// <summary>
    /// Estimated token count
    /// </summary>
    [JsonPropertyName("token_estimate")]
    public int TokenEstimate { get; set; }

    /// <summary>
    /// Detailed project information with classes
    /// </summary>
    [JsonPropertyName("project_details")]
    public List<ProjectDetail> ProjectDetails { get; set; } = new();
}

/// <summary>
/// Detailed project info for pipeline context
/// </summary>
public class ProjectDetail
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    [JsonPropertyName("namespace")]
    public string RootNamespace { get; set; } = string.Empty;

    [JsonPropertyName("interfaces")]
    public List<InterfaceBrief> Interfaces { get; set; } = new();

    [JsonPropertyName("classes")]
    public List<ClassBrief> Classes { get; set; } = new();
}

/// <summary>
/// Brief interface info
/// </summary>
public class InterfaceBrief
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("namespace")]
    public string Namespace { get; set; } = string.Empty;

    [JsonPropertyName("methods")]
    public List<string> Methods { get; set; } = new(); // Method signatures
}

/// <summary>
/// Brief class info
/// </summary>
public class ClassBrief
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("namespace")]
    public string Namespace { get; set; } = string.Empty;

    [JsonPropertyName("base_types")]
    public List<string> BaseTypes { get; set; } = new();

    [JsonPropertyName("pattern")]
    public string? Pattern { get; set; }

    [JsonPropertyName("public_methods")]
    public List<string> PublicMethods { get; set; } = new();
}

/// <summary>
/// Information about a .sln file
/// </summary>
public class SolutionInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    [JsonPropertyName("project_references")]
    public List<string> ProjectReferences { get; set; } = new();
}

/// <summary>
/// Information about a .csproj project
/// </summary>
public class ProjectInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    [JsonPropertyName("relative_path")]
    public string RelativePath { get; set; } = string.Empty;

    /// <summary>
    /// Directory containing the .csproj file (relative to codebase root)
    /// </summary>
    [JsonPropertyName("project_directory")]
    public string ProjectDirectory { get; set; } = string.Empty;

    [JsonPropertyName("target_framework")]
    public string TargetFramework { get; set; } = string.Empty;

    [JsonPropertyName("output_type")]
    public string OutputType { get; set; } = "Library";

    /// <summary>
    /// The root/primary namespace of the project (determined from analysis)
    /// </summary>
    [JsonPropertyName("root_namespace")]
    public string RootNamespace { get; set; } = string.Empty;

    [JsonPropertyName("project_references")]
    public List<string> ProjectReferences { get; set; } = new();

    [JsonPropertyName("package_references")]
    public List<PackageReference> PackageReferences { get; set; } = new();

    [JsonPropertyName("namespaces")]
    public List<string> Namespaces { get; set; } = new();

    /// <summary>
    /// Maps namespace suffixes to relative folder paths within the project.
    /// E.g., "Helpers" -> "Helpers", "Services.Internal" -> "Services/Internal"
    /// Empty string key "" maps to project root directory.
    /// </summary>
    [JsonPropertyName("namespace_folder_map")]
    public Dictionary<string, string> NamespaceFolderMap { get; set; } = new();

    [JsonPropertyName("classes")]
    public List<TypeInfo> Classes { get; set; } = new();

    [JsonPropertyName("interfaces")]
    public List<TypeInfo> Interfaces { get; set; } = new();

    [JsonPropertyName("detected_patterns")]
    public List<string> DetectedPatterns { get; set; } = new();

    /// <summary>
    /// Is this a test project?
    /// </summary>
    [JsonIgnore]
    public bool IsTestProject => Name.EndsWith(".Tests") || Name.EndsWith(".Test") ||
                                  DetectedPatterns.Contains("UnitTest");
}

/// <summary>
/// NuGet package reference
/// </summary>
public class PackageReference
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;
}

/// <summary>
/// Information about a class or interface
/// </summary>
public class TypeInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("namespace")]
    public string Namespace { get; set; } = string.Empty;

    [JsonPropertyName("file_path")]
    public string FilePath { get; set; } = string.Empty;

    [JsonPropertyName("base_types")]
    public List<string> BaseTypes { get; set; } = new();

    [JsonPropertyName("modifiers")]
    public List<string> Modifiers { get; set; } = new(); // public, abstract, static, partial, etc.

    [JsonPropertyName("members")]
    public List<MemberInfo> Members { get; set; } = new();

    [JsonPropertyName("detected_pattern")]
    public string? DetectedPattern { get; set; } // Repository, Service, Helper, Extension, etc.
}

/// <summary>
/// Information about a class/interface member (method, property)
/// </summary>
public class MemberInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("kind")]
    public string Kind { get; set; } = string.Empty; // Method, Property, Field, Event

    [JsonPropertyName("return_type")]
    public string? ReturnType { get; set; }

    [JsonPropertyName("parameters")]
    public List<string> Parameters { get; set; } = new();

    [JsonPropertyName("modifiers")]
    public List<string> Modifiers { get; set; } = new();
}

/// <summary>
/// Summary statistics of the codebase
/// </summary>
public class CodebaseSummary
{
    [JsonPropertyName("total_solutions")]
    public int TotalSolutions { get; set; }

    [JsonPropertyName("total_projects")]
    public int TotalProjects { get; set; }

    [JsonPropertyName("total_classes")]
    public int TotalClasses { get; set; }

    [JsonPropertyName("total_interfaces")]
    public int TotalInterfaces { get; set; }

    [JsonPropertyName("total_files")]
    public int TotalFiles { get; set; }

    [JsonPropertyName("primary_framework")]
    public string PrimaryFramework { get; set; } = string.Empty;

    [JsonPropertyName("detected_patterns")]
    public List<string> DetectedPatterns { get; set; } = new();

    [JsonPropertyName("key_namespaces")]
    public List<string> KeyNamespaces { get; set; } = new();
}

/// <summary>
/// Detected coding conventions
/// </summary>
public class CodeConventions
{
    [JsonPropertyName("naming_style")]
    public string NamingStyle { get; set; } = "PascalCase"; // Based on analysis

    [JsonPropertyName("private_field_prefix")]
    public string PrivateFieldPrefix { get; set; } = "_"; // _field or m_field

    [JsonPropertyName("uses_regions")]
    public bool UsesRegions { get; set; }

    [JsonPropertyName("uses_xml_docs")]
    public bool UsesXmlDocs { get; set; }

    [JsonPropertyName("async_suffix")]
    public bool UsesAsyncSuffix { get; set; } = true;

    [JsonPropertyName("test_framework")]
    public string? TestFramework { get; set; } // NUnit, xUnit, MSTest

    [JsonPropertyName("di_framework")]
    public string? DIFramework { get; set; } // Microsoft.Extensions.DependencyInjection, Autofac, etc.
}

// Note: CodebaseStatus enum is defined in AIDevelopmentEasy.Api.Models to avoid duplication

/// <summary>
/// Result of finding a class in the codebase
/// </summary>
public class ClassSearchResult
{
    [JsonPropertyName("found")]
    public bool Found { get; set; }

    [JsonPropertyName("class_name")]
    public string ClassName { get; set; } = string.Empty;

    [JsonPropertyName("file_path")]
    public string FilePath { get; set; } = string.Empty;

    [JsonPropertyName("full_path")]
    public string FullPath { get; set; } = string.Empty;

    [JsonPropertyName("namespace")]
    public string Namespace { get; set; } = string.Empty;

    [JsonPropertyName("project_name")]
    public string ProjectName { get; set; } = string.Empty;

    [JsonPropertyName("base_types")]
    public List<string> BaseTypes { get; set; } = new();

    [JsonPropertyName("members")]
    public List<MemberInfo> Members { get; set; } = new();

    [JsonPropertyName("file_content")]
    public string? FileContent { get; set; }
}

/// <summary>
/// A reference to a class from another file
/// </summary>
public class ClassReference
{
    [JsonPropertyName("file_path")]
    public string FilePath { get; set; } = string.Empty;

    [JsonPropertyName("full_path")]
    public string FullPath { get; set; } = string.Empty;

    [JsonPropertyName("project_name")]
    public string ProjectName { get; set; } = string.Empty;

    [JsonPropertyName("line_number")]
    public int LineNumber { get; set; }

    [JsonPropertyName("line_content")]
    public string LineContent { get; set; } = string.Empty;

    [JsonPropertyName("reference_type")]
    public ReferenceType ReferenceType { get; set; }

    [JsonPropertyName("containing_class")]
    public string? ContainingClass { get; set; }

    [JsonPropertyName("containing_method")]
    public string? ContainingMethod { get; set; }
}

/// <summary>
/// Type of reference to a class
/// </summary>
public enum ReferenceType
{
    /// <summary>Using/import statement</summary>
    Using,
    /// <summary>Inherits from the class</summary>
    Inheritance,
    /// <summary>Field or property type</summary>
    Field,
    /// <summary>Method parameter type</summary>
    Parameter,
    /// <summary>Method return type</summary>
    ReturnType,
    /// <summary>Local variable type</summary>
    LocalVariable,
    /// <summary>Generic type argument</summary>
    GenericArgument,
    /// <summary>Object instantiation (new)</summary>
    Instantiation,
    /// <summary>Static method call</summary>
    StaticCall,
    /// <summary>Instance method call</summary>
    MethodCall,
    /// <summary>Unknown reference</summary>
    Unknown
}

/// <summary>
/// Result of finding all references to a class
/// </summary>
public class ReferenceSearchResult
{
    [JsonPropertyName("class_name")]
    public string ClassName { get; set; } = string.Empty;

    [JsonPropertyName("class_file_path")]
    public string ClassFilePath { get; set; } = string.Empty;

    [JsonPropertyName("references")]
    public List<ClassReference> References { get; set; } = new();

    [JsonPropertyName("affected_files")]
    public List<string> AffectedFiles => References
        .Select(r => r.FilePath)
        .Distinct()
        .OrderBy(f => f)
        .ToList();

    [JsonPropertyName("affected_projects")]
    public List<string> AffectedProjects => References
        .Where(r => !string.IsNullOrEmpty(r.ProjectName))
        .Select(r => r.ProjectName)
        .Distinct()
        .OrderBy(p => p)
        .ToList();
}

/// <summary>
/// Task that modifies an existing file
/// </summary>
public class FileModificationTask
{
    [JsonPropertyName("file_path")]
    public string FilePath { get; set; } = string.Empty;

    [JsonPropertyName("full_path")]
    public string FullPath { get; set; } = string.Empty;

    [JsonPropertyName("project_name")]
    public string ProjectName { get; set; } = string.Empty;

    [JsonPropertyName("modification_type")]
    public ModificationType ModificationType { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("current_content")]
    public string? CurrentContent { get; set; }

    [JsonPropertyName("target_class")]
    public string? TargetClass { get; set; }

    [JsonPropertyName("target_method")]
    public string? TargetMethod { get; set; }

    [JsonPropertyName("related_references")]
    public List<ClassReference> RelatedReferences { get; set; } = new();
}

/// <summary>
/// Type of modification to make to a file
/// </summary>
public enum ModificationType
{
    /// <summary>Add a new method to existing class</summary>
    AddMethod,
    /// <summary>Modify an existing method</summary>
    ModifyMethod,
    /// <summary>Add a new property to existing class</summary>
    AddProperty,
    /// <summary>Modify an existing property</summary>
    ModifyProperty,
    /// <summary>Update method calls to use new signature</summary>
    UpdateMethodCall,
    /// <summary>Add using statement</summary>
    AddUsing,
    /// <summary>Rename a symbol</summary>
    Rename,
    /// <summary>Add new class to existing file</summary>
    AddClass,
    /// <summary>General modification</summary>
    General
}