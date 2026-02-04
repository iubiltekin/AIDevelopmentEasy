import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { FileCode, ChevronRight } from 'lucide-react';
import { promptsApi } from '../services/api';
import type { PromptsByCategory } from '../types';
import { PageLayout, ErrorAlert, LoadingSpinner, EmptyState } from '../components';

const CATEGORY_LABELS: Record<string, string> = {
  coder: 'Coder',
  debugger: 'Debugger',
  planner: 'Planner',
  requirement: 'Requirement',
  reviewer: 'Reviewer'
};

export default function Prompts() {
  const navigate = useNavigate();
  const [data, setData] = useState<PromptsByCategory | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [selectedCategory, setSelectedCategory] = useState<string | null>(null);

  useEffect(() => {
    loadData();
  }, []);

  const loadData = async () => {
    setLoading(true);
    setError(null);
    try {
      const list = await promptsApi.list();
      setData(list);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load prompts');
    } finally {
      setLoading(false);
    }
  };

  const categories = data ? Object.keys(data).sort() : [];
  const entries: { category: string; name: string }[] = [];
  if (data) {
    const cats = selectedCategory ? [selectedCategory] : categories;
    for (const cat of cats) {
      const names = data[cat];
      if (names) for (const name of names) entries.push({ category: cat, name });
    }
  }

  return (
    <PageLayout
      title="Prompts"
      description="Agent prompt templates by category. View and edit existing prompts."
      titleIcon={FileCode}
      loading={loading}
      onRefresh={loadData}
    >
      {error && <ErrorAlert message={error} />}
      {loading && <LoadingSpinner />}

      {!loading && data && (
        <>
          <div className="flex flex-wrap gap-2 mb-6">
            <button
              onClick={() => setSelectedCategory(null)}
              className={`px-3 py-1.5 rounded-lg text-sm transition-colors ${selectedCategory === null ? 'bg-blue-600 text-white' : 'bg-slate-700 text-slate-300 hover:bg-slate-600'}`}
            >
              All
            </button>
            {categories.map(cat => (
              <button
                key={cat}
                onClick={() => setSelectedCategory(cat)}
                className={`px-3 py-1.5 rounded-lg text-sm transition-colors ${selectedCategory === cat ? 'bg-blue-600 text-white' : 'bg-slate-700 text-slate-300 hover:bg-slate-600'}`}
              >
                {CATEGORY_LABELS[cat] ?? cat}
              </button>
            ))}
          </div>

          {entries.length === 0 ? (
            <EmptyState message="No prompts found." />
          ) : (
            <div className="space-y-3">
              {entries.map(({ category, name }) => (
                <div
                  key={`${category}/${name}`}
                  onClick={() => navigate(`/prompts/${category}/${name}`)}
                  className="bg-slate-800/50 backdrop-blur border border-slate-700 rounded-xl p-5 hover:border-slate-600 transition-all cursor-pointer group"
                >
                  <div className="flex items-center justify-between">
                    <div className="flex items-center gap-3">
                      <div className="p-2 bg-slate-700/50 rounded-lg">
                        <FileCode className="w-5 h-5 text-slate-400" />
                      </div>
                      <div>
                        <span className="px-2 py-0.5 rounded text-xs bg-slate-600 text-slate-300 mr-2">
                          {CATEGORY_LABELS[category] ?? category}
                        </span>
                        <span className="text-white font-medium group-hover:text-blue-400 transition-colors">
                          {name}.md
                        </span>
                      </div>
                    </div>
                    <ChevronRight className="w-5 h-5 text-slate-500" />
                  </div>
                </div>
              ))}
            </div>
          )}
        </>
      )}
    </PageLayout>
  );
}
