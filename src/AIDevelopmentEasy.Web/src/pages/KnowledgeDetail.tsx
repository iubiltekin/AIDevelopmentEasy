import { useState, useEffect } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { knowledgeApi } from '../services/api';
import type { 
  KnowledgeEntryDto,
  SuccessfulPatternDto, 
  CommonErrorDto,
  ProjectTemplateDto
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
    if (!id || !confirm('Are you sure you want to delete this entry?')) return;
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
      <div className="flex justify-center py-12">
        <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-blue-500"></div>
      </div>
    );
  }

  if (error || !entry) {
    return (
      <div className="bg-red-900/30 border border-red-700 text-red-300 p-4 rounded-lg">
        {error || 'Entry not found'}
        <button
          onClick={() => navigate('/knowledge')}
          className="ml-4 text-blue-400 hover:underline"
        >
          ← Back to Knowledge Base
        </button>
      </div>
    );
  }

  const isPattern = entry.category === KnowledgeCategory.Pattern;
  const isError = entry.category === KnowledgeCategory.Error;
  const isTemplate = entry.category === KnowledgeCategory.Template;

  const patternEntry = isPattern ? (entry as SuccessfulPatternDto) : null;
  const errorEntry = isError ? (entry as CommonErrorDto) : null;
  const templateEntry = isTemplate ? (entry as ProjectTemplateDto) : null;

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-start justify-between">
        <div>
          <button
            onClick={() => navigate('/knowledge')}
            className="text-slate-400 hover:text-white text-sm mb-2"
          >
            ← Back to Knowledge Base
          </button>
          <div className="flex items-center gap-2 mt-2">
            <span className={`px-2 py-0.5 rounded text-sm text-white ${getKnowledgeCategoryColor(entry.category)}`}>
              {getKnowledgeCategoryIcon(entry.category)} {getKnowledgeCategoryLabel(entry.category)}
            </span>
            {entry.isVerified && (
              <span className="px-2 py-0.5 bg-emerald-600 text-white rounded text-sm">
                ✓ Verified
              </span>
            )}
            {entry.isManual && (
              <span className="px-2 py-0.5 bg-slate-600 text-slate-300 rounded text-sm">
                Manual Entry
              </span>
            )}
          </div>
          <h1 className="text-2xl font-bold text-white mt-2">{entry.title}</h1>
        </div>
        <div className="flex gap-2">
          {!entry.isVerified && (
            <button
              onClick={handleVerify}
              className="px-4 py-2 bg-emerald-600 text-white rounded-lg hover:bg-emerald-700 transition-colors"
            >
              Mark as Verified
            </button>
          )}
          <button
            onClick={handleDelete}
            className="px-4 py-2 bg-red-600 text-white rounded-lg hover:bg-red-700 transition-colors"
          >
            Delete
          </button>
        </div>
      </div>

      {/* Stats */}
      <div className="grid grid-cols-4 gap-4">
        <div className="bg-slate-800 rounded-lg p-4 border border-slate-700">
          <div className="text-2xl font-bold text-white">{entry.usageCount}</div>
          <div className="text-slate-400 text-sm">Times Used</div>
        </div>
        <div className="bg-slate-800 rounded-lg p-4 border border-slate-700">
          <div className="text-2xl font-bold text-emerald-400">{Math.round(entry.successRate * 100)}%</div>
          <div className="text-slate-400 text-sm">Success Rate</div>
        </div>
        <div className="bg-slate-800 rounded-lg p-4 border border-slate-700">
          <div className="text-lg font-bold text-white">{new Date(entry.createdAt).toLocaleDateString()}</div>
          <div className="text-slate-400 text-sm">Created</div>
        </div>
        <div className="bg-slate-800 rounded-lg p-4 border border-slate-700">
          <div className="text-lg font-bold text-white">
            {entry.lastUsedAt ? new Date(entry.lastUsedAt).toLocaleDateString() : 'Never'}
          </div>
          <div className="text-slate-400 text-sm">Last Used</div>
        </div>
      </div>

      {/* Description */}
      <div className="bg-slate-800 rounded-lg p-4 border border-slate-700">
        <h2 className="text-lg font-semibold text-white mb-2">Description</h2>
        <p className="text-slate-300 whitespace-pre-wrap">{entry.description}</p>
      </div>

      {/* Tags */}
      {entry.tags.length > 0 && (
        <div className="bg-slate-800 rounded-lg p-4 border border-slate-700">
          <h2 className="text-lg font-semibold text-white mb-2">Tags</h2>
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
          <div className="bg-slate-800 rounded-lg p-4 border border-slate-700">
            <div className="flex items-center justify-between mb-2">
              <h2 className="text-lg font-semibold text-white">Problem Description</h2>
              <span className="px-2 py-0.5 bg-slate-700 text-slate-300 rounded text-sm">
                {getPatternSubcategoryLabel(patternEntry.subcategory)}
              </span>
            </div>
            <p className="text-slate-300">{patternEntry.problemDescription}</p>
          </div>

          <div className="bg-slate-800 rounded-lg p-4 border border-slate-700">
            <div className="flex items-center justify-between mb-2">
              <h2 className="text-lg font-semibold text-white">Solution Code</h2>
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
            <div className="bg-slate-800 rounded-lg p-4 border border-slate-700">
              <h2 className="text-lg font-semibold text-white mb-2">Applicable Scenarios</h2>
              <ul className="list-disc list-inside space-y-1">
                {patternEntry.applicableScenarios.map((scenario, i) => (
                  <li key={i} className="text-slate-300">{scenario}</li>
                ))}
              </ul>
            </div>
          )}

          {patternEntry.exampleUsage && (
            <div className="bg-slate-800 rounded-lg p-4 border border-slate-700">
              <h2 className="text-lg font-semibold text-white mb-2">Example Usage</h2>
              <pre className="bg-slate-900 p-4 rounded-lg overflow-x-auto text-sm">
                <code className="text-blue-300">{patternEntry.exampleUsage}</code>
              </pre>
            </div>
          )}

          {patternEntry.dependencies.length > 0 && (
            <div className="bg-slate-800 rounded-lg p-4 border border-slate-700">
              <h2 className="text-lg font-semibold text-white mb-2">Dependencies</h2>
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
          <div className="bg-slate-800 rounded-lg p-4 border border-slate-700">
            <div className="flex items-center gap-2 mb-2">
              <h2 className="text-lg font-semibold text-white">Error Type</h2>
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

          <div className="bg-slate-800 rounded-lg p-4 border border-slate-700">
            <h2 className="text-lg font-semibold text-white mb-2">Root Cause</h2>
            <p className="text-slate-300">{errorEntry.rootCause}</p>
          </div>

          <div className="bg-slate-800 rounded-lg p-4 border border-slate-700">
            <h2 className="text-lg font-semibold text-white mb-2">Fix Description</h2>
            <p className="text-slate-300">{errorEntry.fixDescription}</p>
          </div>

          {errorEntry.fixCode && (
            <div className="bg-slate-800 rounded-lg p-4 border border-slate-700">
              <div className="flex items-center justify-between mb-2">
                <h2 className="text-lg font-semibold text-white">Fix Code</h2>
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
            <div className="bg-slate-800 rounded-lg p-4 border border-slate-700">
              <h2 className="text-lg font-semibold text-white mb-2">Prevention Tips</h2>
              <ul className="list-disc list-inside space-y-1">
                {errorEntry.preventionTips.map((tip, i) => (
                  <li key={i} className="text-slate-300">{tip}</li>
                ))}
              </ul>
            </div>
          )}
        </>
      )}

      {/* Template-specific content */}
      {templateEntry && (
        <>
          <div className="bg-slate-800 rounded-lg p-4 border border-slate-700">
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
            <div className="bg-slate-800 rounded-lg p-4 border border-slate-700">
              <h2 className="text-lg font-semibold text-white mb-2">Required Packages</h2>
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
            <div className="bg-slate-800 rounded-lg p-4 border border-slate-700">
              <h2 className="text-lg font-semibold text-white mb-2">Template Files</h2>
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
            <div className="bg-slate-800 rounded-lg p-4 border border-slate-700">
              <h2 className="text-lg font-semibold text-white mb-2">Setup Instructions</h2>
              <p className="text-slate-300 whitespace-pre-wrap">{templateEntry.setupInstructions}</p>
            </div>
          )}
        </>
      )}

      {/* Source Info */}
      {entry.sourceStoryId && (
        <div className="bg-slate-800 rounded-lg p-4 border border-slate-700">
          <h2 className="text-lg font-semibold text-white mb-2">Source</h2>
          <p className="text-slate-300">
            Captured from story: <span className="font-mono text-blue-400">{entry.sourceStoryId}</span>
          </p>
        </div>
      )}
    </div>
  );
}
