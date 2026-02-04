import { useState, useEffect } from 'react';
import { useParams, useNavigate, Link } from 'react-router-dom';
import { ArrowLeft, RefreshCw, Trash2, CheckCircle, BookOpen } from 'lucide-react';
import { knowledgeApi } from '../services/api';
import type {
  KnowledgeEntryDto,
  SuccessfulPatternDto,
  CommonErrorDto,
  ProjectTemplateDto,
  AgentInsightDto
} from '../types';
import {
  KnowledgeCategory,
  getKnowledgeCategoryLabel,
  getKnowledgeCategoryColor,
  getKnowledgeCategoryIcon,
  getPatternSubcategoryLabel,
  getErrorTypeLabel,
  getErrorTypeColor
} from '../types';

export default function KnowledgeDetail() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [entry, setEntry] = useState<KnowledgeEntryDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [copied, setCopied] = useState(false);
  const [activeTab, setActiveTab] = useState<'overview' | 'content'>('overview');

  useEffect(() => {
    if (id) {
      loadEntry(id);
    }
  }, [id]);

  const loadEntry = async (entryId: string) => {
    setLoading(true);
    setError(null);
    try {
      const data = await knowledgeApi.getById(entryId);
      setEntry(data);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load entry');
    } finally {
      setLoading(false);
    }
  };

  const handleVerify = async () => {
    if (!id) return;
    try {
      await knowledgeApi.verify(id);
      loadEntry(id);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Verification failed');
    }
  };

  const handleDelete = async () => {
    if (!id || !confirm('Delete this knowledge entry? This cannot be undone.')) return;
    try {
      await knowledgeApi.delete(id);
      navigate('/knowledge');
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Delete failed');
    }
  };

  const copyToClipboard = (text: string) => {
    navigator.clipboard.writeText(text);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };

  if (loading) {
    return (
      <div className="flex items-center justify-center h-full">
        <RefreshCw className="w-8 h-8 text-blue-400 animate-spin" />
      </div>
    );
  }

  if (!entry) {
    return (
      <div className="flex flex-col items-center justify-center h-full">
        <div className="text-5xl mb-4">🔍</div>
        <h2 className="text-xl font-semibold text-white mb-2">Entry Not Found</h2>
        <Link to="/knowledge" className="text-blue-400 hover:underline">Back to Knowledge Base</Link>
      </div>
    );
  }

  const isPattern = entry.category === KnowledgeCategory.Pattern;
  const isError = entry.category === KnowledgeCategory.Error;
  const isTemplate = entry.category === KnowledgeCategory.Template;
  const isInsight = entry.category === KnowledgeCategory.AgentInsight;

  const patternEntry = isPattern ? (entry as SuccessfulPatternDto) : null;
  const errorEntry = isError ? (entry as CommonErrorDto) : null;
  const templateEntry = isTemplate ? (entry as ProjectTemplateDto) : null;
  const insightEntry = isInsight ? (entry as AgentInsightDto) : null;

  return (
    <div className="p-8">
      {/* Header – same layout as StoryDetail */}
      <div className="flex items-center justify-between mb-8">
        <div className="flex items-center gap-4">
          <Link
            to="/knowledge"
            className="p-2 text-slate-400 hover:text-white hover:bg-slate-700 rounded-lg transition-colors"
          >
            <ArrowLeft className="w-5 h-5" />
          </Link>
          <div>
            <div className="flex items-center gap-3">
              <BookOpen className="w-7 h-7 text-slate-400 flex-shrink-0" />
              <h1 className="text-2xl font-bold text-white">{entry.title}</h1>
              <span className={`px-2 py-0.5 rounded text-sm text-white ${getKnowledgeCategoryColor(entry.category)}`}>
                {getKnowledgeCategoryIcon(entry.category)} {getKnowledgeCategoryLabel(entry.category)}
              </span>
              {entry.isVerified && (
                <span className="px-2 py-0.5 bg-emerald-600 text-white rounded text-sm flex items-center gap-1">
                  <CheckCircle className="w-3.5 h-3.5" /> Verified
                </span>
              )}
              {entry.isManual && (
                <span className="px-2 py-0.5 bg-slate-600 text-slate-300 rounded text-sm">
                  Manual
                </span>
              )}
            </div>
            <p className="text-slate-400 mt-0.5 font-mono text-sm">{entry.id}</p>
          </div>
        </div>
        <div className="flex gap-3">
          {!entry.isVerified && (
            <button
              onClick={handleVerify}
              className="flex items-center gap-2 px-4 py-2 bg-emerald-600 hover:bg-emerald-700 text-white rounded-lg transition-colors"
            >
              <CheckCircle className="w-4 h-4" />
              Mark Verified
            </button>
          )}
          <button
            onClick={handleDelete}
            className="p-2 text-slate-400 hover:text-red-400 hover:bg-slate-700 rounded-lg transition-colors"
            title="Delete"
          >
            <Trash2 className="w-5 h-5" />
          </button>
        </div>
      </div>

      {error && (
        <div className="mb-6 p-4 bg-red-500/10 border border-red-500/30 rounded-xl text-red-400">
          {error}
        </div>
      )}

      {/* Tabs – same pattern as StoryDetail */}
      <div className="flex gap-2 mb-6">
        {(['overview', 'content'] as const).map(tab => (
          <button
            key={tab}
            onClick={() => setActiveTab(tab)}
            className={`px-4 py-2 rounded-lg font-medium transition-colors ${activeTab === tab
              ? 'bg-blue-600 text-white'
              : 'bg-slate-800 text-slate-400 hover:text-white'
              }`}
          >
            {tab.charAt(0).toUpperCase() + tab.slice(1)}
          </button>
        ))}
      </div>

      {/* Tab content in single card – same style as StoryDetail */}
      <div className="bg-slate-800/50 backdrop-blur border border-slate-700 rounded-xl p-6">
        {activeTab === 'overview' && (
          <div className="grid grid-cols-2 gap-6">
            <div>
              <h3 className="text-lg font-semibold text-white mb-4">Details</h3>
              <dl className="space-y-3">
                <div>
                  <dt className="text-sm text-slate-400">ID</dt>
                  <dd className="text-white font-mono">{entry.id}</dd>
                </div>
                <div>
                  <dt className="text-sm text-slate-400">Category</dt>
                  <dd className="text-white">
                    {getKnowledgeCategoryIcon(entry.category)} {getKnowledgeCategoryLabel(entry.category)}
                  </dd>
                </div>
                <div>
                  <dt className="text-sm text-slate-400">Created</dt>
                  <dd className="text-white">{new Date(entry.createdAt).toLocaleString()}</dd>
                </div>
                {entry.sourceStoryId && (
                  <div>
                    <dt className="text-sm text-slate-400">Source Story</dt>
                    <dd className="text-white">
                      <Link to={`/stories/${entry.sourceStoryId}`} className="text-blue-400 hover:text-blue-300 font-mono">
                        {entry.sourceStoryId}
                      </Link>
                    </dd>
                  </div>
                )}
              </dl>
            </div>
            <div>
              <h3 className="text-lg font-semibold text-white mb-4">Stats</h3>
              <div className="space-y-3">
                <div className="p-4 bg-slate-900 rounded-lg">
                  <div className="text-3xl font-bold text-blue-400">{entry.usageCount}</div>
                  <div className="text-sm text-slate-400">Times Used</div>
                </div>
                <div className="p-4 bg-slate-900 rounded-lg">
                  <div className="text-3xl font-bold text-emerald-400">{Math.round(entry.successRate * 100)}%</div>
                  <div className="text-sm text-slate-400">Success Rate</div>
                </div>
                <div className="p-4 bg-slate-900 rounded-lg">
                  <div className="text-lg font-bold text-white">
                    {entry.lastUsedAt ? new Date(entry.lastUsedAt).toLocaleString() : 'Never'}
                  </div>
                  <div className="text-sm text-slate-400">Last Used</div>
                </div>
              </div>
            </div>
          </div>
        )}

        {activeTab === 'content' && (
          <div className="space-y-6">
            <div>
              <h3 className="text-lg font-semibold text-white mb-2">Description</h3>
              <p className="text-slate-300 whitespace-pre-wrap">{entry.description}</p>
            </div>

            {entry.tags.length > 0 && (
              <div>
                <h3 className="text-lg font-semibold text-white mb-2">Tags</h3>
                <div className="flex flex-wrap gap-2">
                  {entry.tags.map(tag => (
                    <span key={tag} className="px-3 py-1 bg-slate-700 text-slate-300 rounded-lg text-sm">
                      {tag}
                    </span>
                  ))}
                </div>
              </div>
            )}

            {/* Pattern-specific content */}
            {patternEntry && (
              <>
                <div>
                  <div className="flex items-center justify-between mb-2">
                    <h3 className="text-lg font-semibold text-white">Problem Description</h3>
                    <span className="px-2 py-0.5 bg-slate-700 text-slate-300 rounded text-sm">
                      {getPatternSubcategoryLabel(patternEntry.subcategory)}
                    </span>
                  </div>
                  <p className="text-slate-300">{patternEntry.problemDescription}</p>
                </div>

                <div>
                  <div className="flex items-center justify-between mb-2">
                    <h3 className="text-lg font-semibold text-white">Solution Code</h3>
                    <button
                      onClick={() => copyToClipboard(patternEntry.solutionCode)}
                      className="px-3 py-1 bg-slate-700 text-slate-300 rounded hover:bg-slate-600 transition-colors text-sm"
                    >
                      {copied ? '✓ Copied!' : 'Copy'}
                    </button>
                  </div>
                  <pre className="bg-slate-900 p-4 rounded-lg overflow-x-auto text-sm">
                    <code className="text-emerald-300">{patternEntry.solutionCode}</code>
                  </pre>
                </div>

                {patternEntry.applicableScenarios.length > 0 && (
                  <div>
                    <h3 className="text-lg font-semibold text-white mb-2">Applicable Scenarios</h3>
                    <ul className="list-disc list-inside space-y-1 text-slate-300">
                      {patternEntry.applicableScenarios.map((scenario, i) => (
                        <li key={i}>{scenario}</li>
                      ))}
                    </ul>
                  </div>
                )}

                {patternEntry.exampleUsage && (
                  <div>
                    <h3 className="text-lg font-semibold text-white mb-2">Example Usage</h3>
                    <pre className="bg-slate-900 p-4 rounded-lg overflow-x-auto text-sm">
                      <code className="text-blue-300">{patternEntry.exampleUsage}</code>
                    </pre>
                  </div>
                )}

                {patternEntry.dependencies.length > 0 && (
                  <div>
                    <h3 className="text-lg font-semibold text-white mb-2">Dependencies</h3>
                    <div className="flex flex-wrap gap-2">
                      {patternEntry.dependencies.map(dep => (
                        <span key={dep} className="px-3 py-1 bg-purple-900/50 text-purple-300 rounded-lg text-sm">
                          {dep}
                        </span>
                      ))}
                    </div>
                  </div>
                )}
              </>
            )}

            {/* Error-specific content */}
            {errorEntry && (
              <>
                <div>
                  <div className="flex items-center gap-2 mb-2">
                    <h3 className="text-lg font-semibold text-white">Error Type</h3>
                    <span className={`px-2 py-0.5 rounded text-sm text-white ${getErrorTypeColor(errorEntry.errorType)}`}>
                      {getErrorTypeLabel(errorEntry.errorType)}
                    </span>
                    <span className="text-slate-400 text-sm">
                      Occurred {errorEntry.occurrenceCount} time(s)
                    </span>
                  </div>
                  {errorEntry.errorMessage && (
                    <div className="mt-3">
                      <div className="text-slate-400 text-sm mb-1">Error Message:</div>
                      <pre className="bg-red-900/30 p-3 rounded-lg text-red-300 text-sm overflow-x-auto">
                        {errorEntry.errorMessage}
                      </pre>
                    </div>
                  )}
                  {errorEntry.errorPattern && errorEntry.errorPattern !== errorEntry.errorMessage && (
                    <div className="mt-3">
                      <div className="text-slate-400 text-sm mb-1">Error Pattern (Regex):</div>
                      <pre className="bg-slate-900 p-3 rounded-lg text-amber-300 text-sm overflow-x-auto">
                        {errorEntry.errorPattern}
                      </pre>
                    </div>
                  )}
                </div>

                <div>
                  <h3 className="text-lg font-semibold text-white mb-2">Root Cause</h3>
                  <p className="text-slate-300">{errorEntry.rootCause}</p>
                </div>

                <div>
                  <h3 className="text-lg font-semibold text-white mb-2">Fix Description</h3>
                  <p className="text-slate-300">{errorEntry.fixDescription}</p>
                </div>

                {errorEntry.fixCode && (
                  <div>
                    <div className="flex items-center justify-between mb-2">
                      <h3 className="text-lg font-semibold text-white">Fix Code</h3>
                      <button
                        onClick={() => copyToClipboard(errorEntry.fixCode!)}
                        className="px-3 py-1 bg-slate-700 text-slate-300 rounded hover:bg-slate-600 transition-colors text-sm"
                      >
                        {copied ? '✓ Copied!' : 'Copy'}
                      </button>
                    </div>
                    <pre className="bg-slate-900 p-4 rounded-lg overflow-x-auto text-sm">
                      <code className="text-emerald-300">{errorEntry.fixCode}</code>
                    </pre>
                  </div>
                )}

                {errorEntry.preventionTips.length > 0 && (
                  <div>
                    <h3 className="text-lg font-semibold text-white mb-2">Prevention Tips</h3>
                    <ul className="list-disc list-inside space-y-1 text-slate-300">
                      {errorEntry.preventionTips.map((tip, i) => (
                        <li key={i}>{tip}</li>
                      ))}
                    </ul>
                  </div>
                )}
              </>
            )}

            {/* Template-specific content */}
            {templateEntry && (
              <>
                <div>
                  <div className="flex items-center gap-4 mb-2">
                    <div>
                      <div className="text-slate-400 text-sm">Template Type</div>
                      <div className="text-white font-medium">{templateEntry.templateType}</div>
                    </div>
                    <div>
                      <div className="text-slate-400 text-sm">Target Framework</div>
                      <div className="text-white font-medium">{templateEntry.targetFramework}</div>
                    </div>
                  </div>
                </div>

                {templateEntry.packages.length > 0 && (
                  <div>
                    <h3 className="text-lg font-semibold text-white mb-2">Required Packages</h3>
                    <div className="space-y-1">
                      {templateEntry.packages.map((pkg, i) => (
                        <div key={i} className="flex items-center gap-2 text-slate-300">
                          <span className="font-mono">{pkg.name}</span>
                          {pkg.version && <span className="text-slate-500">v{pkg.version}</span>}
                          {pkg.isRequired && <span className="text-xs text-amber-400">(required)</span>}
                        </div>
                      ))}
                    </div>
                  </div>
                )}

                {templateEntry.templateFiles.length > 0 && (
                  <div>
                    <h3 className="text-lg font-semibold text-white mb-2">Template Files</h3>
                    <div className="space-y-4">
                      {templateEntry.templateFiles.map((file, i) => (
                        <div key={i}>
                          <div className="flex items-center justify-between mb-1">
                            <span className="font-mono text-blue-400">{file.path}</span>
                            <button
                              onClick={() => copyToClipboard(file.content)}
                              className="px-2 py-0.5 bg-slate-700 text-slate-300 rounded hover:bg-slate-600 text-xs"
                            >
                              Copy
                            </button>
                          </div>
                          <pre className="bg-slate-900 p-3 rounded-lg overflow-x-auto text-sm max-h-64">
                            <code className="text-slate-300">{file.content}</code>
                          </pre>
                        </div>
                      ))}
                    </div>
                  </div>
                )}

                {templateEntry.setupInstructions && (
                  <div>
                    <h3 className="text-lg font-semibold text-white mb-2">Setup Instructions</h3>
                    <p className="text-slate-300 whitespace-pre-wrap">{templateEntry.setupInstructions}</p>
                  </div>
                )}
              </>
            )}

            {/* Agent Insight-specific content */}
            {insightEntry && (
              <>
                <div>
                  <h3 className="text-lg font-semibold text-white mb-2">Agent</h3>
                  <p className="text-slate-300 font-medium">{insightEntry.agentName}</p>
                </div>
                <div>
                  <h3 className="text-lg font-semibold text-white mb-2">Scenario</h3>
                  <p className="text-slate-300">{insightEntry.scenario}</p>
                </div>
                {insightEntry.promptInsight && (
                  <div>
                    <h3 className="text-lg font-semibold text-white mb-2">Prompt Insight</h3>
                    <p className="text-slate-300 whitespace-pre-wrap">{insightEntry.promptInsight}</p>
                  </div>
                )}
                {insightEntry.optimalTemperature != null && (
                  <div>
                    <h3 className="text-lg font-semibold text-white mb-2">Optimal Temperature</h3>
                    <p className="text-slate-300">{insightEntry.optimalTemperature}</p>
                  </div>
                )}
                {insightEntry.improvementDescription && (
                  <div>
                    <h3 className="text-lg font-semibold text-white mb-2">Improvement Description</h3>
                    <p className="text-slate-300 whitespace-pre-wrap">{insightEntry.improvementDescription}</p>
                  </div>
                )}
              </>
            )}
          </div>
        )}
      </div>
    </div>
  );
}
