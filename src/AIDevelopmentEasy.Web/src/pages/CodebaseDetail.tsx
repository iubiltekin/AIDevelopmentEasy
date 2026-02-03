import { useEffect, useState } from 'react';
import { useParams, Link } from 'react-router-dom';
import { ArrowLeft, RefreshCw, FolderCode, FileCode, Layers, Copy, Check, Zap, Database, Server, AlertCircle } from 'lucide-react';
import { codebasesApi } from '../services/api';
import {
  CodebaseDto,
  ProjectSummaryDto,
  CodebaseStatus,
  getCodebaseStatusLabel,
  getCodebaseStatusColor,
  RequirementContextDto,
  PipelineContextDto
} from '../types';

type TabType = 'overview' | 'projects' | 'requirement-context' | 'pipeline-context';

export function CodebaseDetail() {
  const { id } = useParams<{ id: string }>();
  const [codebase, setCodebase] = useState<CodebaseDto | null>(null);
  const [projects, setProjects] = useState<ProjectSummaryDto[]>([]);
  const [requirementContext, setRequirementContext] = useState<RequirementContextDto | null>(null);
  const [pipelineContext, setPipelineContext] = useState<PipelineContextDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [activeTab, setActiveTab] = useState<TabType>('overview');
  const [copiedReq, setCopiedReq] = useState(false);
  const [copiedPipe, setCopiedPipe] = useState(false);

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
          const [reqCtx, pipeCtx] = await Promise.all([
            codebasesApi.getRequirementContext(id).catch(() => null),
            codebasesApi.getPipelineContext(id).catch(() => null)
          ]);
          setRequirementContext(reqCtx);
          setPipelineContext(pipeCtx);
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

  // Poll when status is Analyzing so we refresh to Ready and load contexts
  useEffect(() => {
    if (!id || !codebase || codebase.status !== CodebaseStatus.Analyzing) return;

    const interval = setInterval(async () => {
      try {
        const cb = await codebasesApi.getById(id);
        setCodebase(cb);
        if (cb.status === CodebaseStatus.Ready) {
          const [reqCtx, pipeCtx] = await Promise.all([
            codebasesApi.getRequirementContext(id).catch(() => null),
            codebasesApi.getPipelineContext(id).catch(() => null)
          ]);
          setRequirementContext(reqCtx);
          setPipelineContext(pipeCtx);
          setProjects(await codebasesApi.getProjects(id).catch(() => []));
        }
      } catch { /* ignore */ }
    }, 3000);

    return () => clearInterval(interval);
  }, [id, codebase?.status]);

  const handleCopy = async (text: string, type: 'req' | 'pipe') => {
    await navigator.clipboard.writeText(text);
    if (type === 'req') {
      setCopiedReq(true);
      setTimeout(() => setCopiedReq(false), 2000);
    } else {
      setCopiedPipe(true);
      setTimeout(() => setCopiedPipe(false), 2000);
    }
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

  const tabs: { key: TabType; label: string; icon?: React.ReactNode }[] = [
    { key: 'overview', label: 'Overview', icon: <Layers className="w-4 h-4" /> },
    { key: 'projects', label: 'Projects', icon: <FileCode className="w-4 h-4" /> },
    { key: 'requirement-context', label: 'Requirement Context', icon: <Zap className="w-4 h-4" /> },
    { key: 'pipeline-context', label: 'Pipeline Context', icon: <Database className="w-4 h-4" /> },
  ];

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
        <div className="mb-6 p-4 bg-red-500/10 border border-red-500/30 rounded-xl text-red-400 flex items-center gap-2">
          <AlertCircle className="w-5 h-5" />
          {error}
        </div>
      )}

      {/* Summary Cards */}
      {codebase.summary && (
        <div className="grid grid-cols-2 md:grid-cols-6 gap-4 mb-8">
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
            <div className="text-lg font-bold text-amber-400">~{requirementContext?.tokenEstimate || 0}</div>
            <div className="text-sm text-slate-400">Req Tokens</div>
          </div>
          <div className="bg-slate-800/50 backdrop-blur border border-slate-700 rounded-xl p-4 text-center">
            <div className="text-lg font-bold text-purple-400">~{pipelineContext?.tokenEstimate || 0}</div>
            <div className="text-sm text-slate-400">Pipeline Tokens</div>
          </div>
        </div>
      )}

      {/* Tabs */}
      <div className="flex gap-2 mb-6 flex-wrap">
        {tabs.map(tab => (
          <button
            key={tab.key}
            onClick={() => setActiveTab(tab.key)}
            className={`flex items-center gap-2 px-4 py-2 rounded-lg font-medium transition-colors ${activeTab === tab.key
              ? 'bg-blue-600 text-white'
              : 'bg-slate-800 text-slate-400 hover:text-white'
              }`}
          >
            {tab.icon}
            {tab.label}
          </button>
        ))}
      </div>

      {/* Tab Content */}
      <div className="bg-slate-800/50 backdrop-blur border border-slate-700 rounded-xl p-6">
        {activeTab === 'overview' && codebase.summary && (
          <div className="space-y-6">
            {codebase.summary.languages && codebase.summary.languages.length > 0 && (
              <div>
                <h3 className="text-lg font-semibold text-white mb-4 flex items-center gap-2">
                  <FileCode className="w-5 h-5 text-emerald-400" />
                  Languages
                </h3>
                <div className="flex flex-wrap gap-2">
                  {codebase.summary.languages.map((lang, i) => (
                    <span key={i} className="px-3 py-1 bg-emerald-900/40 text-emerald-300 rounded-lg text-sm font-medium capitalize">
                      {lang}
                    </span>
                  ))}
                </div>
              </div>
            )}
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

            {/* Technologies */}
            {requirementContext?.technologies && requirementContext.technologies.length > 0 && (
              <div>
                <h3 className="text-lg font-semibold text-white mb-4">Technologies</h3>
                <div className="flex flex-wrap gap-2">
                  {requirementContext.technologies.map((tech, i) => (
                    <span key={i} className="px-3 py-1 bg-purple-900/50 text-purple-300 rounded-lg text-sm">
                      {tech}
                    </span>
                  ))}
                </div>
              </div>
            )}

            {/* Architecture */}
            {requirementContext?.architecture && requirementContext.architecture.length > 0 && (
              <div>
                <h3 className="text-lg font-semibold text-white mb-4">Architecture</h3>
                <div className="flex flex-wrap gap-2">
                  {requirementContext.architecture.map((layer, i) => (
                    <span key={i} className="px-3 py-1 bg-emerald-900/50 text-emerald-300 rounded-lg text-sm">
                      {layer}
                    </span>
                  ))}
                </div>
              </div>
            )}

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
                      <div className="flex flex-wrap gap-2">
                        {project.languageId && (
                          <span className="px-2 py-1 bg-emerald-900/50 text-emerald-300 rounded text-xs font-medium capitalize">
                            {project.languageId}
                          </span>
                        )}
                        {project.role && (
                          <span className="px-2 py-1 bg-blue-900/50 text-blue-300 rounded text-xs">
                            {project.role}
                          </span>
                        )}
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

        {activeTab === 'requirement-context' && (
          <div>
            <div className="flex items-center justify-between mb-4">
              <div>
                <h3 className="text-lg font-semibold text-white flex items-center gap-2">
                  <Zap className="w-5 h-5 text-amber-400" />
                  Requirement Context
                  <span className="text-sm font-normal text-slate-400 ml-2">
                    (Lightweight - ~{requirementContext?.tokenEstimate || 0} tokens)
                  </span>
                </h3>
                <p className="text-sm text-slate-400 mt-1">
                  Used by Requirements Wizard for understanding project structure without detailed code
                </p>
              </div>
              {requirementContext?.summaryText && (
                <button
                  onClick={() => handleCopy(requirementContext.summaryText, 'req')}
                  className="flex items-center gap-2 px-3 py-1 bg-slate-700 hover:bg-slate-600 text-white rounded transition-colors"
                >
                  {copiedReq ? <Check className="w-4 h-4" /> : <Copy className="w-4 h-4" />}
                  {copiedReq ? 'Copied!' : 'Copy'}
                </button>
              )}
            </div>

            {requirementContext ? (
              <div className="space-y-6">
                {/* Projects Summary */}
                {requirementContext.projects.length > 0 && (
                  <div>
                    <h4 className="text-md font-medium text-white mb-3">Projects</h4>
                    <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
                      {requirementContext.projects.map((proj, i) => (
                        <div key={i} className="p-3 bg-slate-900 rounded-lg border border-slate-700">
                          <div className="flex items-center gap-2 mb-1">
                            <Server className="w-4 h-4 text-blue-400" />
                            <span className="font-medium text-white">{proj.name}</span>
                            <span className="px-2 py-0.5 bg-slate-700 text-slate-300 rounded text-xs">{proj.type}</span>
                          </div>
                          <p className="text-sm text-slate-400">{proj.purpose}</p>
                          {proj.keyNamespaces.length > 0 && (
                            <div className="mt-2 flex flex-wrap gap-1">
                              {proj.keyNamespaces.map((ns, j) => (
                                <span key={j} className="text-xs font-mono text-slate-500">{ns}</span>
                              ))}
                            </div>
                          )}
                        </div>
                      ))}
                    </div>
                  </div>
                )}

                {/* Extension Points */}
                {requirementContext.extensionPoints.length > 0 && (
                  <div>
                    <h4 className="text-md font-medium text-white mb-3">Extension Points (Where to add new code)</h4>
                    <div className="space-y-2">
                      {requirementContext.extensionPoints.map((ep, i) => (
                        <div key={i} className="flex items-center gap-3 p-2 bg-slate-900 rounded">
                          <span className="px-2 py-0.5 bg-emerald-600/20 text-emerald-400 rounded text-sm">{ep.layer}</span>
                          <span className="text-white">{ep.project}</span>
                          <span className="text-slate-500">→</span>
                          <span className="font-mono text-sm text-slate-400">{ep.namespace}</span>
                        </div>
                      ))}
                    </div>
                  </div>
                )}

                {/* Raw Text */}
                <div>
                  <h4 className="text-md font-medium text-white mb-3">Raw Context (for LLM)</h4>
                  <pre className="bg-slate-900 p-4 rounded-lg overflow-x-auto text-sm text-slate-300 whitespace-pre-wrap font-mono max-h-96 overflow-y-auto">
                    {requirementContext.summaryText || 'Context not available'}
                  </pre>
                </div>
              </div>
            ) : (
              <div className="text-center py-8 text-slate-400">
                Requirement context not available. Make sure analysis is complete.
              </div>
            )}
          </div>
        )}

        {activeTab === 'pipeline-context' && (
          <div>
            <div className="flex items-center justify-between mb-4">
              <div>
                <h3 className="text-lg font-semibold text-white flex items-center gap-2">
                  <Database className="w-5 h-5 text-purple-400" />
                  Pipeline Context
                  <span className="text-sm font-normal text-slate-400 ml-2">
                    (Detailed - ~{pipelineContext?.tokenEstimate || 0} tokens)
                  </span>
                </h3>
                <p className="text-sm text-slate-400 mt-1">
                  Used by Pipeline for code generation with detailed class/interface information
                </p>
              </div>
              {pipelineContext?.fullContextText && (
                <button
                  onClick={() => handleCopy(pipelineContext.fullContextText, 'pipe')}
                  className="flex items-center gap-2 px-3 py-1 bg-slate-700 hover:bg-slate-600 text-white rounded transition-colors"
                >
                  {copiedPipe ? <Check className="w-4 h-4" /> : <Copy className="w-4 h-4" />}
                  {copiedPipe ? 'Copied!' : 'Copy'}
                </button>
              )}
            </div>

            {pipelineContext ? (
              <div className="space-y-6">
                {/* Stats */}
                <div className="grid grid-cols-3 gap-4">
                  <div className="text-center p-4 bg-slate-900 rounded-lg">
                    <div className="text-2xl font-bold text-white">{pipelineContext.projectCount}</div>
                    <div className="text-sm text-slate-400">Projects</div>
                  </div>
                  <div className="text-center p-4 bg-slate-900 rounded-lg">
                    <div className="text-2xl font-bold text-blue-400">{pipelineContext.classCount}</div>
                    <div className="text-sm text-slate-400">Classes</div>
                  </div>
                  <div className="text-center p-4 bg-slate-900 rounded-lg">
                    <div className="text-2xl font-bold text-emerald-400">{pipelineContext.interfaceCount}</div>
                    <div className="text-sm text-slate-400">Interfaces</div>
                  </div>
                </div>

                {/* Token Comparison */}
                <div className="p-4 bg-amber-900/20 border border-amber-700/50 rounded-lg">
                  <div className="flex items-center gap-2 mb-2">
                    <AlertCircle className="w-5 h-5 text-amber-400" />
                    <span className="font-medium text-amber-300">Token Optimization</span>
                  </div>
                  <p className="text-sm text-slate-300">
                    Pipeline context is <strong className="text-amber-400">
                      {pipelineContext.tokenEstimate > 0 && requirementContext?.tokenEstimate
                        ? Math.round(pipelineContext.tokenEstimate / requirementContext.tokenEstimate)
                        : '?'}×
                    </strong> larger than requirement context.
                    Requirements Wizard uses the lightweight version to save costs.
                  </p>
                </div>

                {/* Raw Text */}
                <div>
                  <h4 className="text-md font-medium text-white mb-3">Raw Context (for LLM)</h4>
                  <pre className="bg-slate-900 p-4 rounded-lg overflow-x-auto text-sm text-slate-300 whitespace-pre-wrap font-mono max-h-96 overflow-y-auto">
                    {pipelineContext.fullContextText || 'Context not available'}
                  </pre>
                </div>
              </div>
            ) : (
              <div className="text-center py-8 text-slate-400">
                Pipeline context not available. Make sure analysis is complete.
              </div>
            )}
          </div>
        )}
      </div>
    </div>
  );
}
