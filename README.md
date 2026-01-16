# AIDevelopmentEasy

Cooperative Multi-Agent Software Development Framework - LLM destekli agent'lar kullanarak yazılım geliştirme görevlerini otomatikleştiren bir C#/.NET uygulaması.

Referans: **"AgentMesh: A Cooperative Multi-Agent Generative AI Framework for Software Development Automation"** - [arXiv:2507.19902](https://arxiv.org/pdf/2507.19902)

## 🚀 Hızlı Başlangıç

```bash
# 1. Clone
git clone https://github.com/yourusername/AIDevelopmentEasy.git
cd AIDevelopmentEasy

# 2. API Key Konfigürasyonu
cp src/AIDevelopmentEasy.CLI/appsettings.json src/AIDevelopmentEasy.CLI/appsettings.Local.json
# appsettings.Local.json dosyasını düzenleyip Azure OpenAI bilgilerinizi girin

# 3. Build & Run
dotnet restore
dotnet build
dotnet run --project src/AIDevelopmentEasy.CLI
```

## Teknolojiler

| Teknoloji | Kullanım Amacı |
|-----------|----------------|
| **.NET 8** | AIDevelopmentEasy runtime |
| **Azure OpenAI (GPT-4o)** | LLM API - kod üretimi ve analizi |
| **MSBuild** | Üretilen C# kodunun derlenmesi |
| **Microsoft.Extensions.DependencyInjection** | SOLID uyumlu DI container |
| **Serilog** | Yapılandırılmış loglama |

## Mimari

### İnteraktif Pipeline Akışı

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
│   │  ├─ Requirement analizi                                               │  │
│   │  ├─ Task oluşturma                                                    │  │
│   │  └─ ❓ Onay: "Approve plan?" [Y/n]                                    │  │
│   └──────────────────────────────────────────────────────────────────────┘  │
│                              │                                               │
│                              ▼                                               │
│   ┌──────────────────────────────────────────────────────────────────────┐  │
│   │  PHASE 2: CODE GENERATION                                             │  │
│   │  ├─ ❓ Onay: "Start coding?" [Y/n]                                    │  │
│   │  └─ Her dosya için kod üretimi                                        │  │
│   └──────────────────────────────────────────────────────────────────────┘  │
│                              │                                               │
│                              ▼                                               │
│   ┌──────────────────────────────────────────────────────────────────────┐  │
│   │  PHASE 3: COMPILATION CHECK                                           │  │
│   │  ├─ ❓ Onay: "Run debugger?" [Y/n]                                    │  │
│   │  └─ MSBuild + hata düzeltme                                           │  │
│   └──────────────────────────────────────────────────────────────────────┘  │
│                              │                                               │
│                              ▼                                               │
│   ┌──────────────────────────────────────────────────────────────────────┐  │
│   │  PHASE 4: UNIT TESTING                                                │  │
│   │  ├─ ❓ Onay: "Run tests?" [Y/n]                                       │  │
│   │  └─ Test sonuçları                                                    │  │
│   └──────────────────────────────────────────────────────────────────────┘  │
│                              │                                               │
│                              ▼                                               │
│   ┌──────────────────────────────────────────────────────────────────────┐  │
│   │  PHASE 5: CODE REVIEW                                                 │  │
│   │  ├─ ❓ Onay: "Run review?" [Y/n]                                      │  │
│   │  └─ Kalite raporu                                                     │  │
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

> 📖 **Detaylı akış kılavuzu:** [INTERACTIVE-WORKFLOW.md](./INTERACTIVE-WORKFLOW.md)

## Agent'lar

| Agent | Rol | Sorumluluk |
|-------|-----|------------|
| **PlannerAgent** | Yazılım Proje Planlayıcı | Requirement'ı analiz eder, task'lara böler |
| **MultiProjectPlannerAgent** | Çoklu Proje Planlayıcı | Multi-project requirement'lar için phase-based planning |
| **CoderAgent** | Senior Developer | Her dosya için kod üretir (coding standards'a uygun) |
| **DebuggerAgent** | Debug Uzmanı | MSBuild ile derler, hataları düzeltir |
| **ReviewerAgent** | Senior Code Reviewer | Son kalite kontrolü, onay verir |

## Özellikler

### 🎯 İnteraktif Akış
- **Durum Takibi**: ⬜ Not Started → 📋 Planned → ✅ Approved → 🔄 In Progress → ✔️ Completed
- **Adım Adım Onay**: Her fazda kullanıcı onayı gerekir
- **Tekrar İşleme Yok**: Tamamlanan requirement'lar otomatik atlanır
- **Menü Sistemi**: Kolay seçim ve navigasyon

### 🏗️ SOLID Uyumlu Mimari
- **Single Responsibility**: Her servis tek bir iş yapıyor
- **Open/Closed**: Yeni processor'lar kolayca eklenebilir
- **Dependency Inversion**: Tüm bağımlılıklar DI container üzerinden
- **Interface Segregation**: Küçük, odaklı interface'ler

### 🤖 Multi-Agent Mimari
- 5 uzmanlaşmış agent cooperative çalışır
- Shared state (blackboard pattern) ile iletişim
- Her agent kendi LLM prompt'una sahip
- **Düzenlenebilir Prompt'lar** - `prompts/` dizininde Markdown formatında

### 📦 Multi-Project Support
- Tek bir requirement ile birden fazla proje etkilenebilir
- Her proje kendi test projesiyle birlikte geliştirilir
- Phase-based execution (core → consumer → integration)
- Cross-project dependency management

### 📝 Task Yönetimi
- Single-project: `requirements/*.txt` veya `*.md`
- Multi-project: `requirements/*.json` (with affected_projects)
- Otomatik task decomposition
- **Düzenlenebilir task dosyaları** - onay öncesi edit/delete/add

### 📋 Coding Standards Entegrasyonu
```json
{
  "framework": { "name": ".NET Framework", "version": "4.6.2" },
  "testing": { "framework": "NUnit", "assertionLibrary": "FluentAssertions" },
  "coding": { "namingConventions": { "privateFields": "_camelCase" } },
  "prohibited": ["System.Text.Json", "MSTest framework"],
  "required": ["XML documentation", "Explicit null checks"]
}
```

## Konfigürasyon

### 🔒 API Key Güvenliği

API key'lerinizi korumak için iki dosya kullanılır:

| Dosya | Amaç | Git'e Gider? |
|-------|------|--------------|
| `appsettings.json` | Template (placeholder değerler) | ✅ Evet |
| `appsettings.Local.json` | Gerçek API key'ler | ❌ Hayır |

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

### appsettings.Local.json (Gerçek Değerler)
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

> ⚠️ **Önemli**: `appsettings.Local.json` dosyası `.gitignore`'da tanımlıdır ve GitHub'a gitmez.

## Kullanım

### Single-Project Requirement

1. Requirement dosyası oluştur:
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

2. Çalıştır ve menüden seç:
```bash
dotnet run --project src/AIDevelopmentEasy.CLI
```

### Multi-Project Requirement

1. JSON requirement dosyası oluştur:
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

2. Çalıştır, menüden seç ve adımları takip et

## Proje Yapısı

```
AIDevelopmentEasy/
├── 📄 README.md
├── 📄 INTERACTIVE-WORKFLOW.md          # Detaylı akış kılavuzu
├── 📄 LICENSE
│
├── 📁 src/
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

## Faydaları

| Fayda | Açıklama |
|-------|----------|
| **Hızlı Prototipleme** | Requirement → Working code in minutes |
| **Tutarlı Kod Kalitesi** | Coding standards her zaman uygulanır |
| **Test Coverage** | Her feature için otomatik unit testleri |
| **Human-in-the-Loop** | Her adımda kullanıcı onayı |
| **Multi-Project Support** | Tek requirement ile birden fazla proje |
| **Tekrar İşleme Yok** | Tamamlanan işler atlanır |
| **SOLID Mimari** | Bakımı kolay, genişletilebilir kod |

## Gelecek Geliştirmeler

- [ ] Jira entegrasyonu (requirement'ları Jira'dan çek)
- [ ] Paralel agent execution
- [ ] Vector database ile memory
- [ ] Git integration (auto-commit)
- [ ] Existing codebase analysis
- [ ] Web UI (Blazor/React)

## Referanslar

### Temel Referans
- [AgentMesh Paper](https://arxiv.org/pdf/2507.19902) - Bu projenin dayandığı akademik makale

### Benzer Multi-Agent Framework'leri (Karşılaştırma için)
- [ChatDev](https://github.com/OpenBMB/ChatDev) - Python, multi-agent software company simulation
- [MetaGPT](https://github.com/geekan/MetaGPT) - Python, multi-agent meta programming
- [AutoGen](https://github.com/microsoft/autogen) - Microsoft'un multi-agent conversation framework'ü

> **Not**: Bu projeler doğrudan kullanılmamıştır, sadece benzer konseptler için referans olarak listelenmiştir.

## Lisans

MIT
