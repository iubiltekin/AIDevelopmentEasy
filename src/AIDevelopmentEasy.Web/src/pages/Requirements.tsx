import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { Plus, RefreshCw, Activity, CheckCircle, Clock, FileText, Trash2, ChevronRight, Sparkles, GitBranch } from 'lucide-react';
import { requirementsApi, codebasesApi } from '../services/api';
import type { 
  RequirementDto, 
  CodebaseDto,
  RequirementType 
} from '../types';
import { 
  getRequirementTypeLabel, 
  getRequirementTypeColor,
  getRequirementStatusLabel,
  getRequirementStatusColor,
  getWizardPhaseLabel,
  RequirementStatus
} from '../types';

export default function Requirements() {
  const navigate = useNavigate();
  const [requirements, setRequirements] = useState<RequirementDto[]>([]);
  const [codebases, setCodebases] = useState<CodebaseDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  
  // New requirement form state
  const [showForm, setShowForm] = useState(false);
  const [formTitle, setFormTitle] = useState('');
  const [formContent, setFormContent] = useState('');
  const [formType, setFormType] = useState<RequirementType>(0);
  const [formCodebaseId, setFormCodebaseId] = useState<string>('');
  const [creating, setCreating] = useState(false);

  useEffect(() => {
    loadData();
  }, []);

  const loadData = async () => {
    try {
      setLoading(true);
      const [reqs, cbs] = await Promise.all([
        requirementsApi.getAll(),
        codebasesApi.getAll()
      ]);
      setRequirements(reqs);
      setCodebases(cbs);
      setError(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load requirements');
    } finally {
      setLoading(false);
    }
  };

  const handleCreate = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!formContent.trim()) return;

    try {
      setCreating(true);
      const req = await requirementsApi.create({
        title: formTitle || undefined,
        rawContent: formContent,
        type: formType,
        codebaseId: formCodebaseId || undefined
      });
      
      // Navigate to the new requirement
      navigate(`/requirements/${req.id}`);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to create requirement');
    } finally {
      setCreating(false);
    }
  };

  const handleDelete = async (id: string, e: React.MouseEvent) => {
    e.stopPropagation();
    if (!confirm('Are you sure you want to delete this requirement?')) return;
    
    try {
      await requirementsApi.delete(id);
      setRequirements(requirements.filter(r => r.id !== id));
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to delete requirement');
    }
  };

  // Stats
  const stats = {
    total: requirements.length,
    completed: requirements.filter(r => r.status === RequirementStatus.Completed).length,
    inProgress: requirements.filter(r => r.status === RequirementStatus.InProgress).length,
    draft: requirements.filter(r => r.status === RequirementStatus.Draft).length
  };

  return (
    <div className="p-8">
      {/* Header */}
      <div className="flex items-center justify-between mb-8">
        <div>
          <h1 className="text-3xl font-bold text-white mb-2">Requirements</h1>
          <p className="text-slate-400">Create and manage requirements through the wizard</p>
        </div>
        <div className="flex gap-3">
          <button
            onClick={loadData}
            className="flex items-center gap-2 px-4 py-2 bg-slate-700 hover:bg-slate-600 text-white rounded-lg transition-colors"
          >
            <RefreshCw className={`w-4 h-4 ${loading ? 'animate-spin' : ''}`} />
            Refresh
          </button>
          <button
            onClick={() => setShowForm(!showForm)}
            className="flex items-center gap-2 px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white rounded-lg transition-colors"
          >
            <Plus className="w-4 h-4" />
            New Requirement
          </button>
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
              <div className="text-sm text-slate-400">Total</div>
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
              <FileText className="w-6 h-6 text-slate-400" />
            </div>
            <div>
              <div className="text-2xl font-bold text-white">{stats.draft}</div>
              <div className="text-sm text-slate-400">Draft</div>
            </div>
          </div>
        </div>
      </div>

      {/* Error */}
      {error && (
        <div className="mb-6 p-4 bg-red-500/10 border border-red-500/30 rounded-xl text-red-400">
          {error}
          <button onClick={() => setError(null)} className="ml-4 text-red-300 hover:text-white">
            Dismiss
          </button>
        </div>
      )}

      {/* Create Form */}
      {showForm && (
        <div className="mb-8 bg-slate-800/50 backdrop-blur border border-slate-700 rounded-xl p-6">
          <h2 className="text-lg font-semibold text-white mb-4">Create New Requirement</h2>
          <form onSubmit={handleCreate} className="space-y-4">
            {/* Title (optional) */}
            <div>
              <label className="block text-sm font-medium text-slate-300 mb-1">
                Title (optional)
              </label>
              <input
                type="text"
                value={formTitle}
                onChange={(e) => setFormTitle(e.target.value)}
                placeholder="Auto-generated from content if empty"
                className="w-full px-4 py-2 bg-slate-900 border border-slate-600 rounded-lg text-white placeholder-slate-500 focus:ring-2 focus:ring-blue-500 focus:border-transparent"
              />
            </div>

            {/* Type Selection */}
            <div>
              <label className="block text-sm font-medium text-slate-300 mb-2">
                Requirement Type
              </label>
              <div className="grid grid-cols-2 md:grid-cols-4 gap-2">
                {[0, 1, 2, 3].map((type) => (
                  <button
                    key={type}
                    type="button"
                    onClick={() => setFormType(type as RequirementType)}
                    className={`px-4 py-2 rounded-lg border transition-colors ${
                      formType === type
                        ? 'bg-blue-600 border-blue-500 text-white'
                        : 'bg-slate-900 border-slate-600 text-slate-300 hover:border-slate-500'
                    }`}
                  >
                    <span className={`inline-block w-2 h-2 rounded-full mr-2 ${getRequirementTypeColor(type as RequirementType)}`}></span>
                    {getRequirementTypeLabel(type as RequirementType)}
                  </button>
                ))}
              </div>
            </div>

            {/* Codebase Selection */}
            <div>
              <label className="block text-sm font-medium text-slate-300 mb-1">
                Codebase (optional)
              </label>
              <select
                value={formCodebaseId}
                onChange={(e) => setFormCodebaseId(e.target.value)}
                className="w-full px-4 py-2 bg-slate-900 border border-slate-600 rounded-lg text-white focus:ring-2 focus:ring-blue-500 focus:border-transparent"
              >
                <option value="">No codebase</option>
                {codebases.map((cb) => (
                  <option key={cb.id} value={cb.id}>
                    {cb.name} ({cb.path})
                  </option>
                ))}
              </select>
            </div>

            {/* Raw Content */}
            <div>
              <label className="block text-sm font-medium text-slate-300 mb-1">
                Raw Requirement <span className="text-red-400">*</span>
              </label>
              <textarea
                value={formContent}
                onChange={(e) => setFormContent(e.target.value)}
                placeholder="Describe your requirement in plain text. The wizard will help you refine it..."
                rows={6}
                required
                className="w-full px-4 py-3 bg-slate-900 border border-slate-600 rounded-lg text-white placeholder-slate-500 focus:ring-2 focus:ring-blue-500 focus:border-transparent resize-none"
              />
              <p className="text-xs text-slate-500 mt-1">
                Write your requirement in natural language. The AI will analyze it and ask clarifying questions.
              </p>
            </div>

            {/* Actions */}
            <div className="flex justify-end gap-3 pt-2">
              <button
                type="button"
                onClick={() => setShowForm(false)}
                className="px-4 py-2 text-slate-300 hover:text-white transition-colors"
              >
                Cancel
              </button>
              <button
                type="submit"
                disabled={creating || !formContent.trim()}
                className="px-6 py-2 bg-blue-600 hover:bg-blue-700 disabled:bg-slate-600 text-white rounded-lg transition-colors flex items-center gap-2"
              >
                {creating ? (
                  <>
                    <RefreshCw className="w-4 h-4 animate-spin" />
                    Creating...
                  </>
                ) : (
                  <>
                    Create & Start Wizard
                    <ChevronRight className="w-4 h-4" />
                  </>
                )}
              </button>
            </div>
          </form>
        </div>
      )}

      {/* Requirements List */}
      {loading ? (
        <div className="flex items-center justify-center py-12">
          <RefreshCw className="w-8 h-8 text-blue-400 animate-spin" />
        </div>
      ) : requirements.length === 0 ? (
        <div className="text-center py-12">
          <div className="text-5xl mb-4">📋</div>
          <h3 className="text-xl font-semibold text-white mb-2">No Requirements Yet</h3>
          <p className="text-slate-400 mb-6">
            Create your first requirement to start the wizard process
          </p>
          <button
            onClick={() => setShowForm(true)}
            className="inline-flex items-center gap-2 px-6 py-3 bg-blue-600 hover:bg-blue-700 text-white font-medium rounded-lg transition-colors"
          >
            <Plus className="w-5 h-5" />
            Create Requirement
          </button>
        </div>
      ) : (
        <div className="grid grid-cols-2 gap-4">
          {requirements.map((req, index) => (
            <RequirementCard
              key={req.id}
              requirement={req}
              onDelete={handleDelete}
              onClick={() => navigate(`/requirements/${req.id}`)}
              style={{ animationDelay: `${index * 100}ms` }}
            />
          ))}
        </div>
      )}
    </div>
  );
}

// Requirement Card Component
function RequirementCard({ 
  requirement, 
  onDelete, 
  onClick,
  style
}: { 
  requirement: RequirementDto; 
  onDelete: (id: string, e: React.MouseEvent) => void;
  onClick: () => void;
  style?: React.CSSProperties;
}) {
  const getStatusIcon = () => {
    switch (requirement.status) {
      case RequirementStatus.Completed:
        return <CheckCircle className="w-5 h-5 text-emerald-400" />;
      case RequirementStatus.InProgress:
        return <Sparkles className="w-5 h-5 text-amber-400 animate-pulse" />;
      default:
        return <FileText className="w-5 h-5 text-slate-400" />;
    }
  };

  return (
    <div
      className="bg-slate-800/50 backdrop-blur border border-slate-700 rounded-xl p-5 hover:border-slate-600 transition-all cursor-pointer group animate-slide-in"
      onClick={onClick}
      style={style}
    >
      <div className="flex items-start justify-between gap-4">
        <div className="flex items-start gap-3 flex-1 min-w-0">
          <div className="mt-1">
            {getStatusIcon()}
          </div>
          <div className="flex-1 min-w-0">
            <div className="flex items-center gap-2 mb-2">
              <span className={`px-2 py-0.5 text-xs font-medium rounded ${getRequirementTypeColor(requirement.type)} text-white`}>
                {getRequirementTypeLabel(requirement.type)}
              </span>
              <span className={`px-2 py-0.5 text-xs font-medium rounded ${getRequirementStatusColor(requirement.status)} text-white`}>
                {getRequirementStatusLabel(requirement.status)}
              </span>
            </div>
            <h3 className="text-lg font-semibold text-white truncate group-hover:text-blue-400 transition-colors">
              {requirement.title}
            </h3>
            <p className="text-sm text-slate-400 line-clamp-2 mt-1">
              {requirement.rawContent}
            </p>
            
            <div className="flex items-center gap-4 mt-3 text-xs text-slate-500">
              <span className="font-mono">{requirement.id}</span>
              <span>{new Date(requirement.createdAt).toLocaleDateString()}</span>
              {requirement.status === RequirementStatus.InProgress && (
                <span className="text-amber-400 flex items-center gap-1">
                  <Sparkles className="w-3 h-3" />
                  {getWizardPhaseLabel(requirement.currentPhase)}
                </span>
              )}
              {requirement.storyCount > 0 && (
                <span className="text-emerald-400 flex items-center gap-1">
                  <GitBranch className="w-3 h-3" />
                  {requirement.storyCount} stories
                </span>
              )}
            </div>
          </div>
        </div>
        
        <div className="flex items-center gap-2">
          <button
            onClick={(e) => onDelete(requirement.id, e)}
            className="p-2 text-slate-400 hover:text-red-400 opacity-0 group-hover:opacity-100 transition-all"
            title="Delete"
          >
            <Trash2 className="w-5 h-5" />
          </button>
          <ChevronRight className="w-5 h-5 text-slate-500 group-hover:text-white transition-colors" />
        </div>
      </div>
    </div>
  );
}
