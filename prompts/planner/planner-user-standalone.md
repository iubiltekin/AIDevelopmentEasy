# Planner user prompt (standalone, no codebase)

Please analyze the following requirement and create a development plan:

# REQUIREMENT

{{REQUIREMENT}}

# INSTRUCTIONS

Break this down into specific development tasks. Consider:

- What data structures/models are needed?
- What functions/methods need to be implemented?
- What error handling is required?
- What tests should be written?

## CRITICAL: Namespace Requirement

- For each task, you MUST specify the full namespace
- The namespace should match the folder structure (e.g., MyProject.Helpers for Helpers/ folder)

Output the plan as JSON with the following structure:

```json
{
    "project_name": "Short project name",
    "summary": "Brief summary of what will be built",
    "tasks": [
        {
            "index": 1,
            "title": "Short descriptive title",
            "description": "Detailed description of what to implement",
            "target_files": ["ClassName.cs"],
            "namespace": "ProjectName.FolderName"
        }
    ]
}
```

Output ONLY valid JSON.
