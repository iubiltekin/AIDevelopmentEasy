import React, { useState } from 'react';
import { 
  Check, 
  Clock, 
  Loader2, 
  AlertCircle, 
  SkipForward, 
  ChevronDown, 
  ChevronRight,
  Search,
  ListTodo,
  Code,
  Bug,
  FileCheck,
  Rocket,
  TestTube,
  GitPullRequest,
  Circle
} from 'lucide-react';
import { 
  PipelineStatusDto, 
  PhaseStatusDto,
  PhaseState, 
  PipelinePhase,
  getPhaseLabel,
  getPhaseAgent
} from '../types';

// ============================================
// Shared Pipeline History Summary Component
// Used by both PipelineStatus (completed view) and RequirementDetail (history modal)
// ============================================

interface PipelineHistorySummaryProps {
  status: PipelineStatusDto;
  showSuccessBanner?: boolean;
  compact?: boolean;
}

// Phase Result Preview - displays JSON result
function PhaseResultPreview({ result }: { result: unknown }) {
  try {
    const jsonStr = typeof result === 'string' ? result : JSON.stringify(result, null, 2);
    return (
      <div className="p-3 bg-slate-900/50 rounded-lg border border-slate-700">
        <pre className="text-xs text-slate-400 overflow-auto max-h-60 whitespace-pre-wrap">
          {jsonStr}
        </pre>
      </div>
    );
  } catch {
    return null;
  }
}

// Get icon for each phase type
export function getPhaseTypeIcon(phase: PipelinePhase, size: string = "w-5 h-5"): React.ReactNode {
  const iconClass = size;
  switch (phase) {
    case PipelinePhase.Analysis:
      return <Search className={iconClass} />;
    case PipelinePhase.Planning:
      return <ListTodo className={iconClass} />;
    case PipelinePhase.Coding:
      return <Code className={iconClass} />;
    case PipelinePhase.Debugging:
      return <Bug className={iconClass} />;
    case PipelinePhase.Reviewing:
      return <FileCheck className={iconClass} />;
    case PipelinePhase.Deployment:
      return <Rocket className={iconClass} />;
    case PipelinePhase.UnitTesting:
      return <TestTube className={iconClass} />;
    case PipelinePhase.PullRequest:
      return <GitPullRequest className={iconClass} />;
    default:
      return <Circle className={iconClass} />;
  }
}

// Phase Card Component - displays a single phase with expandable details
interface PhaseCardProps {
  phase: PhaseStatusDto;
  index: number;
  isExpanded: boolean;
  onToggle: () => void;
}

function PhaseCard({ phase, index, isExpanded, onToggle }: PhaseCardProps) {
  const hasResult = phase.result != null;
  
  const getStateColor = (state: PhaseState) => {
    switch (state) {
      case PhaseState.Completed: return 'border-emerald-500/30 bg-emerald-500/5';
      case PhaseState.Skipped: return 'border-slate-600/50 bg-slate-800/30';
      case PhaseState.Failed: return 'border-red-500/30 bg-red-500/5';
      default: return 'border-slate-700 bg-slate-800/30';
    }
  };

  const getStateIcon = (state: PhaseState) => {
    switch (state) {
      case PhaseState.Completed: return <Check className="w-4 h-4 text-emerald-400" />;
      case PhaseState.Skipped: return <SkipForward className="w-4 h-4 text-slate-400" />;
      case PhaseState.Failed: return <AlertCircle className="w-4 h-4 text-red-400" />;
      case PhaseState.Running: return <Loader2 className="w-4 h-4 text-blue-400 animate-spin" />;
      case PhaseState.WaitingApproval: return <Clock className="w-4 h-4 text-amber-400" />;
      default: return <Clock className="w-4 h-4 text-slate-500" />;
    }
  };

  const getStateBadge = (state: PhaseState) => {
    switch (state) {
      case PhaseState.Completed: return { text: 'Completed', class: 'bg-emerald-500/20 text-emerald-400' };
      case PhaseState.Skipped: return { text: 'Skipped', class: 'bg-slate-600/50 text-slate-400' };
      case PhaseState.Failed: return { text: 'Failed', class: 'bg-red-500/20 text-red-400' };
      case PhaseState.Running: return { text: 'Running', class: 'bg-blue-500/20 text-blue-400' };
      case PhaseState.WaitingApproval: return { text: 'Waiting', class: 'bg-amber-500/20 text-amber-400' };
      default: return { text: 'Pending', class: 'bg-slate-600/50 text-slate-400' };
    }
  };

  const badge = getStateBadge(phase.state);

  return (
    <div className={`rounded-lg border ${getStateColor(phase.state)} overflow-hidden transition-all`}>
      <div 
        className={`p-3 flex items-center gap-3 ${hasResult ? 'cursor-pointer hover:bg-slate-800/30' : ''}`}
        onClick={hasResult ? onToggle : undefined}
      >
        {/* Phase Icon with Type */}
        <div className={`w-10 h-10 rounded-lg flex items-center justify-center ${
          phase.state === PhaseState.Completed ? 'bg-emerald-500/20 text-emerald-400' :
          phase.state === PhaseState.Skipped ? 'bg-slate-600/30 text-slate-500' :
          phase.state === PhaseState.Failed ? 'bg-red-500/20 text-red-400' :
          phase.state === PhaseState.Running ? 'bg-blue-500/20 text-blue-400' :
          phase.state === PhaseState.WaitingApproval ? 'bg-amber-500/20 text-amber-400' :
          'bg-slate-700/50 text-slate-400'
        }`}>
          {getPhaseTypeIcon(phase.phase, "w-5 h-5")}
        </div>

        {/* Phase Info */}
        <div className="flex-1 min-w-0">
          <div className="flex items-center gap-2">
            <span className="font-medium text-slate-200">{index + 1}. {getPhaseLabel(phase.phase)}</span>
            <span className={`text-xs px-2 py-0.5 rounded ${badge.class}`}>
              {badge.text}
            </span>
          </div>
          <div className="text-xs text-slate-500 flex items-center gap-2">
            <span>{getPhaseAgent(phase.phase) || 'System'}</span>
            {phase.completedAt && (
              <>
                <span>•</span>
                <span>{new Date(phase.completedAt).toLocaleTimeString()}</span>
              </>
            )}
          </div>
          {phase.message && (
            <div className="text-xs text-slate-400 mt-1 truncate">{phase.message}</div>
          )}
        </div>

        {/* State Icon */}
        <div className="flex items-center gap-2">
          {getStateIcon(phase.state)}
          {hasResult && (
            <div className="text-slate-500">
              {isExpanded ? (
                <ChevronDown className="w-5 h-5" />
              ) : (
                <ChevronRight className="w-5 h-5" />
              )}
            </div>
          )}
        </div>
      </div>

      {/* Expanded Details */}
      {isExpanded && hasResult && (
        <div className="px-3 pb-3 border-t border-slate-700/50">
          <div className="pt-3">
            {/* Timestamps */}
            <div className="flex gap-4 text-xs text-slate-500 mb-2">
              {phase.startedAt && (
                <span>Started: {new Date(phase.startedAt).toLocaleString()}</span>
              )}
              {phase.completedAt && (
                <span>Completed: {new Date(phase.completedAt).toLocaleString()}</span>
              )}
            </div>
            <div className="text-xs text-slate-500 mb-2 flex items-center gap-1">
              <Code className="w-3 h-3" />
              Phase Result JSON
            </div>
            <PhaseResultPreview result={phase.result} />
          </div>
        </div>
      )}
    </div>
  );
}

// Main Component - Pipeline History Summary
export function PipelineHistorySummary({ 
  status, 
  showSuccessBanner = true,
  compact = false 
}: PipelineHistorySummaryProps) {
  const [expandedPhases, setExpandedPhases] = useState<Set<PipelinePhase>>(new Set());

  const togglePhaseDetails = (phase: PipelinePhase) => {
    setExpandedPhases(prev => {
      const next = new Set(prev);
      if (next.has(phase)) {
        next.delete(phase);
      } else {
        next.add(phase);
      }
      return next;
    });
  };

  return (
    <div className="space-y-4">
      {/* Success Banner */}
      {showSuccessBanner && (
        <div className={`p-4 bg-emerald-500/10 border border-emerald-500/30 rounded-xl`}>
          <div className="flex items-center gap-3">
            <div className={`${compact ? 'w-10 h-10' : 'w-12 h-12'} bg-emerald-500/20 rounded-full flex items-center justify-center`}>
              <Check className={`${compact ? 'w-6 h-6' : 'w-8 h-8'} text-emerald-400`} />
            </div>
            <div>
              <h3 className={`${compact ? 'text-lg' : 'text-xl'} font-bold text-emerald-400`}>
                Pipeline Completed Successfully! 🎉
              </h3>
              <p className="text-sm text-slate-400">
                {status.phases.length} phases processed
                {status.startedAt && ` • Started: ${new Date(status.startedAt).toLocaleString()}`}
                {status.completedAt && ` • Completed: ${new Date(status.completedAt).toLocaleString()}`}
              </p>
            </div>
          </div>
        </div>
      )}

      {/* Phase Summary Cards */}
      <div className="space-y-2">
        <h4 className="text-sm font-semibold text-slate-400 mb-3">Phase Details (click to expand)</h4>
        {status.phases.map((phase, index) => (
          <PhaseCard
            key={phase.phase}
            phase={phase}
            index={index}
            isExpanded={expandedPhases.has(phase.phase)}
            onToggle={() => togglePhaseDetails(phase.phase)}
          />
        ))}
      </div>
    </div>
  );
}

// Export for use in other components
export default PipelineHistorySummary;
