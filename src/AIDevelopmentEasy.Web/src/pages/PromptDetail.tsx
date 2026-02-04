import { useState, useEffect } from 'react';
import { useParams, Link } from 'react-router-dom';
import { ArrowLeft, RefreshCw, Save, FileCode } from 'lucide-react';
import { promptsApi } from '../services/api';
import type { PromptContentDto } from '../types';

const CATEGORY_LABELS: Record<string, string> = {
  coder: 'Coder',
  debugger: 'Debugger',
  planner: 'Planner',
  requirement: 'Requirement',
  reviewer: 'Reviewer'
};

export default function PromptDetail() {
  const { category, name } = useParams<{ category: string; name: string }>();
  const [prompt, setPrompt] = useState<PromptContentDto | null>(null);
  const [content, setContent] = useState('');
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [saved, setSaved] = useState(false);

  useEffect(() => {
    if (category && name) loadPrompt(category, name);
  }, [category, name]);

  const loadPrompt = async (cat: string, n: string) => {
    setLoading(true);
    setError(null);
    try {
      const data = await promptsApi.get(cat, n);
      setPrompt(data);
      setContent(data.content);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load prompt');
    } finally {
      setLoading(false);
    }
  };

  const handleSave = async () => {
    if (!category || !name) return;
    setSaving(true);
    setError(null);
    try {
      await promptsApi.update(category, name, content);
      setSaved(true);
      setTimeout(() => setSaved(false), 2000);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to save prompt');
    } finally {
      setSaving(false);
    }
  };

  if (loading) {
    return (
      <div className="flex items-center justify-center h-full">
        <RefreshCw className="w-8 h-8 text-blue-400 animate-spin" />
      </div>
    );
  }

  if (!prompt || !category || !name) {
    return (
      <div className="flex flex-col items-center justify-center h-full">
        <div className="text-5xl mb-4">🔍</div>
        <h2 className="text-xl font-semibold text-white mb-2">Prompt Not Found</h2>
        <Link to="/prompts" className="text-blue-400 hover:underline">Back to Prompts</Link>
      </div>
    );
  }

  return (
    <div className="p-8">
      {/* Header – aligned with KnowledgeDetail / StoryDetail */}
      <div className="flex items-center justify-between mb-8">
        <div className="flex items-center gap-4">
          <Link
            to="/prompts"
            className="p-2 text-slate-400 hover:text-white hover:bg-slate-700 rounded-lg transition-colors"
          >
            <ArrowLeft className="w-5 h-5" />
          </Link>
          <div>
            <div className="flex items-center gap-3">
              <FileCode className="w-7 h-7 text-slate-400 flex-shrink-0" />
              <h1 className="text-2xl font-bold text-white">{name}.md</h1>
              <span className="px-2 py-0.5 rounded text-sm bg-slate-600 text-slate-300">
                {CATEGORY_LABELS[category] ?? category}
              </span>
            </div>
            <p className="text-slate-400 mt-0.5 font-mono text-sm">{category}/{name}.md</p>
          </div>
        </div>
        <button
          onClick={handleSave}
          disabled={saving || content === prompt.content}
          className="flex items-center gap-2 px-4 py-2 bg-blue-600 hover:bg-blue-700 disabled:opacity-50 disabled:cursor-not-allowed text-white rounded-lg transition-colors"
        >
          <Save className="w-4 h-4" />
          {saving ? 'Saving…' : saved ? 'Saved' : 'Save'}
        </button>
      </div>

      {error && (
        <div className="mb-6 p-4 bg-red-500/10 border border-red-500/30 rounded-xl text-red-400">
          {error}
        </div>
      )}

      {/* Content in single card */}
      <div className="bg-slate-800/50 backdrop-blur border border-slate-700 rounded-xl p-6">
        <label className="block text-sm font-medium text-slate-400 mb-2">Content (Markdown)</label>
        <textarea
          value={content}
          onChange={(e) => setContent(e.target.value)}
          className="w-full min-h-[480px] px-4 py-3 bg-slate-900 border border-slate-600 rounded-lg text-white font-mono text-sm placeholder-slate-500 focus:outline-none focus:border-blue-500 resize-y"
          placeholder="Prompt content…"
          spellCheck={false}
        />
      </div>
    </div>
  );
}
