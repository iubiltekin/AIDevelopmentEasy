import { useEffect, useState } from 'react';
import { useParams, Link } from 'react-router-dom';
import { ArrowLeft, RefreshCw, FolderCode, FileCode, Layers, Copy, Check } from 'lucide-react';
import { codebasesApi } from '../services/api';
import { CodebaseDto, ProjectSummaryDto, CodebaseStatus, getCodebaseStatusLabel, getCodebaseStatusColor } from '../types';

export function CodebaseDetail() {
  const { id } = useParams<{ id: string }>();
  const [codebase, setCodebase] = useState<CodebaseDto | null>(null);
  const [projects, setProjects] = useState<ProjectSummaryDto[]>([]);
  const [context, setContext] = useState<string>('');
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [activeTab, setActiveTab] = useState<'overview' | 'projects' | 'context'>('overview');
  const [copied, setCopied] = useState(false);

  useEffect(() => {
    const load = async () => {
      if (!id) return;

      try {
        setLoading(true);
        const [cb, projs] = await Promise.all([
          codebasesApi.getById(id),
          codebasesApi.getProjects(id).catch(() => [])
        ]);

        setCodebase(cb);
        setProjects(projs);

        if (cb.status === CodebaseStatus.Ready) {
          const ctx = await codebasesApi.getContext(id).catch(() => '');
          setContext(ctx);
        }

        setError(null);
      } catch (err) {
        setError(err instanceof Error ? err.message : 'Failed to load codebase');
      } finally {
        setLoading(false);
      }
    };

    load();
  }, [id]);

  const handleCopyContext = async () => {
    await navigator.clipboard.writeText(context);
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

  if (!codebase) {
    return (
      <div className="flex flex-col items-center justify-center h-full">
        <div className="text-5xl mb-4">🔍</div>
        <h2 className="text-xl font-semibold text-white mb-2">Codebase Not Found</h2>
        <Link to="/codebases" className="text-blue-400 hover:underline">Back to Codebases</Link>
      </div>
    );
  }

  return (
    <div className="p-8">
      {/* Header */}
      <div className="flex items-center gap-4 mb-8">
        <Link
          to="/codebases"
          className="p-2 text-slate-400 hover:text-white hover:bg-slate-700 rounded-lg transition-colors"
        >
          <ArrowLeft className="w-5 h-5" />
        </Link>
        <div className="flex items-center gap-3">
          <FolderCode className="w-8 h-8 text-blue-400" />
          <div>
            <div className="flex items-center gap-3">
              <h1 className="text-2xl font-bold text-white">{codebase.name}</h1>
              <span className={`px-3 py-1 rounded-full text-xs font-medium ${getCodebaseStatusColor(codebase.status)} text-white`}>
                {getCodebaseStatusLabel(codebase.status)}
              </span>
            </div>
            <p className="text-slate-400 font-mono text-sm">{codebase.path}</p>
          </div>
        </div>
      </div>

      {error && (
        <div className="mb-6 p-4 bg-red-500/10 border border-red-500/30 rounded-xl text-red-400">
          {error}
        </div>
      )}

      {/* Summary Cards */}
      {codebase.summary && (
        <div className="grid grid-cols-2 md:grid-cols-5 gap-4 mb-8">
          <div className="bg-slate-800/50 backdrop-blur border border-slate-700 rounded-xl p-4 text-center">
            <div className="text-3xl font-bold text-white">{codebase.summary.totalSolutions}</div>
            <div className="text-sm text-slate-400">Solutions</div>
          </div>
          <div className="bg-slate-800/50 backdrop-blur border border-slate-700 rounded-xl p-4 text-center">
            <div className="text-3xl font-bold text-white">{codebase.summary.totalProjects}</div>
            <div className="text-sm text-slate-400">Projects</div>
          </div>
          <div className="bg-slate-800/50 backdrop-blur border border-slate-700 rounded-xl p-4 text-center">
            <div className="text-3xl font-bold text-blue-400">{codebase.summary.totalClasses}</div>
            <div className="text-sm text-slate-400">Classes</div>
          </div>
          <div className="bg-slate-800/50 backdrop-blur border border-slate-700 rounded-xl p-4 text-center">
            <div className="text-3xl font-bold text-emerald-400">{codebase.summary.totalInterfaces}</div>
            <div className="text-sm text-slate-400">Interfaces</div>
          </div>
          <div className="bg-slate-800/50 backdrop-blur border border-slate-700 rounded-xl p-4 text-center">
            <div className="text-lg font-bold text-amber-400 truncate">{codebase.summary.primaryFramework}</div>
            <div className="text-sm text-slate-400">Framework</div>
          </div>
        </div>
      )}

      {/* Tabs */}
      <div className="flex gap-2 mb-6">
        {['overview', 'projects', 'context'].map(tab => (
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
        {activeTab === 'overview' && codebase.summary && (
          <div className="space-y-6">
            <div>
              <h3 className="text-lg font-semibold text-white mb-4 flex items-center gap-2">
                <Layers className="w-5 h-5 text-blue-400" />
                Detected Patterns
              </h3>
              <div className="flex flex-wrap gap-2">
                {codebase.summary.detectedPatterns.map((pattern, i) => (
                  <span key={i} className="px-3 py-1 bg-slate-700 rounded-lg text-sm text-slate-300">
                    {pattern}
                  </span>
                ))}
                {codebase.summary.detectedPatterns.length === 0 && (
                  <span className="text-slate-500">No patterns detected</span>
                )}
              </div>
            </div>

            <div>
              <h3 className="text-lg font-semibold text-white mb-4">Key Namespaces</h3>
              <div className="flex flex-wrap gap-2">
                {codebase.summary.keyNamespaces.map((ns, i) => (
                  <span key={i} className="px-3 py-1 bg-slate-900 rounded-lg text-sm text-slate-300 font-mono">
                    {ns}
                  </span>
                ))}
                {codebase.summary.keyNamespaces.length === 0 && (
                  <span className="text-slate-500">No namespaces found</span>
                )}
              </div>
            </div>

            <div>
              <h3 className="text-lg font-semibold text-white mb-4">Timestamps</h3>
              <dl className="grid grid-cols-2 gap-4">
                <div>
                  <dt className="text-sm text-slate-400">Created</dt>
                  <dd className="text-white">{new Date(codebase.createdAt).toLocaleString()}</dd>
                </div>
                {codebase.analyzedAt && (
                  <div>
                    <dt className="text-sm text-slate-400">Last Analyzed</dt>
                    <dd className="text-white">{new Date(codebase.analyzedAt).toLocaleString()}</dd>
                  </div>
                )}
              </dl>
            </div>
          </div>
        )}

        {activeTab === 'projects' && (
          <div>
            <h3 className="text-lg font-semibold text-white mb-4">
              Projects ({projects.length})
            </h3>
            {projects.length === 0 ? (
              <div className="text-center py-8 text-slate-400">
                No projects found. Make sure analysis is complete.
              </div>
            ) : (
              <div className="space-y-4">
                {projects.map((project, index) => (
                  <div
                    key={index}
                    className="p-4 bg-slate-900 rounded-lg border border-slate-700"
                  >
                    <div className="flex items-start justify-between mb-3">
                      <div className="flex items-center gap-3">
                        <FileCode className="w-5 h-5 text-blue-400" />
                        <div>
                          <div className="font-medium text-white">{project.name}</div>
                          <div className="text-sm text-slate-400 font-mono">{project.relativePath}</div>
                        </div>
                      </div>
                      <div className="flex gap-2">
                        <span className="px-2 py-1 bg-slate-700 rounded text-xs text-slate-300">
                          {project.targetFramework}
                        </span>
                        <span className="px-2 py-1 bg-slate-700 rounded text-xs text-slate-300">
                          {project.outputType}
                        </span>
                        {project.isTestProject && (
                          <span className="px-2 py-1 bg-amber-600 rounded text-xs text-white">
                            Test
                          </span>
                        )}
                      </div>
                    </div>

                    <div className="grid grid-cols-4 gap-4 mb-3">
                      <div className="text-center p-2 bg-slate-800 rounded">
                        <div className="text-xl font-bold text-white">{project.classCount}</div>
                        <div className="text-xs text-slate-400">Classes</div>
                      </div>
                      <div className="text-center p-2 bg-slate-800 rounded">
                        <div className="text-xl font-bold text-white">{project.interfaceCount}</div>
                        <div className="text-xs text-slate-400">Interfaces</div>
                      </div>
                      <div className="col-span-2 p-2 bg-slate-800 rounded">
                        <div className="text-xs text-slate-400 mb-1">References</div>
                        <div className="text-sm text-slate-300">
                          {project.projectReferences.length > 0
                            ? project.projectReferences.join(', ')
                            : 'None'}
                        </div>
                      </div>
                    </div>

                    {project.detectedPatterns.length > 0 && (
                      <div className="flex flex-wrap gap-1">
                        {project.detectedPatterns.map((pattern, i) => (
                          <span key={i} className="px-2 py-0.5 bg-blue-600/20 text-blue-400 rounded text-xs">
                            {pattern}
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

        {activeTab === 'context' && (
          <div>
            <div className="flex items-center justify-between mb-4">
              <h3 className="text-lg font-semibold text-white">
                Context for Prompts
              </h3>
              <button
                onClick={handleCopyContext}
                className="flex items-center gap-2 px-3 py-1 bg-slate-700 hover:bg-slate-600 text-white rounded transition-colors"
              >
                {copied ? <Check className="w-4 h-4" /> : <Copy className="w-4 h-4" />}
                {copied ? 'Copied!' : 'Copy'}
              </button>
            </div>
            <p className="text-sm text-slate-400 mb-4">
              This context string can be used in prompts to help the AI understand your codebase structure.
            </p>
            <pre className="bg-slate-900 p-4 rounded-lg overflow-x-auto text-sm text-slate-300 whitespace-pre-wrap font-mono">
              {context || 'Context not available. Make sure analysis is complete.'}
            </pre>
          </div>
        )}
      </div>
    </div>
  );
}
