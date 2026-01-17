import { useEffect, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { Plus, RefreshCw, Activity, CheckCircle, Clock, AlertTriangle } from 'lucide-react';
import { RequirementDto, RequirementStatus } from '../types';
import { requirementsApi, pipelineApi } from '../services/api';
import { RequirementCard } from '../components/RequirementCard';
import { useSignalR } from '../hooks/useSignalR';

export function Dashboard() {
  const navigate = useNavigate();
  const [requirements, setRequirements] = useState<RequirementDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const { requirementListChanged } = useSignalR();

  const loadRequirements = async () => {
    try {
      setLoading(true);
      setError(null);
      const data = await requirementsApi.getAll();
      setRequirements(data);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load requirements');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadRequirements();
  }, [requirementListChanged]);

  const handleStart = async (id: string) => {
    try {
      await pipelineApi.start(id);
      navigate(`/pipeline/${id}`);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to start pipeline');
    }
  };

  const handleReset = async (id: string) => {
    if (!confirm('Are you sure you want to reset this requirement? All generated tasks and output will be cleared.')) {
      return;
    }
    try {
      await requirementsApi.reset(id);
      await loadRequirements();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to reset requirement');
    }
  };

  const handleDelete = async (id: string) => {
    if (!confirm('Are you sure you want to delete this requirement? This action cannot be undone.')) {
      return;
    }
    try {
      await requirementsApi.delete(id);
      await loadRequirements();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to delete requirement');
    }
  };

  // Stats
  const stats = {
    total: requirements.length,
    completed: requirements.filter(r => r.status === RequirementStatus.Completed).length,
    inProgress: requirements.filter(r => r.status === RequirementStatus.InProgress).length,
    pending: requirements.filter(r => r.status === RequirementStatus.NotStarted || r.status === RequirementStatus.Planned).length
  };

  return (
    <div className="p-8">
      {/* Header */}
      <div className="flex items-center justify-between mb-8">
        <div>
          <h1 className="text-3xl font-bold text-white mb-2">Dashboard</h1>
          <p className="text-slate-400">Manage your AI-powered development pipeline</p>
        </div>
        <div className="flex gap-3">
          <button
            onClick={loadRequirements}
            className="flex items-center gap-2 px-4 py-2 bg-slate-700 hover:bg-slate-600 text-white rounded-lg transition-colors"
          >
            <RefreshCw className={`w-4 h-4 ${loading ? 'animate-spin' : ''}`} />
            Refresh
          </button>
          <Link
            to="/requirements/new"
            className="flex items-center gap-2 px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white rounded-lg transition-colors"
          >
            <Plus className="w-4 h-4" />
            New Requirement
          </Link>
        </div>
      </div>

      {/* Stats */}
      <div className="grid grid-cols-4 gap-4 mb-8">
        <div className="bg-slate-800/50 backdrop-blur border border-slate-700 rounded-xl p-5">
          <div className="flex items-center gap-3">
            <div className="p-3 bg-blue-500/20 rounded-lg">
              <Activity className="w-6 h-6 text-blue-400" />
            </div>
            <div>
              <div className="text-2xl font-bold text-white">{stats.total}</div>
              <div className="text-sm text-slate-400">Total Requirements</div>
            </div>
          </div>
        </div>
        
        <div className="bg-slate-800/50 backdrop-blur border border-slate-700 rounded-xl p-5">
          <div className="flex items-center gap-3">
            <div className="p-3 bg-emerald-500/20 rounded-lg">
              <CheckCircle className="w-6 h-6 text-emerald-400" />
            </div>
            <div>
              <div className="text-2xl font-bold text-white">{stats.completed}</div>
              <div className="text-sm text-slate-400">Completed</div>
            </div>
          </div>
        </div>

        <div className="bg-slate-800/50 backdrop-blur border border-slate-700 rounded-xl p-5">
          <div className="flex items-center gap-3">
            <div className="p-3 bg-amber-500/20 rounded-lg">
              <Clock className="w-6 h-6 text-amber-400" />
            </div>
            <div>
              <div className="text-2xl font-bold text-white">{stats.inProgress}</div>
              <div className="text-sm text-slate-400">In Progress</div>
            </div>
          </div>
        </div>

        <div className="bg-slate-800/50 backdrop-blur border border-slate-700 rounded-xl p-5">
          <div className="flex items-center gap-3">
            <div className="p-3 bg-slate-500/20 rounded-lg">
              <AlertTriangle className="w-6 h-6 text-slate-400" />
            </div>
            <div>
              <div className="text-2xl font-bold text-white">{stats.pending}</div>
              <div className="text-sm text-slate-400">Pending</div>
            </div>
          </div>
        </div>
      </div>

      {/* Error */}
      {error && (
        <div className="mb-6 p-4 bg-red-500/10 border border-red-500/30 rounded-xl text-red-400">
          {error}
        </div>
      )}

      {/* Requirements List */}
      {loading ? (
        <div className="flex items-center justify-center py-12">
          <RefreshCw className="w-8 h-8 text-blue-400 animate-spin" />
        </div>
      ) : requirements.length === 0 ? (
        <div className="text-center py-12">
          <div className="text-5xl mb-4">📭</div>
          <h3 className="text-xl font-semibold text-white mb-2">No Requirements Yet</h3>
          <p className="text-slate-400 mb-6">
            Add your first requirement to start the AI development pipeline
          </p>
          <Link
            to="/requirements/new"
            className="inline-flex items-center gap-2 px-6 py-3 bg-blue-600 hover:bg-blue-700 text-white font-medium rounded-lg transition-colors"
          >
            <Plus className="w-5 h-5" />
            Create Requirement
          </Link>
        </div>
      ) : (
        <div className="grid grid-cols-2 gap-4">
          {requirements.map((req, index) => (
            <div key={req.id} style={{ animationDelay: `${index * 100}ms` }}>
              <RequirementCard
                requirement={req}
                onStart={handleStart}
                onReset={handleReset}
                onDelete={handleDelete}
              />
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
