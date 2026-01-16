// API Types matching the backend models

export enum RequirementType {
  Single = 0,
  Multi = 1
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
  Planning = 1,
  Coding = 2,
  Debugging = 3,
  Testing = 4,
  Reviewing = 5,
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
  status: TaskStatus;
}

export interface RequirementDto {
  id: string;
  name: string;
  content: string;
  type: RequirementType;
  status: RequirementStatus;
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
  type: RequirementType;
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
    [PipelinePhase.Planning]: 'Planning',
    [PipelinePhase.Coding]: 'Coding',
    [PipelinePhase.Debugging]: 'Debugging',
    [PipelinePhase.Testing]: 'Testing',
    [PipelinePhase.Reviewing]: 'Reviewing',
    [PipelinePhase.Completed]: 'Completed'
  };
  return labels[phase] || 'Unknown';
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
