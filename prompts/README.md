# Agent Prompts

This directory contains the system prompts for each agent in the AIDevelopmentEasy framework.

## Files

| File | Agent | Description |
|------|-------|-------------|
| `planner.md` | PlannerAgent | Task decomposition and project planning |
| `coder-csharp.md` | CoderAgent (C#) | C# code generation with .NET Framework 4.6.2 |
| `coder-generic.md` | CoderAgent (Generic) | Generic code generation for other languages |
| `debugger-csharp.md` | DebuggerAgent (C#) | C# debugging and error fixing |
| `debugger-generic.md` | DebuggerAgent (Generic) | Generic debugging |
| `reviewer.md` | ReviewerAgent | Code review and quality assurance |

## Customization

You can edit these files to customize agent behavior. Changes take effect on the next pipeline run.

### Key Customization Points

1. **planner.md**: Modify task decomposition rules, output format
2. **coder-csharp.md**: Change coding standards, namespace handling, testing conventions
3. **reviewer.md**: Adjust review criteria, approval thresholds

## Important Notes

- **Namespace Handling**: The `coder-csharp.md` prompt emphasizes exact namespace matching. This is critical for correct file placement in the deployment phase.
- **JSON Output**: Planner and Reviewer use JSON output format. Ensure changes maintain valid JSON structure.
- **Variables**: `coder-generic.md` uses `{{LANGUAGE}}` placeholder which is replaced at runtime.
