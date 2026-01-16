// API Types matching the backend models

export enum RequirementType {
  Single = 0
}

export enum RequirementStatus {
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

export enum PipelinePhase {
  None = 0,
  Analysis = 1,   // CodeAnalysisAgent - Codebase analysis (optional)
  Planning = 2,   // PlannerAgent - Task decomposition
  Coding = 3,     // CoderAgent - Code generation/modification
  Debugging = 4,  // DebuggerAgent - Testing and fixing
  Reviewing = 5,  // ReviewerAgent - Quality review
  Completed = 6
}

export enum PhaseState {
  Pending = 0,
  WaitingApproval = 1,
  Running = 2,
  Completed = 3,
  Failed = 4,
  Skipped = 5
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
  usesExisting?: string[];
  isModification?: boolean;  // true = modify existing file, false = create new file
  fullPath?: string;         // full path to target file for modifications
}

export interface RequirementDto {
  id: string;
  name: string;
  content: string;
  type?: RequirementType;  // Optional for backward compatibility
  status: RequirementStatus;
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
  requirementId: string;
  currentPhase: PipelinePhase;
  isRunning: boolean;
  phases: PhaseStatusDto[];
  startedAt?: string;
  completedAt?: string;
}

export interface PipelineUpdateMessage {
  requirementId: string;
  updateType: string;
  phase: PipelinePhase;
  message: string;
  data?: unknown;
  timestamp: string;
}

export interface CreateRequirementRequest {
  name: string;
  content: string;
  type?: RequirementType;  // Optional, defaults to Single
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
export function getStatusLabel(status: RequirementStatus): string {
  const labels: Record<RequirementStatus, string> = {
    [RequirementStatus.NotStarted]: 'Not Started',
    [RequirementStatus.Planned]: 'Planned',
    [RequirementStatus.Approved]: 'Approved',
    [RequirementStatus.InProgress]: 'In Progress',
    [RequirementStatus.Completed]: 'Completed',
    [RequirementStatus.Failed]: 'Failed'
  };
  return labels[status] || 'Unknown';
}

export function getStatusColor(status: RequirementStatus): string {
  const colors: Record<RequirementStatus, string> = {
    [RequirementStatus.NotStarted]: 'bg-slate-500',
    [RequirementStatus.Planned]: 'bg-blue-500',
    [RequirementStatus.Approved]: 'bg-emerald-500',
    [RequirementStatus.InProgress]: 'bg-amber-500',
    [RequirementStatus.Completed]: 'bg-green-500',
    [RequirementStatus.Failed]: 'bg-red-500'
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
    [PipelinePhase.Completed]: 'Completed'
  };
  return labels[phase] || 'Unknown';
}

// Get detailed phase description
export function getPhaseDescription(phase: PipelinePhase): string {
  const descriptions: Record<PipelinePhase, string> = {
    [PipelinePhase.None]: '',
    [PipelinePhase.Analysis]: 'CodeAnalysisAgent analyzing codebase structure and references',
    [PipelinePhase.Planning]: 'PlannerAgent decomposing requirements into tasks',
    [PipelinePhase.Coding]: 'CoderAgent generating or modifying code',
    [PipelinePhase.Debugging]: 'DebuggerAgent verifying and fixing code',
    [PipelinePhase.Reviewing]: 'ReviewerAgent checking code quality',
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
    [PipelinePhase.Completed]: null
  };
  return agents[phase];
}

export function getPhaseStateColor(state: PhaseState): string {
  const colors: Record<PhaseState, string> = {
    [PhaseState.Pending]: 'text-slate-400',
    [PhaseState.WaitingApproval]: 'text-amber-400',
    [PhaseState.Running]: 'text-blue-400',
    [PhaseState.Completed]: 'text-emerald-400',
    [PhaseState.Failed]: 'text-red-400',
    [PhaseState.Skipped]: 'text-slate-500'
  };
  return colors[state] || 'text-slate-400';
}
