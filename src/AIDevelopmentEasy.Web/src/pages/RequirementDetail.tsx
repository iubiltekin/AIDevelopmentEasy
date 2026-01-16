import { useEffect, useState } from 'react';
import { useParams, Link, useNavigate } from 'react-router-dom';
import { ArrowLeft, Play, RefreshCw, FileCode, Eye, Trash2, RotateCcw } from 'lucide-react';
import { RequirementDto, RequirementStatus, RequirementType, TaskStatus } from '../types';
import { requirementsApi, pipelineApi } from '../services/api';
import { StatusBadge } from '../components/StatusBadge';

export function RequirementDetail() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [requirement, setRequirement] = useState<RequirementDto | null>(null);
  const [content, setContent] = useState<string>('');
  const [output, setOutput] = useState<Record<string, string>>({});
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [activeTab, setActiveTab] = useState<'overview' | 'content' | 'tasks' | 'output'>('overview');

  useEffect(() => {
    const load = async () => {
      if (!id) return;
      
      try {
        setLoading(true);
        const [req, reqContent] = await Promise.all([
          requirementsApi.getById(id),
          requirementsApi.getContent(id).catch(() => '')
        ]);
        
        setRequirement(req);
        setContent(reqContent);

        if (req.status === RequirementStatus.Completed) {
          const out = await pipelineApi.getOutput(id).catch(() => ({}));
          setOutput(out);
        }
        
        setError(null);
      } catch (err) {
        setError(err instanceof Error ? err.message : 'Failed to load requirement');
      } finally {
        setLoading(false);
      }
    };

    load();
  }, [id]);

  const handleStart = async () => {
    if (!id) return;
    try {
      await pipelineApi.start(id);
      navigate(`/pipeline/${id}`);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to start pipeline');
    }
  };

  const handleReset = async () => {
    if (!id) return;
    if (!confirm('Reset this requirement? All tasks and output will be cleared.')) return;
    
    try {
      await requirementsApi.reset(id);
      window.location.reload();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to reset requirement');
    }
  };

  const handleDelete = async () => {
    if (!id) return;
    if (!confirm('Delete this requirement? This cannot be undone.')) return;
    
    try {
      await requirementsApi.delete(id);
      navigate('/');
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to delete requirement');
    }
  };

  const getTaskStatusIcon = (status: TaskStatus) => {
    switch (status) {
      case TaskStatus.Completed:
        return '✅';
      case TaskStatus.InProgress:
        return '🔄';
      case TaskStatus.Failed:
        return '❌';
      default:
        return '⬜';
    }
  };

  if (loading) {
    return (
      <div className="flex items-center justify-center h-full">
        <RefreshCw className="w-8 h-8 text-blue-400 animate-spin" />
      </div>
    );
  }

  if (!requirement) {
    return (
      <div className="flex flex-col items-center justify-center h-full">
        <div className="text-5xl mb-4">🔍</div>
        <h2 className="text-xl font-semibold text-white mb-2">Requirement Not Found</h2>
        <Link to="/" className="text-blue-400 hover:underline">Back to Dashboard</Link>
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
            <div className="flex items-center gap-3">
              <h1 className="text-2xl font-bold text-white">{requirement.name}</h1>
              <StatusBadge status={requirement.status} />
            </div>
            <p className="text-slate-400">
              {requirement.type === RequirementType.Multi ? 'Multi-Project' : 'Single Project'} Requirement
            </p>
          </div>
        </div>
        <div className="flex gap-3">
          {requirement.status !== RequirementStatus.InProgress && 
           requirement.status !== RequirementStatus.Completed && (
            <button
              onClick={handleStart}
              className="flex items-center gap-2 px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white rounded-lg transition-colors"
            >
              <Play className="w-4 h-4" />
              Start Pipeline
            </button>
          )}
          {requirement.status === RequirementStatus.InProgress && (
            <Link
              to={`/pipeline/${id}`}
              className="flex items-center gap-2 px-4 py-2 bg-amber-600 hover:bg-amber-700 text-white rounded-lg transition-colors"
            >
              <Eye className="w-4 h-4" />
              View Progress
            </Link>
          )}
          <button
            onClick={handleReset}
            className="p-2 text-slate-400 hover:text-white hover:bg-slate-700 rounded-lg transition-colors"
            title="Reset"
          >
            <RotateCcw className="w-5 h-5" />
          </button>
          <button
            onClick={handleDelete}
            className="p-2 text-slate-400 hover:text-red-400 hover:bg-slate-700 rounded-lg transition-colors"
            title="Delete"
          >
            <Trash2 className="w-5 h-5" />
          </button>
        </div>
      </div>

      {/* Error */}
      {error && (
        <div className="mb-6 p-4 bg-red-500/10 border border-red-500/30 rounded-xl text-red-400">
          {error}
        </div>
      )}

      {/* Tabs */}
      <div className="flex gap-2 mb-6">
        {['overview', 'content', 'tasks', 'output'].map(tab => (
          <button
            key={tab}
            onClick={() => setActiveTab(tab as typeof activeTab)}
            className={`px-4 py-2 rounded-lg font-medium transition-colors ${
              activeTab === tab
                ? 'bg-blue-600 text-white'
                : 'bg-slate-800 text-slate-400 hover:text-white'
            }`}
          >
            {tab.charAt(0).toUpperCase() + tab.slice(1)}
          </button>
        ))}
      </div>

      {/* Tab Content */}
      <div className="bg-slate-800/50 backdrop-blur border border-slate-700 rounded-xl p-6">
        {activeTab === 'overview' && (
          <div className="grid grid-cols-2 gap-6">
            <div>
              <h3 className="text-lg font-semibold text-white mb-4">Details</h3>
              <dl className="space-y-3">
                <div>
                  <dt className="text-sm text-slate-400">ID</dt>
                  <dd className="text-white font-mono">{requirement.id}</dd>
                </div>
                <div>
                  <dt className="text-sm text-slate-400">Type</dt>
                  <dd className="text-white">
                    {requirement.type === RequirementType.Multi ? 'Multi-Project' : 'Single Project'}
                  </dd>
                </div>
                <div>
                  <dt className="text-sm text-slate-400">Status</dt>
                  <dd><StatusBadge status={requirement.status} /></dd>
                </div>
                <div>
                  <dt className="text-sm text-slate-400">Created</dt>
                  <dd className="text-white">
                    {new Date(requirement.createdAt).toLocaleString()}
                  </dd>
                </div>
                {requirement.lastProcessedAt && (
                  <div>
                    <dt className="text-sm text-slate-400">Last Processed</dt>
                    <dd className="text-white">
                      {new Date(requirement.lastProcessedAt).toLocaleString()}
                    </dd>
                  </div>
                )}
              </dl>
            </div>
            <div>
              <h3 className="text-lg font-semibold text-white mb-4">Summary</h3>
              <div className="space-y-3">
                <div className="p-4 bg-slate-900 rounded-lg">
                  <div className="text-3xl font-bold text-blue-400">
                    {requirement.tasks.length}
                  </div>
                  <div className="text-sm text-slate-400">Tasks Generated</div>
                </div>
                <div className="p-4 bg-slate-900 rounded-lg">
                  <div className="text-3xl font-bold text-emerald-400">
                    {requirement.tasks.filter(t => t.status === TaskStatus.Completed).length}
                  </div>
                  <div className="text-sm text-slate-400">Tasks Completed</div>
                </div>
              </div>
            </div>
          </div>
        )}

        {activeTab === 'content' && (
          <div>
            <h3 className="text-lg font-semibold text-white mb-4">Requirement Content</h3>
            <pre className="bg-slate-900 p-4 rounded-lg overflow-x-auto text-slate-300 whitespace-pre-wrap">
              {content || 'No content available'}
            </pre>
          </div>
        )}

        {activeTab === 'tasks' && (
          <div>
            <h3 className="text-lg font-semibold text-white mb-4">
              Tasks ({requirement.tasks.length})
            </h3>
            {requirement.tasks.length === 0 ? (
              <div className="text-center py-8 text-slate-400">
                No tasks yet. Start the pipeline to generate tasks.
              </div>
            ) : (
              <div className="space-y-3">
                {requirement.tasks.map((task, index) => (
                  <div 
                    key={index}
                    className="p-4 bg-slate-900 rounded-lg border border-slate-700"
                  >
                    <div className="flex items-start justify-between">
                      <div className="flex items-center gap-3">
                        <span className="text-xl">{getTaskStatusIcon(task.status)}</span>
                        <div>
                          <div className="font-medium text-white">{task.title}</div>
                          <div className="text-sm text-slate-400">
                            {task.projectName && `Project: ${task.projectName}`}
                          </div>
                        </div>
                      </div>
                      <span className="text-xs text-slate-500">#{task.index + 1}</span>
                    </div>
                    {task.description && (
                      <p className="mt-2 text-sm text-slate-400">{task.description}</p>
                    )}
                    {task.targetFiles.length > 0 && (
                      <div className="mt-2 flex flex-wrap gap-1">
                        {task.targetFiles.map((file, i) => (
                          <span 
                            key={i}
                            className="inline-flex items-center gap-1 px-2 py-1 bg-slate-800 rounded text-xs text-slate-300"
                          >
                            <FileCode className="w-3 h-3" />
                            {file}
                          </span>
                        ))}
                      </div>
                    )}
                  </div>
                ))}
              </div>
            )}
          </div>
        )}

        {activeTab === 'output' && (
          <div>
            <h3 className="text-lg font-semibold text-white mb-4">Generated Output</h3>
            {Object.keys(output).length === 0 ? (
              <div className="text-center py-8 text-slate-400">
                No output yet. Complete the pipeline to see generated files.
              </div>
            ) : (
              <div className="space-y-4">
                {Object.entries(output).map(([filename, content]) => (
                  <div key={filename} className="border border-slate-700 rounded-lg overflow-hidden">
                    <div className="px-4 py-2 bg-slate-700 text-white font-mono text-sm">
                      {filename}
                    </div>
                    <pre className="p-4 bg-slate-900 overflow-x-auto text-sm text-slate-300">
                      {content}
                    </pre>
                  </div>
                ))}
              </div>
            )}
          </div>
        )}
      </div>
    </div>
  );
}
