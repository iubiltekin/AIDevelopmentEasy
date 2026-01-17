import React, { useState } from 'react';
import { Check, Clock, Loader2, AlertCircle, SkipForward, Circle, Search, ListTodo, Code, Bug, FileCheck, Rocket, TestTube, GitPullRequest, RefreshCw, AlertTriangle, Wrench, ChevronDown, ChevronRight, Eye } from 'lucide-react';
import { 
  PipelineStatusDto, 
  PhaseState, 
  PipelinePhase,
  PhaseStatusDto,
  RetryAction,
  FixTaskDto,
  TestSummaryDto,
  getPhaseLabel,
  getPhaseAgent,
  getRetryReasonLabel,
  getFixTaskTypeLabel
} from '../types';

interface PipelineStatusProps {
  status: PipelineStatusDto;
  onApprove: (phase: PipelinePhase) => void;
  onReject: (phase: PipelinePhase) => void;
  onApproveRetry?: (action: RetryAction) => void;
}

// Component to safely display phase result data
function PhaseResultPreview({ result }: { result: unknown }) {
  try {
    const jsonStr = JSON.stringify(result, null, 2);
    return (
      <div className="p-3 bg-slate-900/50 rounded-lg">
        <pre className="text-xs text-slate-400 overflow-auto max-h-60">
          {jsonStr}
        </pre>
      </div>
    );
  } catch {
    return null;
  }
}

export function PipelineStatus({ status, onApprove, onReject, onApproveRetry }: PipelineStatusProps) {
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
      case PipelinePhase.UnitTesting:
        return <TestTube className="w-5 h-5" />;
      case PipelinePhase.PullRequest:
        return <GitPullRequest className="w-5 h-5" />;
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
      case PhaseState.WaitingRetryApproval:
        return <RefreshCw className="w-5 h-5 text-orange-400" />;
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
      [PhaseState.WaitingRetryApproval]: 'Retry Required',
      [PhaseState.Running]: 'Running',
      [PhaseState.Completed]: 'Completed',
      [PhaseState.Failed]: 'Failed',
      [PhaseState.Skipped]: 'Skipped'
    };
    return labels[state];
  };

  // Calculate progress - include completed pipeline as 100%
  const isCompleted = status.currentPhase === PipelinePhase.Completed;
  const completedCount = status.phases.filter(p => 
    p.state === PhaseState.Completed || p.state === PhaseState.Skipped
  ).length;
  const progressPercent = isCompleted ? 100 : (completedCount / status.phases.length) * 100;

  return (
    <div className="space-y-4">
      {/* Progress Bar with Percentage */}
      <div className="mb-2">
        <div className="flex justify-between items-center mb-1">
          <span className="text-sm text-slate-400">Pipeline Progress</span>
          <span className={`text-sm font-bold ${isCompleted ? 'text-emerald-400' : 'text-blue-400'}`}>
            {Math.round(progressPercent)}%
          </span>
        </div>
        <div className="relative h-2 bg-slate-700 rounded-full overflow-hidden">
          <div 
            className={`absolute top-0 left-0 h-full transition-all duration-500 rounded-full ${
              isCompleted ? 'bg-emerald-500' : 'bg-blue-500'
            }`}
            style={{ width: `${progressPercent}%` }}
          />
        </div>
      </div>

      {/* Phase Steps */}
      <div className="relative">
        <div className="absolute top-5 left-5 right-5 h-0.5 bg-slate-700" />
        <div 
          className={`absolute top-5 left-5 h-0.5 transition-all duration-500 ${
            isCompleted ? 'bg-emerald-500' : 'bg-blue-500'
          }`}
          style={{ width: `${progressPercent}%` }}
        />
        <div className="relative flex justify-between">
          {status.phases.map((phase, index) => (
            <div key={phase.phase} className="flex flex-col items-center" style={{ animationDelay: `${index * 100}ms` }}>
              <div 
                className={`w-10 h-10 rounded-full flex items-center justify-center transition-all duration-300 ${
                  phase.state === PhaseState.Completed ? 'bg-emerald-500/20 ring-2 ring-emerald-500' :
                  phase.state === PhaseState.Running ? 'bg-blue-500/20 ring-2 ring-blue-500 animate-pulse-glow' :
                  phase.state === PhaseState.WaitingApproval ? 'bg-amber-500/20 ring-2 ring-amber-500' :
                  phase.state === PhaseState.WaitingRetryApproval ? 'bg-orange-500/20 ring-2 ring-orange-500 animate-pulse' :
                  phase.state === PhaseState.Failed ? 'bg-red-500/20 ring-2 ring-red-500' :
                  phase.state === PhaseState.Skipped ? 'bg-slate-600/50 ring-2 ring-slate-600' :
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
                phase.state === PhaseState.WaitingRetryApproval ? 'text-orange-400' :
                phase.state === PhaseState.Failed ? 'text-red-400' :
                'text-slate-500'
              }`}>
                {getPhaseStateLabel(phase.state)}
              </span>
            </div>
          ))}
        </div>
      </div>

      {/* Current Phase Details - Waiting Approval */}
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
                {phase.phase === PipelinePhase.PullRequest 
                  ? 'Deployment Successful - Create PR?' 
                  : `${getPhaseLabel(phase.phase)} Complete - Waiting for Approval`}
              </h3>
            </div>
            <div className="text-xs text-slate-400 mb-3">
              Agent: <span className="text-amber-300">{getPhaseAgent(phase.phase) || 'N/A'}</span>
            </div>
            {phase.message && (
              <p className="text-slate-300 mb-4">{phase.message}</p>
            )}
            {phase.result != null && (
              <div className="mb-4">
                <PhaseResultPreview result={phase.result} />
              </div>
            )}
            
            {/* Special UI for PullRequest phase */}
            {phase.phase === PipelinePhase.PullRequest ? (
              <div className="space-y-3">
                <p className="text-slate-400 text-sm">
                  Your changes have been deployed to the codebase. You can create a GitHub PR or complete without one.
                </p>
                <div className="flex gap-3">
                  <button
                    onClick={() => onApprove(phase.phase)}
                    className="flex items-center gap-2 px-6 py-2 bg-purple-600 hover:bg-purple-700 text-white font-medium rounded-lg transition-colors"
                  >
                    <GitPullRequest className="w-4 h-4" />
                    Create GitHub PR
                  </button>
                  <button
                    onClick={() => onReject(phase.phase)}
                    className="flex items-center gap-2 px-6 py-2 bg-emerald-600 hover:bg-emerald-700 text-white font-medium rounded-lg transition-colors"
                  >
                    <Check className="w-4 h-4" />
                    Complete without PR
                  </button>
                </div>
                <p className="text-xs text-slate-500">
                  💡 GitHub PR integration is optional. You can always create a PR manually later.
                </p>
              </div>
            ) : phase.phase === PipelinePhase.UnitTesting ? (
              /* Special UI for UnitTesting phase - with Rollback option */
              <div className="space-y-3">
                <div className="flex gap-3">
                  <button
                    onClick={() => onApprove(phase.phase)}
                    className="flex items-center gap-2 px-6 py-2 bg-emerald-600 hover:bg-emerald-700 text-white font-medium rounded-lg transition-colors"
                  >
                    <Check className="w-4 h-4" />
                    Tests Passed - Continue
                  </button>
                  <button
                    onClick={() => onReject(phase.phase)}
                    className="flex items-center gap-2 px-6 py-2 bg-red-600 hover:bg-red-700 text-white font-medium rounded-lg transition-colors"
                  >
                    <RefreshCw className="w-4 h-4" />
                    Reject & Rollback
                  </button>
                </div>
                <p className="text-xs text-slate-500">
                  ⚠️ Reject will rollback all deployment changes (delete new files, revert modified files).
                </p>
              </div>
            ) : (
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
            )}
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

      {/* Completed - Full Summary Panel */}
      {isCompleted && (
        <CompletedSummaryPanel 
          phases={status.phases}
          expandedPhases={expandedPhases}
          onToggle={togglePhaseDetails}
          getPhaseTypeIcon={getPhaseTypeIcon}
        />
      )}

      {/* Retry Panel */}
      {status.retryInfo && onApproveRetry && (
        <RetryPanel 
          retryInfo={status.retryInfo}
          onApproveRetry={onApproveRetry}
        />
      )}
    </div>
  );
}

// Completed Summary Panel Component
interface CompletedSummaryPanelProps {
  phases: PhaseStatusDto[];
  expandedPhases: Set<PipelinePhase>;
  onToggle: (phase: PipelinePhase) => void;
  getPhaseTypeIcon: (phase: PipelinePhase) => React.ReactNode;
}

function CompletedSummaryPanel({ phases, expandedPhases, onToggle, getPhaseTypeIcon }: CompletedSummaryPanelProps) {
  return (
    <div className="mt-6 space-y-4">
      {/* Success Banner */}
      <div className="p-4 bg-emerald-500/10 border border-emerald-500/30 rounded-xl">
        <div className="flex items-center gap-3">
          <div className="w-12 h-12 bg-emerald-500/20 rounded-full flex items-center justify-center">
            <Check className="w-8 h-8 text-emerald-400" />
          </div>
          <div>
            <h3 className="text-xl font-bold text-emerald-400">
              Pipeline Completed Successfully! 🎉
            </h3>
            <p className="text-sm text-slate-400">
              All {phases.length} phases have been processed
            </p>
          </div>
        </div>
      </div>

      {/* Phase Summary List */}
      <div className="bg-slate-800/50 border border-slate-700 rounded-xl overflow-hidden">
        <div className="p-3 bg-slate-800 border-b border-slate-700">
          <h4 className="text-sm font-semibold text-slate-300 flex items-center gap-2">
            <ListTodo className="w-4 h-4" />
            Phase Summary
          </h4>
        </div>
        <div className="divide-y divide-slate-700">
          {phases.map((phase, index) => (
            <PhaseDetailRow
              key={phase.phase}
              phase={phase}
              index={index}
              isExpanded={expandedPhases.has(phase.phase)}
              onToggle={() => onToggle(phase.phase)}
              getPhaseTypeIcon={getPhaseTypeIcon}
            />
          ))}
        </div>
      </div>
    </div>
  );
}

// Phase Detail Row Component
interface PhaseDetailRowProps {
  phase: PhaseStatusDto;
  index: number;
  isExpanded: boolean;
  onToggle: () => void;
  getPhaseTypeIcon: (phase: PipelinePhase) => React.ReactNode;
}

function PhaseDetailRow({ phase, index, isExpanded, onToggle, getPhaseTypeIcon }: PhaseDetailRowProps) {
  const hasResult = phase.result != null;
  
  const getStateColor = (state: PhaseState) => {
    switch (state) {
      case PhaseState.Completed: return 'text-emerald-400 bg-emerald-500/10';
      case PhaseState.Skipped: return 'text-slate-400 bg-slate-600/30';
      case PhaseState.Failed: return 'text-red-400 bg-red-500/10';
      default: return 'text-slate-400 bg-slate-600/30';
    }
  };

  const getStateBadge = (state: PhaseState) => {
    switch (state) {
      case PhaseState.Completed: return '✓ Completed';
      case PhaseState.Skipped: return '⏭ Skipped';
      case PhaseState.Failed: return '✗ Failed';
      default: return state;
    }
  };

  return (
    <div className="transition-colors hover:bg-slate-800/30">
      <div 
        className={`p-3 flex items-center gap-3 ${hasResult ? 'cursor-pointer' : ''}`}
        onClick={hasResult ? onToggle : undefined}
      >
        {/* Phase Number */}
        <div className="w-6 h-6 rounded-full bg-slate-700 flex items-center justify-center text-xs font-bold text-slate-400">
          {index + 1}
        </div>

        {/* Phase Icon */}
        <div className={`w-8 h-8 rounded-lg flex items-center justify-center ${getStateColor(phase.state)}`}>
          {getPhaseTypeIcon(phase.phase)}
        </div>

        {/* Phase Info */}
        <div className="flex-1 min-w-0">
          <div className="flex items-center gap-2">
            <span className="font-medium text-slate-200">{getPhaseLabel(phase.phase)}</span>
            <span className={`text-xs px-2 py-0.5 rounded ${getStateColor(phase.state)}`}>
              {getStateBadge(phase.state)}
            </span>
          </div>
          <div className="text-xs text-slate-500">
            {getPhaseAgent(phase.phase) || 'System'}
            {phase.message && <span className="ml-2 text-slate-400">• {phase.message}</span>}
          </div>
        </div>

        {/* Time Info */}
        {phase.completedAt && (
          <div className="text-xs text-slate-500">
            {new Date(phase.completedAt).toLocaleTimeString()}
          </div>
        )}

        {/* Expand Button */}
        {hasResult && (
          <button className="p-1 hover:bg-slate-700 rounded transition-colors">
            {isExpanded ? (
              <ChevronDown className="w-5 h-5 text-slate-400" />
            ) : (
              <ChevronRight className="w-5 h-5 text-slate-400" />
            )}
          </button>
        )}

        {/* View Button (alternative) */}
        {hasResult && !isExpanded && (
          <Eye className="w-4 h-4 text-slate-500" />
        )}
      </div>

      {/* Expanded Details */}
      {isExpanded && hasResult && (
        <div className="px-3 pb-3 pl-[60px]">
          <div className="text-xs text-slate-500 mb-2">Phase Result Data:</div>
          <PhaseResultPreview result={phase.result} />
        </div>
      )}
    </div>
  );
}

// Retry Panel Component
interface RetryPanelProps {
  retryInfo: {
    currentAttempt: number;
    maxAttempts: number;
    reason: number;
    fixTasks: FixTaskDto[];
    lastError?: string;
    testSummary?: TestSummaryDto;
    lastAttemptAt?: string;
  };
  onApproveRetry: (action: RetryAction) => void;
}

function RetryPanel({ retryInfo, onApproveRetry }: RetryPanelProps) {
  const isBreakingChange = retryInfo.testSummary?.isBreakingChange;

  return (
    <div className="mt-6 p-4 bg-orange-500/10 border border-orange-500/30 rounded-xl animate-slide-in">
      <div className="flex items-center gap-2 mb-4">
        <RefreshCw className="w-6 h-6 text-orange-400" />
        <h3 className="text-lg font-semibold text-orange-400">
          Retry Required - Attempt {retryInfo.currentAttempt}/{retryInfo.maxAttempts}
        </h3>
      </div>

      {/* Breaking Change Warning */}
      {isBreakingChange && (
        <div className="mb-4 p-3 bg-red-500/20 border border-red-500/40 rounded-lg">
          <div className="flex items-center gap-2 text-red-400">
            <AlertTriangle className="w-5 h-5" />
            <span className="font-semibold">⚠️ BREAKING CHANGE DETECTED!</span>
          </div>
          <p className="mt-1 text-sm text-red-300">
            {retryInfo.testSummary?.existingTestsFailed} existing test(s) are now failing. 
            The changes may have broken existing functionality.
          </p>
        </div>
      )}

      {/* Error Message */}
      {retryInfo.lastError && (
        <div className="mb-4 p-3 bg-slate-800/50 rounded-lg">
          <p className="text-sm text-red-400">{retryInfo.lastError}</p>
        </div>
      )}

      {/* Test Summary */}
      {retryInfo.testSummary && (
        <TestSummaryPanel summary={retryInfo.testSummary} />
      )}

      {/* Fix Tasks */}
      {retryInfo.fixTasks.length > 0 && (
        <FixTaskList tasks={retryInfo.fixTasks} />
      )}

      {/* Action Buttons */}
      <div className="mt-4 flex flex-wrap gap-3">
        <button
          onClick={() => onApproveRetry(RetryAction.AutoFix)}
          className="flex items-center gap-2 px-4 py-2 bg-orange-600 hover:bg-orange-700 text-white font-medium rounded-lg transition-colors"
        >
          <Wrench className="w-4 h-4" />
          Auto-fix & Retry
        </button>
        <button
          onClick={() => onApproveRetry(RetryAction.SkipTests)}
          className="flex items-center gap-2 px-4 py-2 bg-amber-600 hover:bg-amber-700 text-white font-medium rounded-lg transition-colors"
        >
          <SkipForward className="w-4 h-4" />
          Skip Tests
        </button>
        <button
          onClick={() => onApproveRetry(RetryAction.ManualFix)}
          className="flex items-center gap-2 px-4 py-2 bg-slate-600 hover:bg-slate-700 text-white font-medium rounded-lg transition-colors"
        >
          <Code className="w-4 h-4" />
          Manual Fix
        </button>
        <button
          onClick={() => onApproveRetry(RetryAction.Abort)}
          className="flex items-center gap-2 px-4 py-2 bg-red-600 hover:bg-red-700 text-white font-medium rounded-lg transition-colors"
        >
          <AlertCircle className="w-4 h-4" />
          Abort
        </button>
      </div>

      <p className="mt-3 text-xs text-slate-500">
        {getRetryReasonLabel(retryInfo.reason)} at {retryInfo.lastAttemptAt ? new Date(retryInfo.lastAttemptAt).toLocaleTimeString() : 'N/A'}
      </p>
    </div>
  );
}

// Test Summary Panel
function TestSummaryPanel({ summary }: { summary: TestSummaryDto }) {
  return (
    <div className="mb-4 p-3 bg-slate-800/50 rounded-lg">
      <h4 className="text-sm font-semibold text-slate-300 mb-2 flex items-center gap-2">
        <TestTube className="w-4 h-4" />
        Test Results
      </h4>
      <div className="grid grid-cols-2 md:grid-cols-4 gap-2 text-sm">
        <div className="p-2 bg-slate-700/50 rounded">
          <div className="text-slate-400">Total</div>
          <div className="text-lg font-bold text-slate-200">{summary.totalTests}</div>
        </div>
        <div className="p-2 bg-emerald-500/10 rounded">
          <div className="text-emerald-400">Passed</div>
          <div className="text-lg font-bold text-emerald-400">{summary.passed}</div>
        </div>
        <div className="p-2 bg-red-500/10 rounded">
          <div className="text-red-400">Failed</div>
          <div className="text-lg font-bold text-red-400">{summary.failed}</div>
        </div>
        <div className="p-2 bg-slate-600/50 rounded">
          <div className="text-slate-400">Skipped</div>
          <div className="text-lg font-bold text-slate-300">{summary.skipped}</div>
        </div>
      </div>
      {summary.newTestsFailed > 0 && (
        <p className="mt-2 text-xs text-orange-400">
          {summary.newTestsFailed} new test(s) failed
        </p>
      )}
    </div>
  );
}

// Fix Task List
function FixTaskList({ tasks }: { tasks: FixTaskDto[] }) {
  return (
    <div className="mb-4">
      <h4 className="text-sm font-semibold text-slate-300 mb-2 flex items-center gap-2">
        <Wrench className="w-4 h-4" />
        Fix Tasks ({tasks.length})
      </h4>
      <div className="space-y-2 max-h-60 overflow-y-auto">
        {tasks.map((task, idx) => (
          <div key={idx} className="p-3 bg-slate-800/50 rounded-lg border border-slate-700">
            <div className="flex items-start justify-between">
              <div className="flex-1">
                <div className="font-medium text-slate-200">{task.title}</div>
                <div className="text-xs text-slate-500 mt-1">
                  <span className="inline-flex items-center gap-1 px-2 py-0.5 bg-slate-700 rounded">
                    {getFixTaskTypeLabel(task.type)}
                  </span>
                  {task.targetFile && (
                    <span className="ml-2 text-slate-400">{task.targetFile}</span>
                  )}
                </div>
              </div>
            </div>
            <div className="mt-2 text-sm text-red-400 font-mono bg-slate-900/50 p-2 rounded overflow-x-auto">
              {task.errorMessage}
            </div>
            {task.suggestedFix && (
              <div className="mt-2 text-xs text-emerald-400">
                💡 {task.suggestedFix}
              </div>
            )}
          </div>
        ))}
      </div>
    </div>
  );
}
