# AIDevelopmentEasy

Cooperative Multi-Agent Software Development Framework - A C#/.NET application that automates software development tasks using LLM-powered agents.

Reference: **"AgentMesh: A Cooperative Multi-Agent Generative AI Framework for Software Development Automation"** - [arXiv:2507.19902](https://arxiv.org/pdf/2507.19902)

## 🚀 Quick Start

```bash
# 1. Clone
git clone https://github.com/iubiltekin/AIDevelopmentEasy.git
cd AIDevelopmentEasy

# 2. API Key Configuration
cp src/AIDevelopmentEasy.CLI/appsettings.json src/AIDevelopmentEasy.CLI/appsettings.Local.json
# Edit appsettings.Local.json and enter your Azure OpenAI credentials

# 3. Build & Run
dotnet restore
dotnet build
dotnet run --project src/AIDevelopmentEasy.CLI
```

## Technologies

| Technology | Purpose |
|------------|---------|
| **.NET 8** | AIDevelopmentEasy runtime |
| **Azure OpenAI (GPT-4o)** | LLM API - code generation and analysis |
| **ASP.NET Core Web API** | REST API with SignalR real-time updates |
| **MSBuild** | Compilation of generated C# code |
| **Microsoft.Extensions.DependencyInjection** | SOLID-compliant DI container |
| **Serilog** | Structured logging |

## Running Modes

| Mode | Command | Description |
|------|---------|-------------|
| **CLI (Interactive)** | `dotnet run --project src/AIDevelopmentEasy.CLI` | Terminal-based interactive workflow |
| **Web API** | `dotnet run --project src/AIDevelopmentEasy.Api` | REST API + SignalR for web clients |

### Web API Endpoints

```
GET  /api/requirements              - List all requirements
GET  /api/requirements/{id}         - Get requirement details
POST /api/requirements              - Create new requirement
DEL  /api/requirements/{id}         - Delete requirement
POST /api/requirements/{id}/reset   - Reset requirement status

POST /api/pipeline/{id}/start       - Start processing
GET  /api/pipeline/{id}/status      - Get pipeline status
POST /api/pipeline/{id}/approve/{phase} - Approve a phase
POST /api/pipeline/{id}/reject/{phase}  - Reject a phase
POST /api/pipeline/{id}/cancel      - Cancel pipeline

SignalR Hub: /hubs/pipeline         - Real-time updates
Swagger UI:  /swagger               - API documentation
```

## Architecture

### Interactive Pipeline Flow

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                     AIDevelopmentEasy Interactive Pipeline                   │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│   ┌─────────────────────────────────────────────────────────────────────┐   │
│   │                    📋 REQUIREMENTS MENU                              │   │
│   │  ─────────────────────────────────────────────────────────────────  │   │
│   │  [1] log-rotation.json        (Multi)   ⬜ Not Started              │   │
│   │  [2] feature-x.md             (Single)  ✅ Approved                 │   │
│   │  [3] completed.txt            (Single)  ✔️ Completed                │   │
│   │  ─────────────────────────────────────────────────────────────────  │   │
│   │  [0] Exit  |  [R] Refresh  |  [number] Select                       │   │
│   └─────────────────────────────────────────────────────────────────────┘   │
│                              │                                               │
│                              ▼                                               │
│   ┌──────────────────────────────────────────────────────────────────────┐  │
│   │  PHASE 1: PLANNING                                                    │  │
│   │  ├─ Requirement analysis                                              │  │
│   │  ├─ Task generation                                                   │  │
│   │  └─ ❓ Approval: "Approve plan?" [Y/n]                                │  │
│   └──────────────────────────────────────────────────────────────────────┘  │
│                              │                                               │
│                              ▼                                               │
│   ┌──────────────────────────────────────────────────────────────────────┐  │
│   │  PHASE 2: CODE GENERATION                                             │  │
│   │  ├─ ❓ Approval: "Start coding?" [Y/n]                                │  │
│   │  └─ Code generation for each file                                     │  │
│   └──────────────────────────────────────────────────────────────────────┘  │
│                              │                                               │
│                              ▼                                               │
│   ┌──────────────────────────────────────────────────────────────────────┐  │
│   │  PHASE 3: COMPILATION CHECK                                           │  │
│   │  ├─ ❓ Approval: "Run debugger?" [Y/n]                                │  │
│   │  └─ MSBuild + error fixing                                            │  │
│   └──────────────────────────────────────────────────────────────────────┘  │
│                              │                                               │
│                              ▼                                               │
│   ┌──────────────────────────────────────────────────────────────────────┐  │
│   │  PHASE 4: UNIT TESTING                                                │  │
│   │  ├─ ❓ Approval: "Run tests?" [Y/n]                                   │  │
│   │  └─ Test results                                                      │  │
│   └──────────────────────────────────────────────────────────────────────┘  │
│                              │                                               │
│                              ▼                                               │
│   ┌──────────────────────────────────────────────────────────────────────┐  │
│   │  PHASE 5: CODE REVIEW                                                 │  │
│   │  ├─ ❓ Approval: "Run review?" [Y/n]                                  │  │
│   │  └─ Quality report                                                    │  │
│   └──────────────────────────────────────────────────────────────────────┘  │
│                              │                                               │
│                              ▼                                               │
│   output/{timestamp}_{requirement}/                                         │
│   ├── ProjectName/                                                          │
│   │   └── GeneratedCode.cs                                                  │
│   └── review_report.md                                                      │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

> 📖 **Detailed workflow guide:** [INTERACTIVE-WORKFLOW.md](./INTERACTIVE-WORKFLOW.md)

## Agents

| Agent | Role | Responsibility |
|-------|------|----------------|
| **PlannerAgent** | Software Project Planner | Analyzes requirements, breaks down into tasks |
| **MultiProjectPlannerAgent** | Multi-Project Planner | Phase-based planning for multi-project requirements |
| **CoderAgent** | Senior Developer | Generates code for each file (following coding standards) |
| **DebuggerAgent** | Debug Specialist | Compiles with MSBuild, fixes errors |
| **ReviewerAgent** | Senior Code Reviewer | Final quality control, provides approval |

## Features

### 🎯 Interactive Flow
- **Status Tracking**: ⬜ Not Started → 📋 Planned → ✅ Approved → 🔄 In Progress → ✔️ Completed
- **Step-by-Step Approval**: User approval required at each phase
- **No Reprocessing**: Completed requirements are automatically skipped
- **Menu System**: Easy selection and navigation

### 🏗️ SOLID-Compliant Architecture
- **Single Responsibility**: Each service does one thing
- **Open/Closed**: New processors can be easily added
- **Dependency Inversion**: All dependencies through DI container
- **Interface Segregation**: Small, focused interfaces

### 🤖 Multi-Agent Architecture
- 5 specialized agents working cooperatively
- Communication via shared state (blackboard pattern)
- Each agent has its own LLM prompt
- **Editable Prompts** - Markdown files in `prompts/` directory

### 📦 Multi-Project Support
- Single requirement can affect multiple projects
- Each project developed with its own test project
- Phase-based execution (core → consumer → integration)
- Cross-project dependency management

### 📝 Task Management
- Single-project: `requirements/*.txt` or `*.md`
- Multi-project: `requirements/*.json` (with affected_projects)
- Automatic task decomposition
- **Editable task files** - edit/delete/add before approval

### 📋 Coding Standards Integration
```json
{
  "framework": { "name": ".NET Framework", "version": "4.6.2" },
  "testing": { "framework": "NUnit", "assertionLibrary": "FluentAssertions" },
  "coding": { "namingConventions": { "privateFields": "_camelCase" } },
  "prohibited": ["System.Text.Json", "MSTest framework"],
  "required": ["XML documentation", "Explicit null checks"]
}
```

## Configuration

### 🔒 API Key Security

Two files are used to protect your API keys:

| File | Purpose | Committed to Git? |
|------|---------|-------------------|
| `appsettings.json` | Template (placeholder values) | ✅ Yes |
| `appsettings.Local.json` | Actual API keys | ❌ No |

### appsettings.json (Template)
```json
{
  "AzureOpenAI": {
    "Endpoint": "YOUR_AZURE_OPENAI_ENDPOINT",
    "ApiKey": "YOUR_AZURE_OPENAI_API_KEY",
    "DeploymentName": "YOUR_DEPLOYMENT_NAME",
    "ApiVersion": "2024-02-15-preview"
  },
  "AIDevelopmentEasy": {
    "RequirementsDirectory": "requirements",
    "OutputDirectory": "output",
    "CodingStandardsFile": "coding-standards.json",
    "TargetLanguage": "csharp",
    "DebugMaxRetries": 3
  }
}
```

### appsettings.Local.json (Actual Values)
```json
{
  "AzureOpenAI": {
    "Endpoint": "https://your-resource.openai.azure.com/",
    "ApiKey": "your-actual-api-key",
    "DeploymentName": "gpt-4o",
    "ApiVersion": "2024-02-15-preview"
  }
}
```

> ⚠️ **Important**: `appsettings.Local.json` is defined in `.gitignore` and will not be pushed to GitHub.

## Usage

### Single-Project Requirement

1. Create a requirement file:
```
requirements/log-rotation.md
```
```markdown
Create a Log Rotation Helper library in C# targeting .NET Framework 4.6.2.

Requirements:
- LogRotator class that manages log file rotation
- Method to check if current log file exceeds size limit
- Thread-safe operations
```

2. Run and select from menu:
```bash
dotnet run --project src/AIDevelopmentEasy.CLI
```

### Multi-Project Requirement

1. Create a JSON requirement file:
```
requirements/log-rotation.json
```
```json
{
  "title": "Log Rotation Helper Library",
  "description": "Implement log rotation capability",
  "affected_projects": [
    {
      "name": "LogRotationHelper",
      "role": "core",
      "type": "library",
      "order": 1,
      "outputs": [
        { "file": "LogRotator.cs", "type": "implementation" },
        { "file": "ILogRotator.cs", "type": "implementation" }
      ],
      "test_project": "LogRotationHelper.Tests"
    },
    {
      "name": "LogRotationHelper.Tests",
      "role": "test",
      "type": "test",
      "order": 2,
      "depends_on": ["LogRotationHelper"],
      "outputs": [
        { "file": "LogRotatorTests.cs", "type": "test", "uses": ["LogRotator"] }
      ]
    }
  ]
}
```

2. Run, select from menu, and follow the steps

## Project Structure

```
AIDevelopmentEasy/
├── 📄 README.md
├── 📄 INTERACTIVE-WORKFLOW.md          # Detailed workflow guide
├── 📄 LICENSE
│
├── 📁 src/
│   ├── 📁 AIDevelopmentEasy.Api/        # Web API (REST + SignalR)
│   │   ├── Controllers/                 # API endpoints
│   │   ├── Hubs/                        # SignalR hubs
│   │   ├── Models/                      # DTOs
│   │   ├── Repositories/                # Data access abstraction
│   │   │   ├── Interfaces/              # Repository contracts
│   │   │   └── FileSystem/              # File-based implementations
│   │   └── Services/                    # Business logic
│   │
│   ├── 📁 AIDevelopmentEasy.Core/       # Core business logic
│   │   ├── 📁 Agents/
│   │   │   ├── 📁 Base/
│   │   │   │   ├── IAgent.cs            # Agent interface & models
│   │   │   │   └── BaseAgent.cs         # LLM utilities
│   │   │   ├── PlannerAgent.cs          # Single-project planning
│   │   │   ├── MultiProjectPlannerAgent.cs
│   │   │   ├── CoderAgent.cs            # Code generation
│   │   │   ├── DebuggerAgent.cs         # Compilation & fix
│   │   │   └── ReviewerAgent.cs         # Quality assurance
│   │   ├── 📁 Services/
│   │   │   └── PromptLoader.cs          # Loads prompts from files
│   │   └── 📁 Models/
│   │       └── MultiProjectRequirement.cs
│   │
│   └── 📁 AIDevelopmentEasy.CLI/        # Console application
│       ├── Program.cs                   # Entry point (minimal)
│       ├── 📁 Configuration/
│       │   └── AppSettings.cs           # Strongly-typed config
│       ├── 📁 Extensions/
│       │   └── ServiceCollectionExtensions.cs  # DI registration
│       ├── 📁 Models/
│       │   └── RequirementInfo.cs       # Requirement status tracking
│       ├── 📁 Services/
│       │   ├── 📁 Interfaces/
│       │   │   ├── IConsoleUI.cs
│       │   │   ├── IRequirementLoader.cs
│       │   │   └── IPipelineRunner.cs
│       │   ├── ConsoleUI.cs             # Interactive UI
│       │   ├── RequirementLoader.cs     # File loading
│       │   └── PipelineRunner.cs        # Pipeline orchestration
│       ├── appsettings.json             # Template config
│       ├── appsettings.Local.json       # Actual secrets (gitignored)
│       └── coding-standards.json        # Coding rules
│
├── 📁 prompts/                          # Agent system prompts (editable)
│   ├── README.md                        # Prompt documentation
│   ├── planner.md                       # PlannerAgent prompt
│   ├── multi-project-planner.md         # MultiProjectPlannerAgent prompt
│   ├── coder-csharp.md                  # CoderAgent C# prompt
│   ├── coder-generic.md                 # CoderAgent generic prompt
│   ├── debugger-csharp.md               # DebuggerAgent C# prompt
│   ├── debugger-generic.md              # DebuggerAgent generic prompt
│   └── reviewer.md                      # ReviewerAgent prompt
│
├── 📁 requirements/                     # Input: requirement files
│   └── log-rotation-helper.json
│
├── 📁 output/                           # Output: generated code
│   └── {timestamp}_{name}/
│
└── 📁 logs/                             # Application logs
    └── aideveasy-{date}.txt
```

## Benefits

| Benefit | Description |
|---------|-------------|
| **Rapid Prototyping** | Requirement → Working code in minutes |
| **Consistent Code Quality** | Coding standards always enforced |
| **Test Coverage** | Automatic unit tests for each feature |
| **Human-in-the-Loop** | User approval at each step |
| **Multi-Project Support** | Single requirement for multiple projects |
| **No Reprocessing** | Completed work is skipped |
| **SOLID Architecture** | Maintainable, extensible code |

## Future Improvements

- [ ] Jira integration (fetch requirements from Jira)
- [ ] Parallel agent execution
- [ ] Vector database for memory
- [ ] Git integration (auto-commit)
- [ ] Existing codebase analysis
- [ ] Web UI (Blazor/React)

## References

### Primary Reference
- [AgentMesh Paper](https://arxiv.org/pdf/2507.19902) - The academic paper this project is based on

### Similar Multi-Agent Frameworks (for comparison)
- [ChatDev](https://github.com/OpenBMB/ChatDev) - Python, multi-agent software company simulation
- [MetaGPT](https://github.com/geekan/MetaGPT) - Python, multi-agent meta programming
- [AutoGen](https://github.com/microsoft/autogen) - Microsoft's multi-agent conversation framework

> **Note**: These projects were not directly used, listed only as references for similar concepts.

## License

MIT
