# AI Agent Prompts

This directory contains the system prompts for AIDevelopmentEasy agents. Each prompt file is in Markdown format and is read by agents at runtime.

## Prompt Files

| File | Agent | Description |
|------|-------|-------------|
| `planner.md` | PlannerAgent | Analyzes requirements and creates task lists |
| `multi-project-planner.md` | MultiProjectPlannerAgent | Plans multi-project requirements |
| `coder-csharp.md` | CoderAgent (C#) | Generates C# code |
| `coder-generic.md` | CoderAgent (Other) | Generates code for other languages |
| `debugger-csharp.md` | DebuggerAgent (C#) | Detects and fixes errors in C# code |
| `debugger-generic.md` | DebuggerAgent (Other) | Fixes errors in other languages |
| `reviewer.md` | ReviewerAgent | Evaluates code quality and requirement compliance |

## Editing Prompts

To edit prompts:

1. Open the relevant `.md` file
2. Edit the content (in Markdown format)
3. Save the file

Changes are **automatically loaded** on the next agent call without requiring a restart.

## Variable Usage

Some prompt files support `{{VARIABLE}}` format variables:

- `coder-generic.md`: `{{LANGUAGE}}` - Target programming language

## Structure

Prompt files follow this structure:

```markdown
# Agent Name System Prompt

Description paragraph...

## Responsibilities

1. Item 1
2. Item 2

## Rules

- Rule 1
- Rule 2

## Output Format

Expected output format description...

**IMPORTANT**: Important notes...
```

## Important Notes

1. **JSON Output**: Planner and Reviewer agents expect JSON format output
2. **Code Output**: Coder and Debugger agents expect code in markdown code blocks
3. **Language Compatibility**: Pay attention to .NET Framework 4.6.2 compatibility
4. **Test Framework**: NUnit and FluentAssertions are used
