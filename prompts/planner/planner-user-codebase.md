# Planner user prompt (with codebase)

Please analyze the following requirement and create a development plan that integrates with the existing codebase.

# REQUIREMENT

{{REQUIREMENT}}

{{CODEBASE_CONTEXT}}

# INSTRUCTIONS

1. Analyze how this requirement fits into the existing codebase structure
2. Identify which projects need modifications and whether each change creates new files or modifies existing ones
3. Create tasks that follow existing patterns and conventions
4. Order tasks by dependency (core implementations first, then consumers, then tests)
5. Each task MUST specify the target project, files, namespace, and modification_type (create or modify)
6. Each target_file MUST use the file extension for that project from the "Languages and file extensions" section (e.g. Go project → .go, TypeScript/React → .tsx, not .py unless the project language is Python)

## CRITICAL: Only use paths from the codebase context

- **target_files** may ONLY contain paths that appear in the codebase context above (under "Main Projects", "Folder Structure (real paths)", or "Test Projects"). Use the exact path style (relative to project or repo) shown there.
- For **modify** tasks: the file must be one of the existing files or paths listed in the context. If a file is not listed, do not include it as a target.
- Do NOT invent or guess paths (e.g. content/models/platform.go, web/src/components/Platform/...). If the context does not show a project or folder, do not create target_files for it.
- Only create tasks for projects and files that are actually present in the codebase context.

## CRITICAL: modification_type

- For each task set "modification_type": "create" (new file) or "modify" (existing file), based on the requirement and codebase context
- Infer from the requirement: e.g. "add new endpoint" → create or modify; "fix bug in X" → modify

## CRITICAL: Namespace Convention

- For each task, you MUST specify the FULL namespace
- The namespace follows the pattern: ProjectName.SubFolder (e.g., Picus.Common.Helpers)
- Look at existing classes in the codebase to determine the correct namespace
- The namespace determines where the file will be placed in the project

Output the plan as JSON with the following structure:

```json
{
    "project_name": "Feature name or module name",
    "summary": "Brief description of what will be implemented",
    "tasks": [
        {
            "index": 1,
            "project": "ProjectName",
            "title": "Task title",
            "description": "Detailed implementation description",
            "target_files": ["Folder/FileName.<ext>"],
            "namespace": "ProjectName.Folder",
            "modification_type": "create or modify",
            "depends_on": [],
            "uses_existing": ["ExistingClass", "IExistingInterface"]
        }
    ]
}
```

Example (new file):

```json
{
    "index": 1,
    "project": "Picus.Common",
    "title": "Create DateTimeHelper class",
    "description": "Create DateTimeHelper class with AddDays method",
    "target_files": ["Helpers/DateTimeHelper.<ext>"],
    "namespace": "Picus.Common.Helpers",
    "modification_type": "create",
    "depends_on": [],
    "uses_existing": []
}
```

Example (modify existing):

```json
{
    "index": 2,
    "project": "Picus.Api",
    "title": "Add validation to UserController",
    "description": "Add input validation to the create method",
    "target_files": ["Controllers/UserController.<ext>"],
    "namespace": "Picus.Api.Controllers",
    "modification_type": "modify",
    "depends_on": [1],
    "uses_existing": ["UserController"]
}
```

Output ONLY valid JSON.
