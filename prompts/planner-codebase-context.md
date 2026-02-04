# Planner codebase context block

## EXISTING CODEBASE: {{CODEBASE_NAME}}

{{CONTEXT}}

## INTEGRATION GUIDELINES (for this codebase only)

1. **File extensions**: Use ONLY the extensions from the "Languages and file extensions" section above for each project. Never use .py unless a project in that section has language Python; never use .cs unless csharp is listed; never use .go unless go is listed; match extension to the project.
2. **Project placement**: Put code in the project(s) and paths shown above.
3. **Namespace/Package/Module**: Follow the convention shown in the codebase context for each project.
4. **Conventions**: Private fields: {{PRIVATE_FIELD_PREFIX}}fieldName; Test framework: {{TEST_FRAMEWORK}}.
5. **Tests**: Place in the test project/path from context, with the same file extension as that project.
6. **Task fields**: project, target_files (paths with correct extension), depends_on, uses_existing.
