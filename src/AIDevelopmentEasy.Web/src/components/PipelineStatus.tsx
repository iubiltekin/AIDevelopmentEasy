import { Check, Clock, Loader2, AlertCircle, SkipForward, Circle } from 'lucide-react';
import { 
  PipelineStatusDto, 
  PhaseState, 
  PipelinePhase,
  getPhaseLabel 
} from '../types';

interface PipelineStatusProps {
  status: PipelineStatusDto;
  onApprove: (phase: PipelinePhase) => void;
  onReject: (phase: PipelinePhase) => void;
}

export function PipelineStatus({ status, onApprove, onReject }: PipelineStatusProps) {
  const getPhaseIcon = (state: PhaseState) => {
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
        return <Circle className="w-5 h-5 text-slate-600" />;
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
                {getPhaseIcon(phase.state)}
              </div>
              <span className="mt-2 text-xs font-medium text-slate-300">
                {getPhaseLabel(phase.phase)}
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
      {status.phases.map(phase => (
        phase.state === PhaseState.WaitingApproval && (
          <div 
            key={`approval-${phase.phase}`}
            className="mt-6 p-4 bg-amber-500/10 border border-amber-500/30 rounded-xl animate-slide-in"
          >
            <h3 className="text-lg font-semibold text-amber-400 mb-2">
              ⏳ {getPhaseLabel(phase.phase)} - Waiting for Approval
            </h3>
            {phase.message && (
              <p className="text-slate-300 mb-4">{phase.message}</p>
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
        )
      ))}

      {/* Running Phase */}
      {status.phases.some(p => p.state === PhaseState.Running) && (
        <div className="mt-6 p-4 bg-blue-500/10 border border-blue-500/30 rounded-xl">
          <div className="flex items-center gap-3">
            <Loader2 className="w-6 h-6 text-blue-400 animate-spin" />
            <span className="text-blue-300 font-medium">
              {getPhaseLabel(status.currentPhase)} in progress...
            </span>
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
