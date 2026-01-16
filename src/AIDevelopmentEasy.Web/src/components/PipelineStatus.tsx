import { Check, Clock, Loader2, AlertCircle, SkipForward, Circle, Search, ListTodo, Code, Bug, FileCheck, Rocket } from 'lucide-react';
import { 
  PipelineStatusDto, 
  PhaseState, 
  PipelinePhase,
  getPhaseLabel,
  getPhaseAgent
} from '../types';

interface PipelineStatusProps {
  status: PipelineStatusDto;
  onApprove: (phase: PipelinePhase) => void;
  onReject: (phase: PipelinePhase) => void;
}

// Component to safely display phase result data
function PhaseResultPreview({ result }: { result: unknown }) {
  try {
    const jsonStr = JSON.stringify(result, null, 2);
    return (
      <div className="mb-4 p-3 bg-slate-800/50 rounded-lg">
        <pre className="text-xs text-slate-400 overflow-auto max-h-40">
          {jsonStr}
        </pre>
      </div>
    );
  } catch {
    return null;
  }
}

export function PipelineStatus({ status, onApprove, onReject }: PipelineStatusProps) {
  // Get icon based on phase type (for running state)
  const getPhaseTypeIcon = (phase: PipelinePhase) => {
    switch (phase) {
      case PipelinePhase.Analysis:
        return <Search className="w-5 h-5" />;
      case PipelinePhase.Planning:
        return <ListTodo className="w-5 h-5" />;
      case PipelinePhase.Coding:
        return <Code className="w-5 h-5" />;
      case PipelinePhase.Debugging:
        return <Bug className="w-5 h-5" />;
      case PipelinePhase.Reviewing:
        return <FileCheck className="w-5 h-5" />;
      case PipelinePhase.Deployment:
        return <Rocket className="w-5 h-5" />;
      default:
        return <Circle className="w-5 h-5" />;
    }
  };

  const getPhaseIcon = (state: PhaseState, phase: PipelinePhase) => {
    switch (state) {
      case PhaseState.Completed:
        return <Check className="w-5 h-5 text-emerald-400" />;
      case PhaseState.Running:
        return <Loader2 className="w-5 h-5 text-blue-400 animate-spin" />;
      case PhaseState.WaitingApproval:
        return <Clock className="w-5 h-5 text-amber-400" />;
      case PhaseState.Failed:
        return <AlertCircle className="w-5 h-5 text-red-400" />;
      case PhaseState.Skipped:
        return <SkipForward className="w-5 h-5 text-slate-500" />;
      default:
        return <span className="text-slate-600">{getPhaseTypeIcon(phase)}</span>;
    }
  };

  const getPhaseStateLabel = (state: PhaseState) => {
    const labels: Record<PhaseState, string> = {
      [PhaseState.Pending]: 'Pending',
      [PhaseState.WaitingApproval]: 'Waiting Approval',
      [PhaseState.Running]: 'Running',
      [PhaseState.Completed]: 'Completed',
      [PhaseState.Failed]: 'Failed',
      [PhaseState.Skipped]: 'Skipped'
    };
    return labels[state];
  };

  return (
    <div className="space-y-4">
      {/* Progress Bar */}
      <div className="relative">
        <div className="absolute top-5 left-5 right-5 h-0.5 bg-slate-700" />
        <div 
          className="absolute top-5 left-5 h-0.5 bg-blue-500 transition-all duration-500"
          style={{
            width: `${(status.phases.filter(p => p.state === PhaseState.Completed).length / status.phases.length) * 100}%`
          }}
        />
        <div className="relative flex justify-between">
          {status.phases.map((phase, index) => (
            <div key={phase.phase} className="flex flex-col items-center" style={{ animationDelay: `${index * 100}ms` }}>
              <div 
                className={`w-10 h-10 rounded-full flex items-center justify-center transition-all duration-300 ${
                  phase.state === PhaseState.Completed ? 'bg-emerald-500/20 ring-2 ring-emerald-500' :
                  phase.state === PhaseState.Running ? 'bg-blue-500/20 ring-2 ring-blue-500 animate-pulse-glow' :
                  phase.state === PhaseState.WaitingApproval ? 'bg-amber-500/20 ring-2 ring-amber-500' :
                  phase.state === PhaseState.Failed ? 'bg-red-500/20 ring-2 ring-red-500' :
                  'bg-slate-800'
                }`}
              >
                {getPhaseIcon(phase.state, phase.phase)}
              </div>
              <span className="mt-2 text-xs font-medium text-slate-300">
                {getPhaseLabel(phase.phase)}
              </span>
              <span className="text-[10px] text-slate-500">
                {getPhaseAgent(phase.phase) || ''}
              </span>
              <span className={`text-xs ${
                phase.state === PhaseState.Completed ? 'text-emerald-400' :
                phase.state === PhaseState.Running ? 'text-blue-400' :
                phase.state === PhaseState.WaitingApproval ? 'text-amber-400' :
                phase.state === PhaseState.Failed ? 'text-red-400' :
                'text-slate-500'
              }`}>
                {getPhaseStateLabel(phase.state)}
              </span>
            </div>
          ))}
        </div>
      </div>

      {/* Current Phase Details */}
      {status.phases
        .filter(phase => phase.state === PhaseState.WaitingApproval)
        .map(phase => (
          <div 
            key={`approval-${phase.phase}`}
            className="mt-6 p-4 bg-amber-500/10 border border-amber-500/30 rounded-xl animate-slide-in"
          >
            <div className="flex items-center gap-2 mb-2">
              <span className="text-amber-400">{getPhaseTypeIcon(phase.phase)}</span>
              <h3 className="text-lg font-semibold text-amber-400">
                {getPhaseLabel(phase.phase)} Complete - Waiting for Approval
              </h3>
            </div>
            <div className="text-xs text-slate-400 mb-3">
              Agent: <span className="text-amber-300">{getPhaseAgent(phase.phase) || 'N/A'}</span>
            </div>
            {phase.message && (
              <p className="text-slate-300 mb-4">{phase.message}</p>
            )}
            {phase.result != null && (
              <PhaseResultPreview result={phase.result} />
            )}
            <div className="flex gap-3">
              <button
                onClick={() => onApprove(phase.phase)}
                className="px-6 py-2 bg-emerald-600 hover:bg-emerald-700 text-white font-medium rounded-lg transition-colors"
              >
                ✓ Approve & Continue
              </button>
              <button
                onClick={() => onReject(phase.phase)}
                className="px-6 py-2 bg-slate-600 hover:bg-slate-700 text-white font-medium rounded-lg transition-colors"
              >
                ✗ Reject
              </button>
            </div>
          </div>
        ))}

      {/* Running Phase */}
      {status.phases.some(p => p.state === PhaseState.Running) && (
        <div className="mt-6 p-4 bg-blue-500/10 border border-blue-500/30 rounded-xl">
          <div className="flex items-center gap-3">
            <Loader2 className="w-6 h-6 text-blue-400 animate-spin" />
            <div>
              <span className="text-blue-300 font-medium">
                {getPhaseLabel(status.currentPhase)} in progress...
              </span>
              <div className="text-xs text-slate-400">
                Running: <span className="text-blue-300">{getPhaseAgent(status.currentPhase) || 'N/A'}</span>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* Completed */}
      {status.currentPhase === PipelinePhase.Completed && (
        <div className="mt-6 p-4 bg-emerald-500/10 border border-emerald-500/30 rounded-xl">
          <div className="flex items-center gap-3">
            <Check className="w-6 h-6 text-emerald-400" />
            <span className="text-emerald-300 font-medium">
              Pipeline completed successfully! 🎉
            </span>
          </div>
        </div>
      )}
    </div>
  );
}
