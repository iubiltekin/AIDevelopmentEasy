import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { Plus, Activity, CheckCircle, Bug, FileCode, Lightbulb, ChevronRight, Trash2, Search, BookOpen } from 'lucide-react';
import { knowledgeApi } from '../services/api';
import { PageLayout, StatCard, StatsGrid, ErrorAlert, LoadingSpinner, EmptyState } from '../components';
import type {
  KnowledgeEntryDto,
  KnowledgeStatsDto
} from '../types';
import {
  KnowledgeCategory,
  getKnowledgeCategoryLabel,
  getKnowledgeCategoryColor,
  getKnowledgeCategoryIcon
} from '../types';

export default function Knowledge() {
  const navigate = useNavigate();
  const [entries, setEntries] = useState<KnowledgeEntryDto[]>([]);
  const [stats, setStats] = useState<KnowledgeStatsDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [selectedCategory, setSelectedCategory] = useState<KnowledgeCategory | null>(null);
  const [searchQuery, setSearchQuery] = useState('');
  const [tags, setTags] = useState<string[]>([]);
  const [selectedTags, setSelectedTags] = useState<string[]>([]);
  const [showSearch, setShowSearch] = useState(false);

  useEffect(() => {
    loadData();
  }, [selectedCategory]);

  const loadData = async () => {
    setLoading(true);
    setError(null);
    try {
      const [entriesData, statsData, tagsData] = await Promise.all([
        selectedCategory !== null
          ? knowledgeApi.getAll(selectedCategory)
          : knowledgeApi.getAll(),
        knowledgeApi.getStats(),
        knowledgeApi.getTags()
      ]);
      setEntries(entriesData);
      setStats(statsData);
      setTags(tagsData);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load knowledge base');
    } finally {
      setLoading(false);
    }
  };

  const handleSearch = async () => {
    if (!searchQuery && selectedTags.length === 0) {
      loadData();
      return;
    }

    setLoading(true);
    try {
      const results = await knowledgeApi.search({
        query: searchQuery || undefined,
        category: selectedCategory ?? undefined,
        tags: selectedTags.length > 0 ? selectedTags : undefined,
        limit: 50
      });
      setEntries(results);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Search failed');
    } finally {
      setLoading(false);
    }
  };

  const handleDelete = async (id: string, e: React.MouseEvent) => {
    e.stopPropagation();
    if (!confirm('Are you sure you want to delete this entry?')) return;

    try {
      await knowledgeApi.delete(id);
      await loadData();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Delete failed');
    }
  };

  const handleVerify = async (id: string, e: React.MouseEvent) => {
    e.stopPropagation();
    try {
      await knowledgeApi.verify(id);
      await loadData();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Verification failed');
    }
  };

  const toggleTag = (tag: string) => {
    setSelectedTags(prev =>
      prev.includes(tag)
        ? prev.filter(t => t !== tag)
        : [...prev, tag]
    );
  };

  const filteredEntries = entries.filter(entry => {
    if (selectedTags.length === 0) return true;
    return selectedTags.some(tag => entry.tags.includes(tag));
  });

  // Computed stats
  const computedStats = {
    total: stats?.totalEntries ?? 0,
    patterns: stats?.patternsCount ?? 0,
    errors: stats?.errorsCount ?? 0,
    verified: stats?.verifiedCount ?? 0
  };

  return (
    <PageLayout
      title="Knowledge Base"
      description="Captured patterns, error fixes, and templates from successful pipelines"
      titleIcon={BookOpen}
      loading={loading}
      onRefresh={loadData}
      actions={[
        {
          label: 'Search',
          onClick: () => setShowSearch(!showSearch),
          icon: Search,
          variant: 'secondary'
        },
        {
          label: 'Add Pattern',
          onClick: () => navigate('/knowledge/new/pattern'),
          icon: Plus
        },
        {
          label: 'Add Error Fix',
          onClick: () => navigate('/knowledge/new/error'),
          icon: Bug,
          variant: 'secondary'
        },
        {
          label: 'Add Template',
          onClick: () => navigate('/knowledge/new/template'),
          icon: FileCode,
          variant: 'secondary'
        },
        {
          label: 'Add Agent Insight',
          onClick: () => navigate('/knowledge/new/insight'),
          icon: Lightbulb,
          variant: 'secondary'
        }
      ]}
    >
      {/* Stats */}
      <StatsGrid>
        <StatCard
          icon={Activity}
          iconColor="text-blue-400"
          bgColor="bg-blue-500/20"
          value={computedStats.total}
          label="Total Entries"
        />
        <StatCard
          icon={FileCode}
          iconColor="text-purple-400"
          bgColor="bg-purple-500/20"
          value={computedStats.patterns}
          label="Patterns"
        />
        <StatCard
          icon={Bug}
          iconColor="text-red-400"
          bgColor="bg-red-500/20"
          value={computedStats.errors}
          label="Error Fixes"
        />
        <StatCard
          icon={CheckCircle}
          iconColor="text-emerald-400"
          bgColor="bg-emerald-500/20"
          value={computedStats.verified}
          label="Verified"
        />
      </StatsGrid>

      {/* Search Panel */}
      {showSearch && (
        <div className="mb-6 p-4 bg-slate-800/50 backdrop-blur border border-slate-700 rounded-xl">
          <div className="flex flex-wrap gap-4 items-center">
            {/* Category Filter */}
            <div className="flex gap-2">
              <button
                onClick={() => setSelectedCategory(null)}
                className={`px-3 py-1.5 rounded-lg text-sm transition-colors ${selectedCategory === null
                  ? 'bg-blue-600 text-white'
                  : 'bg-slate-700 text-slate-300 hover:bg-slate-600'
                  }`}
              >
                All
              </button>
              {Object.values(KnowledgeCategory)
                .filter(v => typeof v === 'number')
                .map(cat => (
                  <button
                    key={cat}
                    onClick={() => setSelectedCategory(cat as KnowledgeCategory)}
                    className={`px-3 py-1.5 rounded-lg text-sm transition-colors flex items-center gap-1 ${selectedCategory === cat
                      ? 'bg-blue-600 text-white'
                      : 'bg-slate-700 text-slate-300 hover:bg-slate-600'
                      }`}
                  >
                    <span>{getKnowledgeCategoryIcon(cat as KnowledgeCategory)}</span>
                    {getKnowledgeCategoryLabel(cat as KnowledgeCategory)}
                  </button>
                ))}
            </div>

            {/* Search Input */}
            <div className="flex-1 flex gap-2">
              <input
                type="text"
                value={searchQuery}
                onChange={(e) => setSearchQuery(e.target.value)}
                onKeyDown={(e) => e.key === 'Enter' && handleSearch()}
                placeholder="Search knowledge base..."
                className="flex-1 px-4 py-2 bg-slate-700 border border-slate-600 rounded-lg text-white placeholder-slate-400 focus:outline-none focus:border-blue-500"
              />
              <button
                onClick={handleSearch}
                className="px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 transition-colors"
              >
                Search
              </button>
            </div>
          </div>

          {/* Tags */}
          {tags.length > 0 && (
            <div className="mt-4 flex flex-wrap gap-2">
              <span className="text-slate-400 text-sm">Tags:</span>
              {tags.slice(0, 20).map(tag => (
                <button
                  key={tag}
                  onClick={() => toggleTag(tag)}
                  className={`px-2 py-0.5 rounded text-xs transition-colors ${selectedTags.includes(tag)
                    ? 'bg-blue-600 text-white'
                    : 'bg-slate-700 text-slate-300 hover:bg-slate-600'
                    }`}
                >
                  {tag}
                </button>
              ))}
            </div>
          )}
        </div>
      )}

      {/* Error */}
      {error && <ErrorAlert message={error} />}

      {/* Loading */}
      {loading && <LoadingSpinner />}

      {/* Content */}
      {!loading && (
        <>
          {filteredEntries.length === 0 ? (
            <EmptyState
              message="No knowledge entries found. Start by adding patterns or error fixes."
              actionLabel="Add Pattern"
              onAction={() => navigate('/knowledge/new/pattern')}
            />
          ) : (
            <div className="space-y-3">
              {filteredEntries.map(entry => (
                <div
                  key={entry.id}
                  onClick={() => navigate(`/knowledge/${entry.id}`)}
                  className="bg-slate-800/50 backdrop-blur border border-slate-700 rounded-xl p-5 hover:border-slate-600 transition-all cursor-pointer group"
                >
                  <div className="flex items-start justify-between">
                    <div className="flex-1">
                      <div className="flex items-center gap-2 mb-2">
                        <span className={`px-2 py-0.5 rounded text-xs text-white ${getKnowledgeCategoryColor(entry.category)}`}>
                          {getKnowledgeCategoryIcon(entry.category)} {getKnowledgeCategoryLabel(entry.category)}
                        </span>
                        {entry.isVerified && (
                          <span className="px-2 py-0.5 bg-emerald-500/20 text-emerald-400 rounded text-xs flex items-center gap-1">
                            <CheckCircle className="w-3 h-3" />
                            Verified
                          </span>
                        )}
                        {entry.isManual && (
                          <span className="px-2 py-0.5 bg-slate-600 text-slate-300 rounded text-xs">
                            Manual
                          </span>
                        )}
                      </div>
                      <h3 className="text-white font-medium group-hover:text-blue-400 transition-colors">
                        {entry.title}
                      </h3>
                      <p className="text-slate-400 text-sm mt-1 line-clamp-2">
                        {entry.description}
                      </p>
                      {entry.tags.length > 0 && (
                        <div className="flex flex-wrap gap-1 mt-2">
                          {entry.tags.slice(0, 5).map(tag => (
                            <span key={tag} className="px-2 py-0.5 bg-slate-700 text-slate-400 rounded text-xs">
                              {tag}
                            </span>
                          ))}
                          {entry.tags.length > 5 && (
                            <span className="text-slate-500 text-xs">+{entry.tags.length - 5} more</span>
                          )}
                        </div>
                      )}
                    </div>
                    <div className="text-right ml-4 flex flex-col items-end">
                      <div className="text-slate-500 text-xs">
                        Used {entry.usageCount}× • {Math.round(entry.successRate * 100)}% success
                      </div>
                      <div className="text-slate-500 text-xs mt-1">
                        {new Date(entry.createdAt).toLocaleDateString()}
                      </div>
                      <div className="flex gap-2 mt-3 opacity-0 group-hover:opacity-100 transition-opacity">
                        {!entry.isVerified && (
                          <button
                            onClick={(e) => handleVerify(entry.id, e)}
                            className="p-1.5 bg-emerald-600 text-white rounded hover:bg-emerald-700 transition-colors"
                            title="Mark as verified"
                          >
                            <CheckCircle className="w-4 h-4" />
                          </button>
                        )}
                        <button
                          onClick={(e) => handleDelete(entry.id, e)}
                          className="p-1.5 bg-red-600 text-white rounded hover:bg-red-700 transition-colors"
                          title="Delete"
                        >
                          <Trash2 className="w-4 h-4" />
                        </button>
                        <ChevronRight className="w-5 h-5 text-slate-500" />
                      </div>
                    </div>
                  </div>
                </div>
              ))}
            </div>
          )}

          {/* Most Used Section */}
          {stats && stats.mostUsed.length > 0 && (
            <div className="mt-8 bg-slate-800/50 backdrop-blur border border-slate-700 rounded-xl p-5">
              <div className="flex items-center gap-2 mb-4">
                <Lightbulb className="w-5 h-5 text-amber-400" />
                <h2 className="text-lg font-semibold text-white">Most Used</h2>
              </div>
              <div className="grid grid-cols-1 md:grid-cols-2 gap-2">
                {stats.mostUsed.slice(0, 6).map(item => (
                  <div
                    key={item.id}
                    onClick={() => navigate(`/knowledge/${item.id}`)}
                    className="flex items-center justify-between p-3 bg-slate-700/50 rounded-lg cursor-pointer hover:bg-slate-700 transition-colors"
                  >
                    <div className="flex items-center gap-2">
                      <span>{getKnowledgeCategoryIcon(item.category)}</span>
                      <span className="text-white text-sm truncate">{item.title}</span>
                    </div>
                    <span className="text-slate-400 text-xs">{item.usageCount}×</span>
                  </div>
                ))}
              </div>
            </div>
          )}
        </>
      )}
    </PageLayout>
  );
}
