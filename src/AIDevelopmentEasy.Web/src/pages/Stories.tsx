import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Plus, RefreshCw, Activity, CheckCircle, Clock, AlertTriangle, ChevronRight, Database, FileCode } from 'lucide-react';
import { StoryDto, StoryStatus, CodebaseDto } from '../types';
import { storiesApi, pipelineApi, codebasesApi } from '../services/api';
import { StoryCard } from '../components/StoryCard';
import { useSignalR } from '../hooks/useSignalR';

export function Stories() {
  const navigate = useNavigate();
  const [stories, setStories] = useState<StoryDto[]>([]);
  const [codebases, setCodebases] = useState<CodebaseDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const { storyListChanged } = useSignalR();

  // New story form state
  const [showForm, setShowForm] = useState(false);
  const [formName, setFormName] = useState('');
  const [formContent, setFormContent] = useState('');
  const [formCodebaseId, setFormCodebaseId] = useState<string>('');
  const [creating, setCreating] = useState(false);

  const loadData = async () => {
    try {
      setLoading(true);
      setError(null);
      const [storiesData, codebasesData] = await Promise.all([
        storiesApi.getAll(),
        codebasesApi.getAll()
      ]);
      setStories(storiesData);
      setCodebases(codebasesData);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load data');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadData();
  }, [storyListChanged]);

  const handleCreate = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!formContent.trim()) return;

    try {
      setCreating(true);
      const story = await storiesApi.create({
        name: formName.trim(),
        content: formContent,
        type: 0, // Single
        codebaseId: formCodebaseId || undefined
      });

      // Reset form
      setFormName('');
      setFormContent('');
      setFormCodebaseId('');
      setShowForm(false);

      // Navigate to the new story
      navigate(`/stories/${story.id}`);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to create story');
    } finally {
      setCreating(false);
    }
  };

  const handleStart = async (id: string) => {
    try {
      await pipelineApi.start(id);
      navigate(`/pipeline/${id}`);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to start pipeline');
    }
  };

  const handleReset = async (id: string) => {
    if (!confirm('Are you sure you want to reset this story? All generated tasks and output will be cleared.')) {
      return;
    }
    try {
      await storiesApi.reset(id);
      await loadData();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to reset story');
    }
  };

  const handleDelete = async (id: string) => {
    if (!confirm('Are you sure you want to delete this story? This action cannot be undone.')) {
      return;
    }
    try {
      await storiesApi.delete(id);
      await loadData();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to delete story');
    }
  };

  // Stats
  const stats = {
    total: stories.length,
    completed: stories.filter(r => r.status === StoryStatus.Completed).length,
    inProgress: stories.filter(r => r.status === StoryStatus.InProgress).length,
    pending: stories.filter(r => r.status === StoryStatus.NotStarted || r.status === StoryStatus.Planned).length
  };

  return (
    <div className="p-8">
      {/* Header */}
      <div className="flex items-center justify-between mb-8">
        <div>
          <h1 className="text-3xl font-bold text-white mb-2 flex items-center gap-3">
            <FileCode className="w-8 h-8 text-slate-400" />
            Stories
          </h1>
          <p className="text-slate-400">Manage your development stories and run the AI pipeline</p>
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
            New Story
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
          <button onClick={() => setError(null)} className="ml-4 text-red-300 hover:text-white">
            Dismiss
          </button>
        </div>
      )}

      {/* Create Form */}
      {showForm && (
        <div className="mb-8 bg-slate-800/50 backdrop-blur border border-slate-700 rounded-xl p-6">
          <h2 className="text-lg font-semibold text-white mb-4">Create New Story</h2>
          <form onSubmit={handleCreate} className="space-y-4">
            {/* Name (optional – LLM-generated from content if left empty) */}
            <div>
              <label className="block text-sm font-medium text-slate-300 mb-1">
                Story Name (optional)
              </label>
              <input
                type="text"
                value={formName}
                onChange={(e) => setFormName(e.target.value)}
                placeholder="Leave empty to generate a title from your content using AI"
                className="w-full px-4 py-2 bg-slate-900 border border-slate-600 rounded-lg text-white placeholder-slate-500 focus:ring-2 focus:ring-blue-500 focus:border-transparent"
              />
              <p className="text-xs text-slate-500 mt-1">
                A short, clear name is generated from the story content if you leave this blank.
              </p>
            </div>

            {/* Codebase Selection */}
            <div>
              <label className="block text-sm font-medium text-slate-300 mb-1">
                <div className="flex items-center gap-2">
                  <Database className="w-4 h-4" />
                  Codebase (optional)
                </div>
              </label>
              <select
                value={formCodebaseId}
                onChange={(e) => setFormCodebaseId(e.target.value)}
                className="w-full px-4 py-2 bg-slate-900 border border-slate-600 rounded-lg text-white focus:ring-2 focus:ring-blue-500 focus:border-transparent"
              >
                <option value="">No codebase - create from scratch</option>
                {codebases.filter(cb => cb.status === 2).map((cb) => (
                  <option key={cb.id} value={cb.id}>
                    {cb.name} ({cb.path})
                  </option>
                ))}
              </select>
              <p className="text-xs text-slate-500 mt-1">
                Select a codebase for context-aware code generation
              </p>
            </div>

            {/* Content */}
            <div>
              <label className="block text-sm font-medium text-slate-300 mb-1">
                Story Content <span className="text-red-400">*</span>
              </label>
              <textarea
                value={formContent}
                onChange={(e) => setFormContent(e.target.value)}
                placeholder="Describe what you want to build. Be specific about requirements, expected behavior, and any constraints..."
                rows={8}
                required
                className="w-full px-4 py-3 bg-slate-900 border border-slate-600 rounded-lg text-white placeholder-slate-500 focus:ring-2 focus:ring-blue-500 focus:border-transparent resize-none font-mono text-sm"
              />
              <p className="text-xs text-slate-500 mt-1">
                Write a clear description of the feature or task. The AI will analyze and create implementation tasks.
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
                    Create Story
                    <ChevronRight className="w-4 h-4" />
                  </>
                )}
              </button>
            </div>
          </form>
        </div>
      )}

      {/* Stories List */}
      {loading ? (
        <div className="flex items-center justify-center py-12">
          <RefreshCw className="w-8 h-8 text-blue-400 animate-spin" />
        </div>
      ) : stories.length === 0 ? (
        <div className="text-center py-12">
          <div className="text-5xl mb-4">📝</div>
          <h3 className="text-xl font-semibold text-white mb-2">No Stories Yet</h3>
          <p className="text-slate-400 mb-6">
            Create your first story to start the AI development pipeline
          </p>
          <button
            onClick={() => setShowForm(true)}
            className="inline-flex items-center gap-2 px-6 py-3 bg-blue-600 hover:bg-blue-700 text-white font-medium rounded-lg transition-colors"
          >
            <Plus className="w-5 h-5" />
            Create Story
          </button>
        </div>
      ) : (
        <div className="grid grid-cols-2 gap-4">
          {stories.map((story, index) => {
            const codebase = story.codebaseId ? codebases.find(c => c.id === story.codebaseId) : undefined;
            return (
              <div key={story.id} style={{ animationDelay: `${index * 100}ms` }}>
                <StoryCard
                  story={story}
                  codebaseName={codebase?.name}
                  codebasePath={codebase?.path}
                  onStart={handleStart}
                  onReset={handleReset}
                  onDelete={handleDelete}
                />
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
}

export default Stories;
