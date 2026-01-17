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
  createdAt: string;
  lastProcessedAt?: string;
  tasks: TaskDto[];
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
