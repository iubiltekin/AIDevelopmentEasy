import { useEffect, useState, useCallback } from 'react';
import { useParams, useNavigate, Link } from 'react-router-dom';
import { ArrowLeft, RefreshCw, XCircle, Activity } from 'lucide-react';
import {
  PipelineStatusDto,
  PipelinePhase,
  PipelineUpdateMessage,
  RetryAction
} from '../types';
import { pipelineApi, storiesApi } from '../services/api';
import { PipelineStatus } from '../components/PipelineStatus';
import { LogViewer } from '../components/LogViewer';
import { useSignalR } from '../hooks/useSignalR';

export function PipelineView() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [status, setStatus] = useState<PipelineStatusDto | null>(null);
  const [story, setStory] = useState<{ name: string } | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [logs, setLogs] = useState<PipelineUpdateMessage[]>([]);

  const { lastUpdate, subscribeToStory, unsubscribeFromStory } = useSignalR();

  const loadStatus = useCallback(async () => {
    if (!id) return;

    try {
      const [pipelineStatus, req] = await Promise.all([
        pipelineApi.getStatus(id),
        storiesApi.getById(id)
      ]);
      setStatus(pipelineStatus);
      setStory({ name: req.name });
      setError(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load pipeline status');
    } finally {
      setLoading(false);
    }
  }, [id]);

  useEffect(() => {
    loadStatus();

    if (id) {
      subscribeToStory(id);
      return () => {
        unsubscribeFromStory(id);
      };
    }
  }, [id, loadStatus, subscribeToStory, unsubscribeFromStory]);

  useEffect(() => {
    if (lastUpdate && lastUpdate.storyId === id) {
      setLogs(prev => [...prev, lastUpdate]);
      loadStatus(); // Refresh status on update
    }
  }, [lastUpdate, id, loadStatus]);

  const handleApprove = async (phase: PipelinePhase) => {
    if (!id) return;
    try {
      await pipelineApi.approvePhase(id, phase);
      await loadStatus();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to approve phase');
    }
  };

  const handleReject = async (phase: PipelinePhase) => {
    if (!id) return;
    const reason = prompt('Reason for rejection (optional):');
    try {
      await pipelineApi.rejectPhase(id, phase, reason || undefined);
      await loadStatus();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to reject phase');
    }
  };

  const handleCancel = async () => {
    if (!id) return;
    if (!confirm('Are you sure you want to cancel this pipeline?')) return;

    try {
      await pipelineApi.cancel(id);
      navigate('/');
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to cancel pipeline');
    }
  };

  const handleApproveRetry = async (action: RetryAction) => {
    if (!id) return;

    const actionLabels: Record<RetryAction, string> = {
      [RetryAction.AutoFix]: 'Auto-fix and retry',
      [RetryAction.ManualFix]: 'Request manual fix',
      [RetryAction.SkipTests]: 'Skip failed tests',
      [RetryAction.Abort]: 'Abort pipeline'
    };

    if (action === RetryAction.Abort) {
      if (!confirm('Are you sure you want to abort the pipeline?')) return;
    }

    try {
      await pipelineApi.approveRetry(id, action);
      await loadStatus();

      // Add log entry
      setLogs(prev => [...prev, {
        storyId: id,
        updateType: 'RetryApproved',
        phase: status?.currentPhase || PipelinePhase.None,
        message: `Retry action selected: ${actionLabels[action]}`,
        timestamp: new Date().toISOString()
      }]);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to approve retry');
    }
  };

  if (loading) {
    return (
      <div className="flex items-center justify-center h-full">
        <RefreshCw className="w-8 h-8 text-blue-400 animate-spin" />
      </div>
    );
  }

  return (
    <div className="p-8">
      {/* Header */}
      <div className="flex items-center justify-between mb-8">
        <div className="flex items-center gap-4">
          <Link
            to="/"
            className="p-2 text-slate-400 hover:text-white hover:bg-slate-700 rounded-lg transition-colors"
          >
            <ArrowLeft className="w-5 h-5" />
          </Link>
          <div>
            <h1 className="text-2xl font-bold text-white flex items-center gap-3">
              <Activity className="w-7 h-7 text-slate-400 flex-shrink-0" />
              {story?.name || id}
            </h1>
            <p className="text-slate-400">Pipeline Progress</p>
          </div>
        </div>
        <div className="flex gap-3">
          <button
            onClick={loadStatus}
            className="flex items-center gap-2 px-4 py-2 bg-slate-700 hover:bg-slate-600 text-white rounded-lg transition-colors"
          >
            <RefreshCw className="w-4 h-4" />
            Refresh
          </button>
          {status?.isRunning && (
            <button
              onClick={handleCancel}
              className="flex items-center gap-2 px-4 py-2 bg-red-600 hover:bg-red-700 text-white rounded-lg transition-colors"
            >
              <XCircle className="w-4 h-4" />
              Cancel
            </button>
          )}
        </div>
      </div>

      {/* Error */}
      {error && (
        <div className="mb-6 p-4 bg-red-500/10 border border-red-500/30 rounded-xl text-red-400">
          {error}
        </div>
      )}

      {/* Pipeline Status */}
      {status && (
        <div className="bg-slate-800/50 backdrop-blur border border-slate-700 rounded-xl p-6 mb-6">
          <h2 className="text-lg font-semibold text-white mb-6">Pipeline Status</h2>
          <PipelineStatus
            status={status}
            onApprove={handleApprove}
            onReject={handleReject}
            onApproveRetry={handleApproveRetry}
          />
        </div>
      )}

      {/* Logs */}
      <div className="bg-slate-800/50 backdrop-blur border border-slate-700 rounded-xl p-6">
        <h2 className="text-lg font-semibold text-white mb-4">Activity Log</h2>
        <LogViewer logs={logs} className="h-80" />
      </div>

      {/* Completed Actions */}
      {status?.currentPhase === PipelinePhase.Completed && (
        <div className="mt-6 flex gap-4">
          <Link
            to={`/storys/${id}`}
            className="flex-1 text-center py-4 bg-emerald-600 hover:bg-emerald-700 text-white font-medium rounded-xl transition-colors"
          >
            View Generated Output
          </Link>
          <Link
            to="/"
            className="flex-1 text-center py-4 bg-slate-600 hover:bg-slate-700 text-white font-medium rounded-xl transition-colors"
          >
            Back to Dashboard
          </Link>
        </div>
      )}
    </div>
  );
}
