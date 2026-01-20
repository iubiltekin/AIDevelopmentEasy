import { useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { knowledgeApi } from '../services/api';
import { 
  PatternSubcategory, 
  ErrorType,
  getPatternSubcategoryLabel,
  getErrorTypeLabel 
} from '../types';

type CreateType = 'pattern' | 'error';

export default function KnowledgeCreate() {
  const { type } = useParams<{ type: CreateType }>();
  const navigate = useNavigate();
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Common fields
  const [title, setTitle] = useState('');
  const [tags, setTags] = useState('');
  const [language, setLanguage] = useState('csharp');

  // Pattern fields
  const [problemDescription, setProblemDescription] = useState('');
  const [solutionCode, setSolutionCode] = useState('');
  const [subcategory, setSubcategory] = useState<PatternSubcategory>(PatternSubcategory.Other);
  const [applicableScenarios, setApplicableScenarios] = useState('');
  const [exampleUsage, setExampleUsage] = useState('');
  const [dependencies, setDependencies] = useState('');

  // Error fields
  const [errorType, setErrorType] = useState<ErrorType>(ErrorType.Compilation);
  const [errorPattern, setErrorPattern] = useState('');
  const [errorMessage, setErrorMessage] = useState('');
  const [rootCause, setRootCause] = useState('');
  const [fixDescription, setFixDescription] = useState('');
  const [fixCode, setFixCode] = useState('');
  const [preventionTips, setPreventionTips] = useState('');

  const isPattern = type === 'pattern';

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setLoading(true);
    setError(null);

    try {
      const tagList = tags.split(',').map(t => t.trim()).filter(t => t);

      if (isPattern) {
        await knowledgeApi.createPattern({
          title,
          problemDescription,
          solutionCode,
          subcategory,
          tags: tagList,
          language,
          context: undefined,
          applicableScenarios: applicableScenarios.split('\n').map(s => s.trim()).filter(s => s),
          exampleUsage: exampleUsage || undefined,
          dependencies: dependencies.split(',').map(d => d.trim()).filter(d => d)
        });
      } else {
        await knowledgeApi.createError({
          title,
          errorType,
          errorPattern,
          errorMessage: errorMessage || undefined,
          rootCause,
          fixDescription,
          fixCode: fixCode || undefined,
          tags: tagList,
          language,
          preventionTips: preventionTips.split('\n').map(t => t.trim()).filter(t => t)
        });
      }

      navigate('/knowledge');
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to create entry');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="p-8 max-w-4xl mx-auto">
      {/* Header */}
      <div className="mb-8">
        <button
          onClick={() => navigate('/knowledge')}
          className="text-slate-400 hover:text-white text-sm mb-2"
        >
          ← Back to Knowledge Base
        </button>
        <h1 className="text-2xl font-bold text-white">
          {isPattern ? 'Add New Pattern' : 'Add New Error Fix'}
        </h1>
        <p className="text-slate-400 text-sm mt-1">
          {isPattern 
            ? 'Document a successful code pattern for future reference'
            : 'Document an error and its fix to help with future debugging'
          }
        </p>
      </div>

      {/* Error */}
      {error && (
        <div className="bg-red-900/30 border border-red-700 text-red-300 p-4 rounded-lg mb-6">
          {error}
        </div>
      )}

      {/* Form */}
      <form onSubmit={handleSubmit} className="space-y-6">
        {/* Common Fields */}
        <div className="bg-slate-800 rounded-lg p-6 border border-slate-700 space-y-4">
          <h2 className="text-lg font-semibold text-white mb-4">Basic Information</h2>
          
          <div>
            <label className="block text-sm font-medium text-slate-300 mb-1">
              Title *
            </label>
            <input
              type="text"
              value={title}
              onChange={(e) => setTitle(e.target.value)}
              required
              placeholder={isPattern ? 'e.g., Repository Pattern with Generic CRUD' : 'e.g., NullReferenceException in async method'}
              className="w-full px-4 py-2 bg-slate-700 border border-slate-600 rounded-lg text-white placeholder-slate-400 focus:outline-none focus:border-blue-500"
            />
          </div>

          <div className="grid grid-cols-2 gap-4">
            <div>
              <label className="block text-sm font-medium text-slate-300 mb-1">
                Language
              </label>
              <select
                value={language}
                onChange={(e) => setLanguage(e.target.value)}
                className="w-full px-4 py-2 bg-slate-700 border border-slate-600 rounded-lg text-white focus:outline-none focus:border-blue-500"
              >
                <option value="csharp">C#</option>
                <option value="typescript">TypeScript</option>
                <option value="javascript">JavaScript</option>
                <option value="python">Python</option>
              </select>
            </div>
            <div>
              <label className="block text-sm font-medium text-slate-300 mb-1">
                {isPattern ? 'Subcategory' : 'Error Type'}
              </label>
              {isPattern ? (
                <select
                  value={subcategory}
                  onChange={(e) => setSubcategory(Number(e.target.value) as PatternSubcategory)}
                  className="w-full px-4 py-2 bg-slate-700 border border-slate-600 rounded-lg text-white focus:outline-none focus:border-blue-500"
                >
                  {Object.values(PatternSubcategory)
                    .filter(v => typeof v === 'number')
                    .map(sub => (
                      <option key={sub} value={sub}>
                        {getPatternSubcategoryLabel(sub as PatternSubcategory)}
                      </option>
                    ))}
                </select>
              ) : (
                <select
                  value={errorType}
                  onChange={(e) => setErrorType(Number(e.target.value) as ErrorType)}
                  className="w-full px-4 py-2 bg-slate-700 border border-slate-600 rounded-lg text-white focus:outline-none focus:border-blue-500"
                >
                  {Object.values(ErrorType)
                    .filter(v => typeof v === 'number')
                    .map(type => (
                      <option key={type} value={type}>
                        {getErrorTypeLabel(type as ErrorType)}
                      </option>
                    ))}
                </select>
              )}
            </div>
          </div>

          <div>
            <label className="block text-sm font-medium text-slate-300 mb-1">
              Tags (comma separated)
            </label>
            <input
              type="text"
              value={tags}
              onChange={(e) => setTags(e.target.value)}
              placeholder="async, repository, di"
              className="w-full px-4 py-2 bg-slate-700 border border-slate-600 rounded-lg text-white placeholder-slate-400 focus:outline-none focus:border-blue-500"
            />
          </div>
        </div>

        {/* Pattern-specific fields */}
        {isPattern && (
          <div className="bg-slate-800 rounded-lg p-6 border border-slate-700 space-y-4">
            <h2 className="text-lg font-semibold text-white mb-4">Pattern Details</h2>
            
            <div>
              <label className="block text-sm font-medium text-slate-300 mb-1">
                Problem Description *
              </label>
              <textarea
                value={problemDescription}
                onChange={(e) => setProblemDescription(e.target.value)}
                required
                rows={3}
                placeholder="Describe the problem this pattern solves..."
                className="w-full px-4 py-2 bg-slate-700 border border-slate-600 rounded-lg text-white placeholder-slate-400 focus:outline-none focus:border-blue-500"
              />
            </div>

            <div>
              <label className="block text-sm font-medium text-slate-300 mb-1">
                Solution Code *
              </label>
              <textarea
                value={solutionCode}
                onChange={(e) => setSolutionCode(e.target.value)}
                required
                rows={12}
                placeholder="// Paste your solution code here..."
                className="w-full px-4 py-2 bg-slate-900 border border-slate-600 rounded-lg text-emerald-300 font-mono text-sm placeholder-slate-500 focus:outline-none focus:border-blue-500"
              />
            </div>

            <div>
              <label className="block text-sm font-medium text-slate-300 mb-1">
                Applicable Scenarios (one per line)
              </label>
              <textarea
                value={applicableScenarios}
                onChange={(e) => setApplicableScenarios(e.target.value)}
                rows={3}
                placeholder="When implementing data access layer&#10;When needing async CRUD operations&#10;When using Entity Framework"
                className="w-full px-4 py-2 bg-slate-700 border border-slate-600 rounded-lg text-white placeholder-slate-400 focus:outline-none focus:border-blue-500"
              />
            </div>

            <div>
              <label className="block text-sm font-medium text-slate-300 mb-1">
                Example Usage
              </label>
              <textarea
                value={exampleUsage}
                onChange={(e) => setExampleUsage(e.target.value)}
                rows={5}
                placeholder="// Show how to use this pattern..."
                className="w-full px-4 py-2 bg-slate-900 border border-slate-600 rounded-lg text-blue-300 font-mono text-sm placeholder-slate-500 focus:outline-none focus:border-blue-500"
              />
            </div>

            <div>
              <label className="block text-sm font-medium text-slate-300 mb-1">
                Dependencies (comma separated)
              </label>
              <input
                type="text"
                value={dependencies}
                onChange={(e) => setDependencies(e.target.value)}
                placeholder="Microsoft.EntityFrameworkCore, Newtonsoft.Json"
                className="w-full px-4 py-2 bg-slate-700 border border-slate-600 rounded-lg text-white placeholder-slate-400 focus:outline-none focus:border-blue-500"
              />
            </div>
          </div>
        )}

        {/* Error-specific fields */}
        {!isPattern && (
          <div className="bg-slate-800 rounded-lg p-6 border border-slate-700 space-y-4">
            <h2 className="text-lg font-semibold text-white mb-4">Error Details</h2>
            
            <div>
              <label className="block text-sm font-medium text-slate-300 mb-1">
                Error Message
              </label>
              <textarea
                value={errorMessage}
                onChange={(e) => setErrorMessage(e.target.value)}
                rows={2}
                placeholder="Paste the exact error message..."
                className="w-full px-4 py-2 bg-red-900/30 border border-slate-600 rounded-lg text-red-300 font-mono text-sm placeholder-slate-500 focus:outline-none focus:border-blue-500"
              />
            </div>

            <div>
              <label className="block text-sm font-medium text-slate-300 mb-1">
                Error Pattern (Regex) *
              </label>
              <input
                type="text"
                value={errorPattern}
                onChange={(e) => setErrorPattern(e.target.value)}
                required
                placeholder="Object reference not set.*"
                className="w-full px-4 py-2 bg-slate-700 border border-slate-600 rounded-lg text-amber-300 font-mono placeholder-slate-400 focus:outline-none focus:border-blue-500"
              />
              <p className="text-slate-500 text-xs mt-1">
                Regex pattern to match similar errors. Use .* for wildcards.
              </p>
            </div>

            <div>
              <label className="block text-sm font-medium text-slate-300 mb-1">
                Root Cause *
              </label>
              <textarea
                value={rootCause}
                onChange={(e) => setRootCause(e.target.value)}
                required
                rows={2}
                placeholder="Explain why this error occurs..."
                className="w-full px-4 py-2 bg-slate-700 border border-slate-600 rounded-lg text-white placeholder-slate-400 focus:outline-none focus:border-blue-500"
              />
            </div>

            <div>
              <label className="block text-sm font-medium text-slate-300 mb-1">
                Fix Description *
              </label>
              <textarea
                value={fixDescription}
                onChange={(e) => setFixDescription(e.target.value)}
                required
                rows={3}
                placeholder="Describe how to fix this error..."
                className="w-full px-4 py-2 bg-slate-700 border border-slate-600 rounded-lg text-white placeholder-slate-400 focus:outline-none focus:border-blue-500"
              />
            </div>

            <div>
              <label className="block text-sm font-medium text-slate-300 mb-1">
                Fix Code
              </label>
              <textarea
                value={fixCode}
                onChange={(e) => setFixCode(e.target.value)}
                rows={8}
                placeholder="// Code that fixes the error..."
                className="w-full px-4 py-2 bg-slate-900 border border-slate-600 rounded-lg text-emerald-300 font-mono text-sm placeholder-slate-500 focus:outline-none focus:border-blue-500"
              />
            </div>

            <div>
              <label className="block text-sm font-medium text-slate-300 mb-1">
                Prevention Tips (one per line)
              </label>
              <textarea
                value={preventionTips}
                onChange={(e) => setPreventionTips(e.target.value)}
                rows={3}
                placeholder="Always null-check before accessing&#10;Use null-conditional operators&#10;Enable nullable reference types"
                className="w-full px-4 py-2 bg-slate-700 border border-slate-600 rounded-lg text-white placeholder-slate-400 focus:outline-none focus:border-blue-500"
              />
            </div>
          </div>
        )}

        {/* Actions */}
        <div className="flex gap-4">
          <button
            type="submit"
            disabled={loading}
            className="px-6 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 transition-colors disabled:opacity-50"
          >
            {loading ? 'Creating...' : `Create ${isPattern ? 'Pattern' : 'Error Fix'}`}
          </button>
          <button
            type="button"
            onClick={() => navigate('/knowledge')}
            className="px-6 py-2 bg-slate-600 text-white rounded-lg hover:bg-slate-500 transition-colors"
          >
            Cancel
          </button>
        </div>
      </form>
    </div>
  );
}
