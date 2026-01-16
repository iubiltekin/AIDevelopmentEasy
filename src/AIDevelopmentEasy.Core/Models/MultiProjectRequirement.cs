using System.Text.Json.Serialization;

namespace AIDevelopmentEasy.Core.Models;

/// <summary>
/// Multi-project requirement definition
/// </summary>
public class MultiProjectRequirement
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("affected_projects")]
    public List<AffectedProject> AffectedProjects { get; set; } = new();

    [JsonPropertyName("integration")]
    public IntegrationConfig? Integration { get; set; }
}

/// <summary>
/// A project affected by the requirement
/// </summary>
public class AffectedProject
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("path")]
    public string? Path { get; set; }

    [JsonPropertyName("role")]
    public string Role { get; set; } = "implementation"; // core, consumer, test

    [JsonPropertyName("type")]
    public string Type { get; set; } = "library"; // library, test, console

    [JsonPropertyName("order")]
    public int Order { get; set; } = 1;

    [JsonPropertyName("depends_on")]
    public List<string> DependsOn { get; set; } = new();

    [JsonPropertyName("outputs")]
    public List<ProjectOutput> Outputs { get; set; } = new();

    [JsonPropertyName("test_project")]
    public string? TestProject { get; set; } // e.g., "Picus.Common.Tests"

    /// <summary>
    /// Is this a test project?
    /// </summary>
    [JsonIgnore]
    public bool IsTestProject => Type == "test" || Name.EndsWith(".Tests");
}

/// <summary>
/// Expected output file from a project
/// </summary>
public class ProjectOutput
{
    [JsonPropertyName("file")]
    public string File { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = "implementation"; // implementation, test, config

    [JsonPropertyName("uses")]
    public List<string> Uses { get; set; } = new(); // Classes this file uses
}

/// <summary>
/// Integration/final build configuration
/// </summary>
public class IntegrationConfig
{
    [JsonPropertyName("order")]
    public int Order { get; set; } = 99;

    [JsonPropertyName("description")]
    public string Description { get; set; } = "Full solution build and integration test";

    [JsonPropertyName("build_all")]
    public bool BuildAll { get; set; } = true;

    [JsonPropertyName("run_tests")]
    public bool RunTests { get; set; } = true;
}

/// <summary>
/// Extended SubTask with project information
/// </summary>
public class ProjectSubTask
{
    public int Index { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public int Phase { get; set; } = 1;
    public List<string> TargetFiles { get; set; } = new();
    public List<string> DependsOnTasks { get; set; } = new();
    public List<string> DependsOnProjects { get; set; } = new();
    public List<string> UsesClasses { get; set; } = new();
    public Dictionary<string, string> Context { get; set; } = new();
    public string Status { get; set; } = "Pending";
}

/// <summary>
/// Phase containing tasks for multiple projects
/// </summary>
public class ExecutionPhase
{
    public int Order { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<ProjectSubTask> Tasks { get; set; } = new();
    public bool IsApproved { get; set; }
}
