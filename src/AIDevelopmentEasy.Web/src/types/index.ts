// API Types matching the backend models

export enum StoryType {
  Single = 0
}

export enum StoryStatus {
  NotStarted = 0,
  Planned = 1,
  Approved = 2,
  InProgress = 3,
  Completed = 4,
  Failed = 5
}

export enum TaskStatus {
  Pending = 0,
  InProgress = 1,
  Completed = 2,
  Failed = 3
}

export enum TaskType {
  Original = 0,  // Original task from planning phase
  Fix = 1        // Fix task generated from test/build failures
}

export enum PipelinePhase {
  None = 0,
  Analysis = 1,           // CodeAnalysisAgent - Codebase analysis (optional)
  Planning = 2,           // PlannerAgent - Task decomposition
  Coding = 3,             // CoderAgent - Code generation/modification
  Debugging = 4,          // DebuggerAgent - Testing and fixing
  Reviewing = 5,          // ReviewerAgent - Quality review
  Deployment = 6,         // DeploymentAgent - Deploy to codebase and build
  UnitTesting = 7, // Run new/modified tests in deployed codebase
  PullRequest = 8,        // Create GitHub PR (after tests pass)
  Completed = 9
}

export enum PhaseState {
  Pending = 0,
  WaitingApproval = 1,
  Running = 2,
  Completed = 3,
  Failed = 4,
  Skipped = 5,
  WaitingRetryApproval = 6  // Waiting for user to approve auto-fix retry
}

export interface TaskDto {
  index: number;
  title: string;
  description: string;
  projectName: string;
  targetFiles: string[];
  dependsOnProjects: string[];
  projectOrder: number;
  status: TaskStatus;
  type?: TaskType;           // Original = 0, Fix = 1
  retryAttempt?: number;     // Retry attempt number (0 = initial, 1+ = retry)
  usesExisting?: string[];
  isModification?: boolean;  // true = modify existing file, false = create new file
  fullPath?: string;         // full path to target file for modifications
  namespace?: string;        // target namespace for generated code
  existingCode?: string;     // existing code content for fix tasks (preserved before rollback)
}

export interface StoryDto {
  id: string;
  name: string;
  content: string;
  type?: StoryType;  // Optional for backward compatibility
  status: StoryStatus;
  codebaseId?: string;
  /** The requirement this story was created from (if any) */
  requirementId?: string;
  createdAt: string;
  lastProcessedAt?: string;
  tasks: TaskDto[];
  // Target Info (Optional) - For bugfixes and modifications
  targetProject?: string;
  targetFile?: string;
  targetClass?: string;
  targetMethod?: string;
  changeType?: ChangeType;
}

export enum ChangeType {
  Create = 0,
  Modify = 1,
  Delete = 2
}

export interface UpdateStoryTargetRequest {
  targetProject?: string;
  targetFile?: string;
  targetClass?: string;
  targetMethod?: string;
  changeType?: ChangeType;
}

export interface PhaseStatusDto {
  phase: PipelinePhase;
  state: PhaseState;
  message?: string;
  startedAt?: string;
  completedAt?: string;
  result?: unknown;
}

export interface PipelineStatusDto {
  storyId: string;
  currentPhase: PipelinePhase;
  isRunning: boolean;
  phases: PhaseStatusDto[];
  startedAt?: string;
  completedAt?: string;
  retryInfo?: RetryInfoDto;
  retryTargetPhase?: PipelinePhase;
}

// ════════════════════════════════════════════════════════════════════════════
// Retry and Fix Task Types
// ════════════════════════════════════════════════════════════════════════════

export enum FixTaskType {
  BuildError = 0,
  TestFailure = 1,
  IntegrationError = 2
}

export enum RetryReason {
  BuildFailed = 0,
  TestsFailed = 1,
  IntegrationFailed = 2
}

export enum RetryAction {
  AutoFix = 0,
  ManualFix = 1,
  SkipTests = 2,
  Abort = 3
}

export interface FixTaskDto {
  index: number;
  title: string;
  description: string;
  targetFile: string;
  type: FixTaskType;
  errorMessage: string;
  errorLocation?: string;
  stackTrace?: string;
  suggestedFix?: string;
}

export interface TestResultDto {
  testName: string;
  className: string;
  filePath?: string;
  passed: boolean;
  errorMessage?: string;
  stackTrace?: string;
  duration: string;
  isNewTest: boolean;
}

export interface TestSummaryDto {
  totalTests: number;
  passed: number;
  failed: number;
  skipped: number;
  newTestsPassed: number;
  newTestsFailed: number;
  existingTestsFailed: number;
  totalDuration: string;
  isBreakingChange: boolean;
  failedTests: TestResultDto[];
}

export interface RetryInfoDto {
  currentAttempt: number;
  maxAttempts: number;
  reason: RetryReason;
  fixTasks: FixTaskDto[];
  lastError?: string;
  testSummary?: TestSummaryDto;
  lastAttemptAt?: string;
}

export interface PipelineUpdateMessage {
  storyId: string;
  updateType: string;
  phase: PipelinePhase;
  message: string;
  data?: unknown;
  timestamp: string;
}

export interface CreateStoryRequest {
  name: string;
  content: string;
  type?: StoryType;  // Optional, defaults to Single
  codebaseId?: string;
  requirementId?: string;
  // Target Info (Optional)
  targetProject?: string;
  targetFile?: string;
  targetClass?: string;
  targetMethod?: string;
  changeType?: ChangeType;
}

// ════════════════════════════════════════════════════════════════════════════
// Target Selection Types (for dropdowns)
// ════════════════════════════════════════════════════════════════════════════

export interface FileInfoDto {
  path: string;
  name: string;
  namespace: string;
  classCount: number;
  interfaceCount: number;
}

export interface ClassInfoDto {
  name: string;
  namespace: string;
  filePath: string;
  baseTypes: string[];
  pattern?: string;
  methods: string[];
}

export interface MethodInfoDto {
  name: string;
  returnType: string;
  parameters: string[];
  isPublic: boolean;
  isAsync: boolean;
}

// Helper functions
export function getChangeTypeLabel(type: ChangeType): string {
  const labels: Record<ChangeType, string> = {
    [ChangeType.Create]: 'Create New',
    [ChangeType.Modify]: 'Modify Existing',
    [ChangeType.Delete]: 'Delete'
  };
  return labels[type] ?? 'Unknown';
}

export function getChangeTypeColor(type: ChangeType): string {
  const colors: Record<ChangeType, string> = {
    [ChangeType.Create]: 'bg-green-600',
    [ChangeType.Modify]: 'bg-amber-600',
    [ChangeType.Delete]: 'bg-red-600'
  };
  return colors[type] ?? 'bg-slate-600';
}

// ════════════════════════════════════════════════════════════════════════════
// Codebase Types
// ════════════════════════════════════════════════════════════════════════════

export enum CodebaseStatus {
  Pending = 0,
  Analyzing = 1,
  Ready = 2,
  Failed = 3
}

export interface CodebaseDto {
  id: string;
  name: string;
  path: string;
  status: CodebaseStatus;
  analyzedAt?: string;
  createdAt: string;
  summary?: CodebaseSummaryDto;
}

export interface CodebaseSummaryDto {
  totalSolutions: number;
  totalProjects: number;
  totalClasses: number;
  totalInterfaces: number;
  primaryFramework: string;
  detectedPatterns: string[];
  keyNamespaces: string[];
}

export interface CreateCodebaseRequest {
  name: string;
  path: string;
}

export interface ProjectSummaryDto {
  name: string;
  relativePath: string;
  targetFramework: string;
  outputType: string;
  isTestProject: boolean;
  classCount: number;
  interfaceCount: number;
  detectedPatterns: string[];
  projectReferences: string[];
}

// ════════════════════════════════════════════════════════════════════════════
// Codebase Context Types (Two-Level LLM Optimization)
// ════════════════════════════════════════════════════════════════════════════

export interface RequirementContextDto {
  summaryText: string;
  tokenEstimate: number;
  projects: ProjectBriefDto[];
  architecture: string[];
  technologies: string[];
  extensionPoints: ExtensionPointDto[];
}

export interface ProjectBriefDto {
  name: string;
  type: string;
  purpose: string;
  keyNamespaces: string[];
}

export interface ExtensionPointDto {
  layer: string;
  project: string;
  namespace: string;
  pattern?: string;
}

export interface PipelineContextDto {
  fullContextText: string;
  tokenEstimate: number;
  projectCount: number;
  classCount: number;
  interfaceCount: number;
}

export function getCodebaseStatusLabel(status: CodebaseStatus): string {
  const labels: Record<CodebaseStatus, string> = {
    [CodebaseStatus.Pending]: 'Pending',
    [CodebaseStatus.Analyzing]: 'Analyzing...',
    [CodebaseStatus.Ready]: 'Ready',
    [CodebaseStatus.Failed]: 'Failed'
  };
  return labels[status] || 'Unknown';
}

export function getCodebaseStatusColor(status: CodebaseStatus): string {
  const colors: Record<CodebaseStatus, string> = {
    [CodebaseStatus.Pending]: 'bg-slate-500',
    [CodebaseStatus.Analyzing]: 'bg-amber-500 animate-pulse',
    [CodebaseStatus.Ready]: 'bg-emerald-500',
    [CodebaseStatus.Failed]: 'bg-red-500'
  };
  return colors[status] || 'bg-slate-500';
}

// Helper functions
export function getStatusLabel(status: StoryStatus): string {
  const labels: Record<StoryStatus, string> = {
    [StoryStatus.NotStarted]: 'Not Started',
    [StoryStatus.Planned]: 'Planned',
    [StoryStatus.Approved]: 'Approved',
    [StoryStatus.InProgress]: 'In Progress',
    [StoryStatus.Completed]: 'Completed',
    [StoryStatus.Failed]: 'Failed'
  };
  return labels[status] || 'Unknown';
}

export function getStatusColor(status: StoryStatus): string {
  const colors: Record<StoryStatus, string> = {
    [StoryStatus.NotStarted]: 'bg-slate-500',
    [StoryStatus.Planned]: 'bg-blue-500',
    [StoryStatus.Approved]: 'bg-emerald-500',
    [StoryStatus.InProgress]: 'bg-amber-500',
    [StoryStatus.Completed]: 'bg-green-500',
    [StoryStatus.Failed]: 'bg-red-500'
  };
  return colors[status] || 'bg-slate-500';
}

export function getPhaseLabel(phase: PipelinePhase): string {
  const labels: Record<PipelinePhase, string> = {
    [PipelinePhase.None]: 'None',
    [PipelinePhase.Analysis]: 'Analysis',
    [PipelinePhase.Planning]: 'Planning',
    [PipelinePhase.Coding]: 'Coding',
    [PipelinePhase.Debugging]: 'Debugging',
    [PipelinePhase.Reviewing]: 'Reviewing',
    [PipelinePhase.Deployment]: 'Deployment',
    [PipelinePhase.UnitTesting]: 'Unit Testing',
    [PipelinePhase.PullRequest]: 'Pull Request',
    [PipelinePhase.Completed]: 'Completed'
  };
  return labels[phase] || 'Unknown';
}

// Get detailed phase description
export function getPhaseDescription(phase: PipelinePhase): string {
  const descriptions: Record<PipelinePhase, string> = {
    [PipelinePhase.None]: '',
    [PipelinePhase.Analysis]: 'CodeAnalysisAgent analyzing codebase structure and references',
    [PipelinePhase.Planning]: 'PlannerAgent decomposing story into tasks',
    [PipelinePhase.Coding]: 'CoderAgent generating or modifying code',
    [PipelinePhase.Debugging]: 'DebuggerAgent verifying and fixing code',
    [PipelinePhase.Reviewing]: 'ReviewerAgent checking code quality',
    [PipelinePhase.Deployment]: 'DeploymentAgent deploying code to codebase and building',
    [PipelinePhase.UnitTesting]: 'Running new/modified tests in deployed codebase',
    [PipelinePhase.PullRequest]: 'Creating GitHub Pull Request',
    [PipelinePhase.Completed]: 'Pipeline completed successfully'
  };
  return descriptions[phase] || '';
}

// Get phase agent name
export function getPhaseAgent(phase: PipelinePhase): string | null {
  const agents: Record<PipelinePhase, string | null> = {
    [PipelinePhase.None]: null,
    [PipelinePhase.Analysis]: 'CodeAnalysisAgent',
    [PipelinePhase.Planning]: 'PlannerAgent',
    [PipelinePhase.Coding]: 'CoderAgent',
    [PipelinePhase.Debugging]: 'DebuggerAgent',
    [PipelinePhase.Reviewing]: 'ReviewerAgent',
    [PipelinePhase.Deployment]: 'DeploymentAgent',
    [PipelinePhase.UnitTesting]: 'TestRunner',
    [PipelinePhase.PullRequest]: 'GitHubService',
    [PipelinePhase.Completed]: null
  };
  return agents[phase];
}

// Get retry reason label
export function getRetryReasonLabel(reason: RetryReason): string {
  const labels: Record<RetryReason, string> = {
    [RetryReason.BuildFailed]: 'Build Failed',
    [RetryReason.TestsFailed]: 'Tests Failed',
    [RetryReason.IntegrationFailed]: 'Integration Failed'
  };
  return labels[reason] || 'Unknown';
}

// Get retry action label
export function getRetryActionLabel(action: RetryAction): string {
  const labels: Record<RetryAction, string> = {
    [RetryAction.AutoFix]: 'Auto-fix and Retry',
    [RetryAction.ManualFix]: 'Manual Fix Required',
    [RetryAction.SkipTests]: 'Skip Tests and Continue',
    [RetryAction.Abort]: 'Abort Pipeline'
  };
  return labels[action] || 'Unknown';
}

// Get fix task type label
export function getFixTaskTypeLabel(type: FixTaskType): string {
  const labels: Record<FixTaskType, string> = {
    [FixTaskType.BuildError]: 'Build Error',
    [FixTaskType.TestFailure]: 'Test Failure',
    [FixTaskType.IntegrationError]: 'Integration Error'
  };
  return labels[type] || 'Unknown';
}

export function getPhaseStateColor(state: PhaseState): string {
  const colors: Record<PhaseState, string> = {
    [PhaseState.Pending]: 'text-slate-400',
    [PhaseState.WaitingApproval]: 'text-amber-400',
    [PhaseState.WaitingRetryApproval]: 'text-orange-400',
    [PhaseState.Running]: 'text-blue-400',
    [PhaseState.Completed]: 'text-emerald-400',
    [PhaseState.Failed]: 'text-red-400',
    [PhaseState.Skipped]: 'text-slate-500'
  };
  return colors[state] || 'text-slate-400';
}

// ════════════════════════════════════════════════════════════════════════════
// Requirement Types
// ════════════════════════════════════════════════════════════════════════════

export enum RequirementType {
  Feature = 0,
  Improvement = 1,
  Defect = 2,
  TechDebt = 3
}

export enum RequirementStatus {
  Draft = 0,
  InProgress = 1,
  Completed = 2,
  Cancelled = 3,
  Failed = 4
}

export enum WizardPhase {
  Input = 0,
  Analysis = 1,
  Questions = 2,
  Refinement = 3,
  Decomposition = 4,
  Review = 5,
  Completed = 6
}

export enum WizardPhaseState {
  Pending = 0,
  Running = 1,
  WaitingApproval = 2,
  Completed = 3,
  Failed = 4,
  Skipped = 5
}

export enum QuestionCategory {
  Functional = 0,
  NonFunctional = 1,
  Technical = 2,
  Business = 3,
  UX = 4
}

export enum QuestionType {
  Single = 0,
  Multiple = 1,
  Text = 2
}

export enum StoryComplexity {
  Small = 0,
  Medium = 1,
  Large = 2
}

export interface RequirementDto {
  id: string;
  title: string;
  rawContent: string;
  finalContent?: string;
  type: RequirementType;
  status: RequirementStatus;
  currentPhase: WizardPhase;
  codebaseId?: string;
  createdAt: string;
  updatedAt: string;
  completedAt?: string;
  storyCount: number;
  createdStoryIds: string[];
}

export interface RequirementDetailDto extends RequirementDto {
  questions?: QuestionSetDto;
  answers?: AnswerSetDto;
  aiNotes?: string;
  generatedStories: StoryDefinitionDto[];
}

export interface QuestionSetDto {
  questions: QuestionDto[];
}

export interface QuestionDto {
  id: string;
  category: QuestionCategory;
  text: string;
  type: QuestionType;
  options: string[];
  required: boolean;
  context?: string;
}

export interface AnswerSetDto {
  answers: AnswerDto[];
}

export interface AnswerDto {
  questionId: string;
  selectedOptions: string[];
  textResponse?: string;
}

export interface StoryDefinitionDto {
  id: string;
  title: string;
  description: string;
  acceptanceCriteria: string[];
  estimatedComplexity: StoryComplexity;
  dependencies: string[];
  technicalNotes?: string;
  selected: boolean;
}

export interface WizardStatusDto {
  requirementId: string;
  currentPhase: WizardPhase;
  isRunning: boolean;
  phases: WizardPhaseStatusDto[];
  startedAt?: string;
  completedAt?: string;
  error?: string;
}

export interface WizardPhaseStatusDto {
  phase: WizardPhase;
  state: WizardPhaseState;
  message?: string;
  startedAt?: string;
  completedAt?: string;
  result?: unknown;
}

export interface CreateRequirementRequest {
  title?: string;
  rawContent: string;
  type: RequirementType;
  codebaseId?: string;
}

export interface SubmitAnswersRequest {
  answers: AnswerDto[];
  aiNotes?: string;
}

export interface CreateStoriesRequest {
  selectedStoryIds: string[];
}

// Helper functions for Requirements
export function getRequirementTypeLabel(type: RequirementType): string {
  const labels: Record<RequirementType, string> = {
    [RequirementType.Feature]: 'Feature',
    [RequirementType.Improvement]: 'Improvement',
    [RequirementType.Defect]: 'Defect / Bug',
    [RequirementType.TechDebt]: 'Tech Debt / Refactor'
  };
  return labels[type] || 'Unknown';
}

export function getRequirementTypeColor(type: RequirementType): string {
  const colors: Record<RequirementType, string> = {
    [RequirementType.Feature]: 'bg-blue-500',
    [RequirementType.Improvement]: 'bg-emerald-500',
    [RequirementType.Defect]: 'bg-red-500',
    [RequirementType.TechDebt]: 'bg-amber-500'
  };
  return colors[type] || 'bg-slate-500';
}

export function getRequirementStatusLabel(status: RequirementStatus): string {
  const labels: Record<RequirementStatus, string> = {
    [RequirementStatus.Draft]: 'Draft',
    [RequirementStatus.InProgress]: 'In Progress',
    [RequirementStatus.Completed]: 'Completed',
    [RequirementStatus.Cancelled]: 'Cancelled',
    [RequirementStatus.Failed]: 'Failed'
  };
  return labels[status] || 'Unknown';
}

export function getRequirementStatusColor(status: RequirementStatus): string {
  const colors: Record<RequirementStatus, string> = {
    [RequirementStatus.Draft]: 'bg-slate-500',
    [RequirementStatus.InProgress]: 'bg-amber-500',
    [RequirementStatus.Completed]: 'bg-emerald-500',
    [RequirementStatus.Cancelled]: 'bg-slate-600',
    [RequirementStatus.Failed]: 'bg-red-500'
  };
  return colors[status] || 'bg-slate-500';
}

export function getWizardPhaseLabel(phase: WizardPhase): string {
  const labels: Record<WizardPhase, string> = {
    [WizardPhase.Input]: 'Input',
    [WizardPhase.Analysis]: 'Analysis',
    [WizardPhase.Questions]: 'Questions',
    [WizardPhase.Refinement]: 'Refinement',
    [WizardPhase.Decomposition]: 'Decomposition',
    [WizardPhase.Review]: 'Review',
    [WizardPhase.Completed]: 'Completed'
  };
  return labels[phase] || 'Unknown';
}

export function getWizardPhaseDescription(phase: WizardPhase): string {
  const descriptions: Record<WizardPhase, string> = {
    [WizardPhase.Input]: 'Enter raw requirement and select type',
    [WizardPhase.Analysis]: 'AI analyzing requirement and generating questions',
    [WizardPhase.Questions]: 'Answer clarifying questions',
    [WizardPhase.Refinement]: 'AI creating final requirement document',
    [WizardPhase.Decomposition]: 'AI breaking down into stories',
    [WizardPhase.Review]: 'Review and select stories to create',
    [WizardPhase.Completed]: 'Wizard completed'
  };
  return descriptions[phase] || '';
}

export function getWizardPhaseStateColor(state: WizardPhaseState): string {
  const colors: Record<WizardPhaseState, string> = {
    [WizardPhaseState.Pending]: 'text-slate-400',
    [WizardPhaseState.Running]: 'text-blue-400',
    [WizardPhaseState.WaitingApproval]: 'text-amber-400',
    [WizardPhaseState.Completed]: 'text-emerald-400',
    [WizardPhaseState.Failed]: 'text-red-400',
    [WizardPhaseState.Skipped]: 'text-slate-500'
  };
  return colors[state] || 'text-slate-400';
}

export function getQuestionCategoryLabel(category: QuestionCategory): string {
  const labels: Record<QuestionCategory, string> = {
    [QuestionCategory.Functional]: 'Functional',
    [QuestionCategory.NonFunctional]: 'Non-Functional',
    [QuestionCategory.Technical]: 'Technical',
    [QuestionCategory.Business]: 'Business',
    [QuestionCategory.UX]: 'UX'
  };
  return labels[category] || 'Unknown';
}

export function getStoryComplexityLabel(complexity: StoryComplexity): string {
  const labels: Record<StoryComplexity, string> = {
    [StoryComplexity.Small]: 'Small',
    [StoryComplexity.Medium]: 'Medium',
    [StoryComplexity.Large]: 'Large'
  };
  return labels[complexity] || 'Unknown';
}

export function getStoryComplexityColor(complexity: StoryComplexity): string {
  const colors: Record<StoryComplexity, string> = {
    [StoryComplexity.Small]: 'bg-emerald-500',
    [StoryComplexity.Medium]: 'bg-amber-500',
    [StoryComplexity.Large]: 'bg-red-500'
  };
  return colors[complexity] || 'bg-slate-500';
}

// ════════════════════════════════════════════════════════════════════════════
// Knowledge Base Types
// ════════════════════════════════════════════════════════════════════════════

export enum KnowledgeCategory {
  Pattern = 0,
  Error = 1,
  Template = 2,
  AgentInsight = 3
}

export enum PatternSubcategory {
  Logging = 0,
  Repository = 1,
  Validation = 2,
  ErrorHandling = 3,
  DependencyInjection = 4,
  ApiDesign = 5,
  Testing = 6,
  Configuration = 7,
  FileIO = 8,
  Other = 9
}

export enum ErrorType {
  Compilation = 0,
  Runtime = 1,
  TestFailure = 2,
  Integration = 3,
  Configuration = 4,
  Dependency = 5
}

export interface KnowledgeEntryDto {
  id: string;
  category: KnowledgeCategory;
  title: string;
  description: string;
  tags: string[];
  language: string;
  createdAt: string;
  lastUsedAt?: string;
  usageCount: number;
  successRate: number;
  isVerified: boolean;
  isManual: boolean;
  sourceStoryId?: string;
}

export interface SuccessfulPatternDto extends KnowledgeEntryDto {
  subcategory: PatternSubcategory;
  problemDescription: string;
  solutionCode: string;
  applicableScenarios: string[];
  exampleUsage?: string;
  dependencies: string[];
  relatedPatterns: string[];
}

export interface CommonErrorDto extends KnowledgeEntryDto {
  errorType: ErrorType;
  errorPattern: string;
  errorMessage?: string;
  rootCause: string;
  fixDescription: string;
  fixCode?: string;
  occurrenceCount: number;
  preventionTips: string[];
}

export interface ProjectTemplateDto extends KnowledgeEntryDto {
  templateType: string;
  targetFramework: string;
  templateFiles: TemplateFileDto[];
  packages: PackageInfoDto[];
  setupInstructions?: string;
}

export interface TemplateFileDto {
  path: string;
  content: string;
  isRequired: boolean;
}

export interface PackageInfoDto {
  name: string;
  version?: string;
  isRequired: boolean;
}

export interface KnowledgeStatsDto {
  totalEntries: number;
  patternsCount: number;
  errorsCount: number;
  templatesCount: number;
  insightsCount: number;
  verifiedCount: number;
  mostUsed: KnowledgeUsageStatDto[];
  recentlyAdded: KnowledgeEntrySummaryDto[];
  topTags: Record<string, number>;
}

export interface KnowledgeUsageStatDto {
  id: string;
  title: string;
  category: KnowledgeCategory;
  usageCount: number;
  successRate: number;
}

export interface KnowledgeEntrySummaryDto {
  id: string;
  title: string;
  category: KnowledgeCategory;
  createdAt: string;
  tags: string[];
}

export interface ErrorMatchResultDto {
  found: boolean;
  error?: CommonErrorDto;
  matchScore: number;
  matchedOn?: string;
}

export interface PatternSearchResultDto {
  patterns: SuccessfulPatternDto[];
  relevanceScores: Record<string, number>;
}

// Request types
export interface CreatePatternRequest {
  title: string;
  problemDescription: string;
  solutionCode: string;
  subcategory: PatternSubcategory;
  tags: string[];
  language: string;
  context?: string;
  applicableScenarios: string[];
  exampleUsage?: string;
  dependencies: string[];
}

export interface CreateErrorRequest {
  title: string;
  errorType: ErrorType;
  errorPattern: string;
  errorMessage?: string;
  rootCause: string;
  fixDescription: string;
  fixCode?: string;
  tags: string[];
  language: string;
  preventionTips: string[];
}

export interface SearchKnowledgeRequest {
  query?: string;
  category?: KnowledgeCategory;
  tags?: string[];
  language?: string;
  isVerified?: boolean;
  limit?: number;
  offset?: number;
}

// Helper functions for Knowledge
export function getKnowledgeCategoryLabel(category: KnowledgeCategory): string {
  const labels: Record<KnowledgeCategory, string> = {
    [KnowledgeCategory.Pattern]: 'Pattern',
    [KnowledgeCategory.Error]: 'Error Fix',
    [KnowledgeCategory.Template]: 'Template',
    [KnowledgeCategory.AgentInsight]: 'Agent Insight'
  };
  return labels[category] || 'Unknown';
}

export function getKnowledgeCategoryColor(category: KnowledgeCategory): string {
  const colors: Record<KnowledgeCategory, string> = {
    [KnowledgeCategory.Pattern]: 'bg-blue-500',
    [KnowledgeCategory.Error]: 'bg-red-500',
    [KnowledgeCategory.Template]: 'bg-purple-500',
    [KnowledgeCategory.AgentInsight]: 'bg-amber-500'
  };
  return colors[category] || 'bg-slate-500';
}

export function getKnowledgeCategoryIcon(category: KnowledgeCategory): string {
  const icons: Record<KnowledgeCategory, string> = {
    [KnowledgeCategory.Pattern]: '🧩',
    [KnowledgeCategory.Error]: '🐛',
    [KnowledgeCategory.Template]: '📋',
    [KnowledgeCategory.AgentInsight]: '🤖'
  };
  return icons[category] || '📄';
}

export function getPatternSubcategoryLabel(subcategory: PatternSubcategory): string {
  const labels: Record<PatternSubcategory, string> = {
    [PatternSubcategory.Logging]: 'Logging',
    [PatternSubcategory.Repository]: 'Repository',
    [PatternSubcategory.Validation]: 'Validation',
    [PatternSubcategory.ErrorHandling]: 'Error Handling',
    [PatternSubcategory.DependencyInjection]: 'Dependency Injection',
    [PatternSubcategory.ApiDesign]: 'API Design',
    [PatternSubcategory.Testing]: 'Testing',
    [PatternSubcategory.Configuration]: 'Configuration',
    [PatternSubcategory.FileIO]: 'File I/O',
    [PatternSubcategory.Other]: 'Other'
  };
  return labels[subcategory] || 'Unknown';
}

export function getErrorTypeLabel(errorType: ErrorType): string {
  const labels: Record<ErrorType, string> = {
    [ErrorType.Compilation]: 'Build Error',
    [ErrorType.Runtime]: 'Runtime Error',
    [ErrorType.TestFailure]: 'Test Failure',
    [ErrorType.Integration]: 'Integration Error',
    [ErrorType.Configuration]: 'Config Error',
    [ErrorType.Dependency]: 'Dependency Error'
  };
  return labels[errorType] || 'Unknown';
}

export function getErrorTypeColor(errorType: ErrorType): string {
  const colors: Record<ErrorType, string> = {
    [ErrorType.Compilation]: 'bg-red-500',
    [ErrorType.Runtime]: 'bg-orange-500',
    [ErrorType.TestFailure]: 'bg-amber-500',
    [ErrorType.Integration]: 'bg-purple-500',
    [ErrorType.Configuration]: 'bg-cyan-500',
    [ErrorType.Dependency]: 'bg-pink-500'
  };
  return colors[errorType] || 'bg-slate-500';
}
