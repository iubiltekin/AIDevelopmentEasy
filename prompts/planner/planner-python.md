# Planner – Python

When this codebase uses Python:

- Use file extension `.py` for Python files in `target_files`.
- **Module layout**: One module per file; file and directory names in `snake_case`. Use `__init__.py` where the project uses packages (match context).
- **Package structure**: Prefer a clear package layout (e.g. `src/<package>/`, `app/`, or flat by feature). Follow the existing layout and naming from the codebase context.
- **Naming**: `snake_case` for functions, modules, and variables; `PascalCase` for classes. Match PEP 8 and existing style in context.
- **Types**: Prefer type hints when the codebase uses them. Use `typing` or `from __future__ import annotations` as in context.
- **Dependencies**: Do not add new top-level dependencies unless the requirement asks for it. Use stdlib or existing dependencies from context.
- **Async**: Use `async def` / `await` only if the project already uses asyncio. Match existing async style.
- **Tests**: Place tests in `tests/` or alongside code (e.g. `test_*.py`, `*_test.py`) as in context. Use the project's test framework (pytest, unittest, etc.).
- **Namespace**: Use the package/module path (e.g. `app.services.auth`) as in the codebase context. For standalone scripts, a simple module name is enough.
