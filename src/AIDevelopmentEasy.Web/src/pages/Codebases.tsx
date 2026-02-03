import { useEffect, useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import { Database, RefreshCw, Plus, Trash2, RotateCcw, FolderCode, Layers } from 'lucide-react';
import { codebasesApi } from '../services/api';
import { CodebaseDto, CodebaseStatus, getCodebaseStatusLabel, getCodebaseStatusColor } from '../types';

export function Codebases() {
  const [codebases, setCodebases] = useState<CodebaseDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);
  const [showAddForm, setShowAddForm] = useState(false);
  const [newName, setNewName] = useState('');
  const [newPath, setNewPath] = useState('');
  const [creating, setCreating] = useState(false);
  const pollingUntilRef = useRef(0);

  const fetchCodebases = async (): Promise<CodebaseDto[]> => {
    try {
      setLoading(true);
      const data = await codebasesApi.getAll();
      setCodebases(data);
      setError(null);
      return data;
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load codebases');
      return [];
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchCodebases();
  }, []);

  useEffect(() => {
    const interval = setInterval(async () => {
      if (pollingUntilRef.current <= Date.now()) return;
      try {
        const data = await codebasesApi.getAll();
        setCodebases(data);
        setError(null);
        const anyAnalyzing = data.some(c => c.status === CodebaseStatus.Analyzing);
        if (!anyAnalyzing) {
          pollingUntilRef.current = 0;
          setSuccessMessage('Analysis complete. List updated.');
          setTimeout(() => setSuccessMessage(null), 4000);
        }
      } catch { /* keep previous state on poll error */ }
    }, 3000);
    return () => clearInterval(interval);
  }, []);

  const handleCreate = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!newName.trim() || !newPath.trim()) return;

    try {
      setCreating(true);
      await codebasesApi.create({ name: newName, path: newPath });
      setNewName('');
      setNewPath('');
      setShowAddForm(false);
      fetchCodebases();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to create codebase');
    } finally {
      setCreating(false);
    }
  };

  const handleReanalyze = async (id: string) => {
    try {
      setError(null);
      await codebasesApi.analyze(id);
      pollingUntilRef.current = Date.now() + 120000;
      setSuccessMessage('Re-analyze started. List will update when complete.');
      await fetchCodebases();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to start analysis');
    }
  };

  const handleDelete = async (id: string) => {
    if (!confirm('Delete this codebase? This will not delete the actual files.')) return;

    try {
      await codebasesApi.delete(id);
      fetchCodebases();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to delete codebase');
    }
  };

  if (loading && codebases.length === 0) {
    return (
      <div className="flex items-center justify-center h-full">
        <RefreshCw className="w-8 h-8 text-blue-400 animate-spin" />
      </div>
    );
  }

  return (
    <div className="p-8">
      <div className="flex items-center justify-between mb-8">
        <div className="flex items-center gap-3">
          <Database className="w-8 h-8 text-blue-400" />
          <h1 className="text-3xl font-bold text-white">Codebases</h1>
        </div>
        <div className="flex gap-3">
          <button
            onClick={fetchCodebases}
            className="p-2 text-slate-400 hover:text-white hover:bg-slate-700 rounded-lg transition-colors"
            title="Refresh"
          >
            <RefreshCw className="w-5 h-5" />
          </button>
          <button
            onClick={() => setShowAddForm(!showAddForm)}
            className="flex items-center gap-2 px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white rounded-lg transition-colors"
          >
            <Plus className="w-5 h-5" />
            Add Codebase
          </button>
        </div>
      </div>

      {error && (
        <div className="mb-6 p-4 bg-red-500/10 border border-red-500/30 rounded-xl text-red-400">
          {error}
        </div>
      )}

      {successMessage && (
        <div className="mb-6 p-4 bg-emerald-500/10 border border-emerald-500/30 rounded-xl text-emerald-400">
          {successMessage}
        </div>
      )}

      {/* Add Form */}
      {showAddForm && (
        <div className="mb-8 bg-slate-800/50 backdrop-blur border border-slate-700 rounded-xl p-6">
          <h2 className="text-xl font-semibold text-white mb-4">Register New Codebase</h2>
          <form onSubmit={handleCreate} className="space-y-4">
            <div>
              <label htmlFor="name" className="block text-sm font-medium text-slate-300 mb-2">
                Name
              </label>
              <input
                type="text"
                id="name"
                value={newName}
                onChange={(e) => setNewName(e.target.value)}
                placeholder="e.g., windows-agent"
                className="w-full p-3 bg-slate-900 border border-slate-700 rounded-lg text-white focus:ring-blue-500 focus:border-blue-500"
                required
              />
            </div>
            <div>
              <label htmlFor="path" className="block text-sm font-medium text-slate-300 mb-2">
                Path
              </label>
              <input
                type="text"
                id="path"
                value={newPath}
                onChange={(e) => setNewPath(e.target.value)}
                placeholder="e.g., C:\Projects\my-project"
                className="w-full p-3 bg-slate-900 border border-slate-700 rounded-lg text-white focus:ring-blue-500 focus:border-blue-500 font-mono"
                required
              />
            </div>
            <div className="flex gap-3">
              <button
                type="submit"
                disabled={creating}
                className="px-5 py-2 bg-emerald-600 hover:bg-emerald-700 text-white rounded-lg font-medium transition-colors disabled:opacity-50"
              >
                {creating ? 'Creating...' : 'Create & Analyze'}
              </button>
              <button
                type="button"
                onClick={() => setShowAddForm(false)}
                className="px-5 py-2 bg-slate-700 hover:bg-slate-600 text-white rounded-lg font-medium transition-colors"
              >
                Cancel
              </button>
            </div>
          </form>
        </div>
      )}

      {/* Codebases List */}
      {codebases.length === 0 ? (
        <div className="text-center py-16 text-slate-400 bg-slate-800/50 backdrop-blur border border-slate-700 rounded-xl">
          <Database className="w-16 h-16 mx-auto mb-4 text-slate-500" />
          <p className="text-lg mb-2">No codebases registered yet.</p>
          <p>Click "Add Codebase" to analyze an existing project.</p>
        </div>
      ) : (
        <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
          {codebases.map((codebase) => (
            <div
              key={codebase.id}
              className="bg-slate-800/50 backdrop-blur border border-slate-700 rounded-xl p-6 hover:border-blue-600 transition-all"
            >
              <div className="flex items-start justify-between mb-4">
                <div className="flex items-center gap-3">
                  <FolderCode className="w-6 h-6 text-blue-400" />
                  <div>
                    <h3 className="text-lg font-semibold text-white">{codebase.name}</h3>
                    <p className="text-sm text-slate-400 font-mono truncate max-w-[300px]">
                      {codebase.path}
                    </p>
                  </div>
                </div>
                <span className={`px-3 py-1 rounded-full text-xs font-medium ${getCodebaseStatusColor(codebase.status)} text-white`}>
                  {getCodebaseStatusLabel(codebase.status)}
                </span>
              </div>

              {codebase.summary && (
                <>
                  <div className="grid grid-cols-4 gap-4 mb-4">
                    <div className="text-center p-3 bg-slate-900 rounded-lg">
                      <div className="text-2xl font-bold text-white">{codebase.summary.totalProjects}</div>
                      <div className="text-xs text-slate-400">Projects</div>
                    </div>
                    <div className="text-center p-3 bg-slate-900 rounded-lg">
                      <div className="text-2xl font-bold text-white">{codebase.summary.totalClasses}</div>
                      <div className="text-xs text-slate-400">Classes</div>
                    </div>
                    <div className="text-center p-3 bg-slate-900 rounded-lg">
                      <div className="text-2xl font-bold text-white">{codebase.summary.totalInterfaces}</div>
                      <div className="text-xs text-slate-400">Interfaces</div>
                    </div>
                    <div className="text-center p-3 bg-slate-900 rounded-lg">
                      <div className="text-sm font-medium text-blue-400 truncate">{codebase.summary.primaryFramework}</div>
                      <div className="text-xs text-slate-400">Framework</div>
                    </div>
                  </div>
                  {codebase.summary.languages && codebase.summary.languages.length > 0 && (
                    <div className="flex flex-wrap gap-2 mb-4">
                      {codebase.summary.languages.map((lang, i) => (
                        <span key={i} className="px-2 py-1 bg-emerald-900/40 text-emerald-300 rounded text-xs font-medium capitalize">
                          {lang}
                        </span>
                      ))}
                    </div>
                  )}
                </>
              )}

              {codebase.summary?.detectedPatterns && codebase.summary.detectedPatterns.length > 0 && (
                <div className="mb-4">
                  <div className="flex items-center gap-2 mb-2">
                    <Layers className="w-4 h-4 text-slate-400" />
                    <span className="text-sm text-slate-400">Detected Patterns</span>
                  </div>
                  <div className="flex flex-wrap gap-2">
                    {codebase.summary.detectedPatterns.slice(0, 6).map((pattern, i) => (
                      <span key={i} className="px-2 py-1 bg-slate-700 rounded text-xs text-slate-300">
                        {pattern}
                      </span>
                    ))}
                    {codebase.summary.detectedPatterns.length > 6 && (
                      <span className="px-2 py-1 text-xs text-slate-500">
                        +{codebase.summary.detectedPatterns.length - 6} more
                      </span>
                    )}
                  </div>
                </div>
              )}

              <div className="flex items-center justify-between pt-4 border-t border-slate-700">
                <div className="text-xs text-slate-500">
                  {codebase.analyzedAt
                    ? `Analyzed: ${new Date(codebase.analyzedAt).toLocaleString()}`
                    : `Created: ${new Date(codebase.createdAt).toLocaleString()}`}
                </div>
                <div className="flex gap-2">
                  <Link
                    to={`/codebases/${codebase.id}`}
                    className="px-3 py-1 bg-blue-600 hover:bg-blue-700 text-white rounded text-sm transition-colors"
                  >
                    Details
                  </Link>
                  <button
                    onClick={() => handleReanalyze(codebase.id)}
                    disabled={codebase.status === CodebaseStatus.Analyzing}
                    className="p-1 text-slate-400 hover:text-white hover:bg-slate-700 rounded transition-colors disabled:opacity-50"
                    title="Re-analyze"
                  >
                    <RotateCcw className="w-4 h-4" />
                  </button>
                  <button
                    onClick={() => handleDelete(codebase.id)}
                    className="p-1 text-slate-400 hover:text-red-400 hover:bg-slate-700 rounded transition-colors"
                    title="Delete"
                  >
                    <Trash2 className="w-4 h-4" />
                  </button>
                </div>
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
