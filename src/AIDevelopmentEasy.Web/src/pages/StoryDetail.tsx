import { useEffect, useState } from 'react';
import { useParams, Link, useNavigate } from 'react-router-dom';
import { ArrowLeft, Play, RefreshCw, FileCode, Eye, Trash2, RotateCcw, History, X, Edit2, Save, Target } from 'lucide-react';
import { StoryDto, StoryStatus, TaskStatus, TaskType, PipelineStatusDto, ChangeType, getChangeTypeLabel, getChangeTypeColor, ProjectSummaryDto, ClassInfoDto, MethodInfoDto } from '../types';
import { storiesApi, pipelineApi, codebasesApi } from '../services/api';
import { StatusBadge } from '../components/StatusBadge';
import { PipelineHistorySummary } from '../components/PipelineHistorySummary';
import { SearchableSelect } from '../components';

export function StoryDetail() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [story, setStory] = useState<StoryDto | null>(null);
  const [content, setContent] = useState<string>('');
  const [editedContent, setEditedContent] = useState<string>('');
  const [editedName, setEditedName] = useState<string>('');
  const [isEditing, setIsEditing] = useState(false);
  const [isSaving, setIsSaving] = useState(false);
  const [output, setOutput] = useState<Record<string, string>>({});
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [activeTab, setActiveTab] = useState<'overview' | 'content' | 'tasks' | 'output'>('overview');
  const [showHistory, setShowHistory] = useState(false);
  const [historyData, setHistoryData] = useState<PipelineStatusDto | null>(null);
  const [historyLoading, setHistoryLoading] = useState(false);

  // Target Info State
  const [projects, setProjects] = useState<ProjectSummaryDto[]>([]);
  const [classes, setClasses] = useState<ClassInfoDto[]>([]);
  const [methods, setMethods] = useState<MethodInfoDto[]>([]);
  const [targetProject, setTargetProject] = useState<string>('');
  const [targetFile, setTargetFile] = useState<string>('');
  const [targetClass, setTargetClass] = useState<string>('');
  const [targetMethod, setTargetMethod] = useState<string>('');
  const [changeType, setChangeType] = useState<ChangeType>(ChangeType.Create);
  const [targetSaving, setTargetSaving] = useState(false);
  const [targetDirty, setTargetDirty] = useState(false);

  // Test Target State
  const [testProjects, setTestProjects] = useState<ProjectSummaryDto[]>([]);
  const [testClasses, setTestClasses] = useState<ClassInfoDto[]>([]);
  const [targetTestProject, setTargetTestProject] = useState<string>('');
  const [targetTestFile, setTargetTestFile] = useState<string>('');
  const [targetTestClass, setTargetTestClass] = useState<string>('');

  // Codebase display (name + path for header/overview)
  const [codebase, setCodebase] = useState<{ name: string; path: string } | null>(null);

  useEffect(() => {
    const load = async () => {
      if (!id) return;

      try {
        setLoading(true);
        const [req, reqContent] = await Promise.all([
          storiesApi.getById(id),
          storiesApi.getContent(id).catch(() => '')
        ]);

        setStory(req);
        setContent(reqContent);

        // Load target info from story
        if (req.targetProject) setTargetProject(req.targetProject);
        if (req.targetFile) setTargetFile(req.targetFile);
        if (req.targetClass) setTargetClass(req.targetClass);
        if (req.targetMethod) setTargetMethod(req.targetMethod);
        if (req.changeType !== undefined) setChangeType(req.changeType);
        // Load test target info
        if (req.targetTestProject) setTargetTestProject(req.targetTestProject);
        if (req.targetTestFile) setTargetTestFile(req.targetTestFile);
        if (req.targetTestClass) setTargetTestClass(req.targetTestClass);

        // Load codebase and projects if codebase exists
        if (req.codebaseId) {
          const [cb, projs] = await Promise.all([
            codebasesApi.getById(req.codebaseId).catch(() => null),
            codebasesApi.getProjects(req.codebaseId).catch(() => [])
          ]);
          if (cb) setCodebase({ name: cb.name, path: cb.path });
          setProjects(projs);
          // Separate test projects
          setTestProjects(projs.filter(p => p.isTestProject));

          // Load classes if target project exists
          if (req.targetProject) {
            const cls = await codebasesApi.getProjectClasses(req.codebaseId, req.targetProject).catch(() => []);
            setClasses(cls);

            // Load methods if target class exists
            if (req.targetClass) {
              const meths = await codebasesApi.getClassMethods(req.codebaseId, req.targetProject, req.targetClass).catch(() => []);
              setMethods(meths);
            }
          }

          // Load test classes if test project exists
          if (req.targetTestProject) {
            const testCls = await codebasesApi.getProjectClasses(req.codebaseId, req.targetTestProject).catch(() => []);
            setTestClasses(testCls);
          }
        } else {
          setCodebase(null);
        }

        if (req.status === StoryStatus.Completed) {
          const out = await pipelineApi.getOutput(id).catch(() => ({}));
          setOutput(out);
        }

        setError(null);
      } catch (err) {
        setError(err instanceof Error ? err.message : 'Failed to load story');
      } finally {
        setLoading(false);
      }
    };

    load();
  }, [id]);

  // Load classes when project changes
  const handleProjectChange = async (projectName: string) => {
    setTargetProject(projectName);
    setTargetFile('');
    setTargetClass('');
    setTargetMethod('');
    setClasses([]);
    setMethods([]);
    setTargetDirty(true);

    if (projectName && story?.codebaseId) {
      const cls = await codebasesApi.getProjectClasses(story.codebaseId, projectName).catch(() => []);
      setClasses(cls);
    }
  };

  // Load methods when class changes
  const handleClassChange = async (className: string) => {
    setTargetClass(className);
    setTargetMethod('');
    setMethods([]);
    setTargetDirty(true);

    // Also set the file path from the class
    const selectedClass = classes.find(c => c.name === className);
    if (selectedClass) {
      setTargetFile(selectedClass.filePath);
    }

    if (className && targetProject && story?.codebaseId) {
      const meths = await codebasesApi.getClassMethods(story.codebaseId, targetProject, className).catch(() => []);
      setMethods(meths);
    }
  };

  const handleMethodChange = (methodName: string) => {
    setTargetMethod(methodName);
    setTargetDirty(true);
  };

  const handleChangeTypeChange = (type: ChangeType) => {
    setChangeType(type);
    setTargetDirty(true);
  };

  // Test target handlers
  const handleTestProjectChange = async (projectName: string) => {
    setTargetTestProject(projectName);
    setTargetTestFile('');
    setTargetTestClass('');
    setTestClasses([]);
    setTargetDirty(true);

    if (projectName && story?.codebaseId) {
      const cls = await codebasesApi.getProjectClasses(story.codebaseId, projectName).catch(() => []);
      setTestClasses(cls);
    }
  };

  const handleTestClassChange = (className: string) => {
    setTargetTestClass(className);
    setTargetDirty(true);

    // Auto-fill test file path from class
    const selectedClass = testClasses.find(c => c.name === className);
    if (selectedClass) {
      setTargetTestFile(selectedClass.filePath);
    }
  };

  // Language-based labels for target dropdowns (multi-language support)
  const selectedProject = projects.find(p => p.name === targetProject);
  const langId = (selectedProject?.languageId ?? 'csharp').toLowerCase();
  const targetLabels = {
    project: langId === 'go' ? 'Module' : langId === 'rust' ? 'Crate' : langId === 'python' ? 'Package' : langId === 'typescript' ? 'Project' : 'Project',
    type: langId === 'go' ? 'Type' : langId === 'rust' ? 'Type' : langId === 'python' ? 'Class' : langId === 'typescript' ? 'Component' : 'Class',
    method: langId === 'go' ? 'Function' : langId === 'rust' ? 'Function' : langId === 'python' ? 'Function' : langId === 'typescript' ? '—' : 'Method',
    testProject: langId === 'go' ? 'Test Module' : langId === 'rust' ? 'Test Crate' : 'Test Project',
    testType: langId === 'go' ? 'Test Type' : langId === 'rust' ? 'Test Type' : 'Test Class'
  };
  const showMethodField = methods.length > 0 || langId === 'csharp';

  const handleSaveTarget = async () => {
    if (!id) return;

    setTargetSaving(true);
    try {
      await storiesApi.updateTarget(id, {
        targetProject: targetProject || undefined,
        targetFile: targetFile || undefined,
        targetClass: targetClass || undefined,
        targetMethod: targetMethod || undefined,
        changeType,
        // Test target
        targetTestProject: targetTestProject || undefined,
        targetTestFile: targetTestFile || undefined,
        targetTestClass: targetTestClass || undefined
      });
      setTargetDirty(false);
      // Update local story state
      if (story) {
        setStory({
          ...story,
          targetProject,
          targetFile,
          targetClass,
          targetMethod,
          changeType,
          targetTestProject,
          targetTestFile,
          targetTestClass
        });
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to save target info');
    } finally {
      setTargetSaving(false);
    }
  };

  const handleStart = async () => {
    if (!id) return;

    // Save target info if dirty before starting
    if (targetDirty) {
      await handleSaveTarget();
    }

    try {
      await pipelineApi.start(id);
      navigate(`/pipeline/${id}`);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to start pipeline');
    }
  };

  const handleReset = async () => {
    if (!id) return;
    if (!confirm('Reset this story? All tasks and output will be cleared.')) return;

    try {
      await storiesApi.reset(id);
      window.location.reload();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to reset story');
    }
  };

  const handleDelete = async () => {
    if (!id) return;
    if (!confirm('Delete this story? This cannot be undone.')) return;

    try {
      await storiesApi.delete(id);
      navigate('/');
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to delete story');
    }
  };

  const handleShowHistory = async () => {
    if (!id) return;

    setHistoryLoading(true);
    setShowHistory(true);

    try {
      const history = await pipelineApi.getHistory(id);
      setHistoryData(history);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load pipeline history');
    } finally {
      setHistoryLoading(false);
    }
  };

  const handleEditContent = () => {
    setEditedContent(content);
    setEditedName(story?.name || '');
    setIsEditing(true);
    setError(null);
  };

  const handleCancelEdit = () => {
    setIsEditing(false);
    setEditedContent('');
    setEditedName('');
    setError(null);
  };

  const handleSaveContent = async () => {
    if (!id || !editedContent.trim()) {
      setError('Content cannot be empty');
      return;
    }

    if (!editedName.trim()) {
      setError('Name cannot be empty');
      return;
    }

    setIsSaving(true);
    try {
      await storiesApi.updateContent(id, editedContent, editedName);
      setContent(editedContent);
      // Update local story state with new name
      if (story) {
        setStory({ ...story, name: editedName });
      }
      setIsEditing(false);
      setEditedContent('');
      setEditedName('');
      setError(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to save content');
    } finally {
      setIsSaving(false);
    }
  };

  const canEditContent = story?.status === StoryStatus.NotStarted;
  const canEditTarget = story?.status === StoryStatus.NotStarted || story?.status === StoryStatus.Planned;

  const getTaskStatusIcon = (status: TaskStatus) => {
    switch (status) {
      case TaskStatus.Completed:
        return '✅';
      case TaskStatus.InProgress:
        return '🔄';
      case TaskStatus.Failed:
        return '❌';
      default:
        return '⬜';
    }
  };

  if (loading) {
    return (
      <div className="flex items-center justify-center h-full">
        <RefreshCw className="w-8 h-8 text-blue-400 animate-spin" />
      </div>
    );
  }

  if (!story) {
    return (
      <div className="flex flex-col items-center justify-center h-full">
        <div className="text-5xl mb-4">🔍</div>
        <h2 className="text-xl font-semibold text-white mb-2">Story Not Found</h2>
        <Link to="/" className="text-blue-400 hover:underline">Back to Dashboard</Link>
      </div>
    );
  }

  return (
    <div className="p-8">
      {/* Header */}
      <div className="flex items-center justify-between mb-8">
        <div className="flex items-center gap-4">
          <Link
            to="/"
            className="p-2 text-slate-400 hover:text-white hover:bg-slate-700 rounded-lg transition-colors"
          >
            <ArrowLeft className="w-5 h-5" />
          </Link>
          <div>
            <div className="flex items-center gap-3">
              <FileCode className="w-7 h-7 text-slate-400 flex-shrink-0" />
              <h1 className="text-2xl font-bold text-white">{story.name}</h1>
              <StatusBadge status={story.status} />
            </div>
            <p className="text-slate-400">
              {story.codebaseId ? (
                <>
                  {codebase ? (
                    <Link to={`/codebases/${story.codebaseId}`} className="text-emerald-400 hover:text-emerald-300">
                      {codebase.name}
                    </Link>
                  ) : (
                    <span className="text-slate-500">Codebase: {story.codebaseId}</span>
                  )}
                  {codebase?.path && (
                    <span className="text-slate-500 font-mono text-sm block mt-0.5">{codebase.path}</span>
                  )}
                </>
              ) : (
                'New project story'
              )}
            </p>
          </div>
        </div>
        <div className="flex gap-3">
          {story.status !== StoryStatus.InProgress &&
            story.status !== StoryStatus.Completed && (
              <button
                onClick={handleStart}
                className="flex items-center gap-2 px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white rounded-lg transition-colors"
              >
                <Play className="w-4 h-4" />
                Start Pipeline
              </button>
            )}
          {story.status === StoryStatus.InProgress && (
            <Link
              to={`/pipeline/${id}`}
              className="flex items-center gap-2 px-4 py-2 bg-amber-600 hover:bg-amber-700 text-white rounded-lg transition-colors"
            >
              <Eye className="w-4 h-4" />
              View Progress
            </Link>
          )}
          {story.status === StoryStatus.Completed && (
            <button
              onClick={handleShowHistory}
              className="flex items-center gap-2 px-4 py-2 bg-purple-600 hover:bg-purple-700 text-white rounded-lg transition-colors"
            >
              <History className="w-4 h-4" />
              Pipeline History
            </button>
          )}
          <button
            onClick={handleReset}
            className="p-2 text-slate-400 hover:text-white hover:bg-slate-700 rounded-lg transition-colors"
            title="Reset"
          >
            <RotateCcw className="w-5 h-5" />
          </button>
          <button
            onClick={handleDelete}
            className="p-2 text-slate-400 hover:text-red-400 hover:bg-slate-700 rounded-lg transition-colors"
            title="Delete"
          >
            <Trash2 className="w-5 h-5" />
          </button>
        </div>
      </div>

      {/* Error */}
      {error && (
        <div className="mb-6 p-4 bg-red-500/10 border border-red-500/30 rounded-xl text-red-400">
          {error}
        </div>
      )}

      {/* Target Info Panel - Show before pipeline starts */}
      {story.codebaseId && canEditTarget && (
        <div className="mb-6 bg-slate-800/50 backdrop-blur border border-slate-700 rounded-xl p-6">
          <div className="flex items-center justify-between mb-4">
            <div className="flex items-center gap-3">
              <div className="w-10 h-10 bg-amber-500/20 rounded-lg flex items-center justify-center">
                <Target className="w-5 h-5 text-amber-400" />
              </div>
              <div>
                <h3 className="text-lg font-semibold text-white">Target Information</h3>
                <p className="text-sm text-slate-400">Where to implement (optional). Options adapt to the codebase (e.g. Module/Type for Go, Project/Class for C#).</p>
              </div>
            </div>
            {targetDirty && (
              <button
                onClick={handleSaveTarget}
                disabled={targetSaving}
                className="flex items-center gap-2 px-3 py-1.5 bg-emerald-600 hover:bg-emerald-700 text-white text-sm rounded-lg transition-colors disabled:opacity-50"
              >
                {targetSaving ? <RefreshCw className="w-4 h-4 animate-spin" /> : <Save className="w-4 h-4" />}
                Save
              </button>
            )}
          </div>

          {/* Change Type – optional; only needed when targeting a specific file/class. Otherwise AI infers from story + codebase. */}
          <div className="mb-4">
            <label className="block text-sm text-slate-400 mb-1">
              Change Type <span className="text-slate-500 font-normal">(optional – for targeting a specific file/class below)</span>
            </label>
            {!(targetProject || targetClass || targetFile) && (
              <p className="text-xs text-slate-500 mb-2">
                When you don’t set a target, the AI will decide from your story and codebase whether to create, modify, or remove code.
              </p>
            )}
            <div className="flex gap-2">
              {[ChangeType.Create, ChangeType.Modify, ChangeType.Delete].map(type => (
                <button
                  key={type}
                  onClick={() => handleChangeTypeChange(type)}
                  className={`px-4 py-2 rounded-lg text-sm font-medium transition-colors ${changeType === type
                    ? `${getChangeTypeColor(type)} text-white`
                    : 'bg-slate-700 text-slate-400 hover:text-white'
                    }`}
                >
                  {getChangeTypeLabel(type)}
                </button>
              ))}
            </div>
          </div>

          {/* Development path (root path of selected project) */}
          {selectedProject?.rootPath && (
            <div className="mb-4 p-2 bg-slate-900 rounded-lg">
              <span className="text-sm text-slate-400">Development path: </span>
              <span className="text-sm text-white font-mono">{selectedProject.rootPath}</span>
            </div>
          )}

          {/* Code Target Row - all non-test projects (backend, frontend, any language) */}
          <div className="grid grid-cols-4 gap-4 mb-4">
            {/* Project/Module/Crate Dropdown - show language and role so Go + React both visible */}
            <div>
              <label className="block text-sm text-slate-400 mb-1">{targetLabels.project}</label>
              <SearchableSelect
                value={targetProject}
                onChange={handleProjectChange}
                options={projects.filter(p => !p.isTestProject).map(p => {
                  const roleLang = [p.role, p.languageId].filter(Boolean).join(' · ') || p.targetFramework || '';
                  const sublabel = [roleLang, p.rootPath, `${p.classCount} types`].filter(Boolean).join(' · ');
                  return { value: p.name, label: p.name, sublabel };
                })}
                placeholder={`Any ${targetLabels.project}`}
              />
            </div>

            {/* Class/Type/Component Dropdown */}
            <div>
              <label className="block text-sm text-slate-400 mb-1">{targetLabels.type}</label>
              <SearchableSelect
                value={targetClass}
                onChange={handleClassChange}
                options={classes.map(c => ({
                  value: c.name,
                  label: c.name,
                  sublabel: c.filePath
                }))}
                placeholder={`Any ${targetLabels.type}`}
                disabled={!targetProject}
              />
            </div>

            {/* Method/Function Dropdown - only when methods exist or C# */}
            {showMethodField ? (
              <div>
                <label className="block text-sm text-slate-400 mb-1">{targetLabels.method}</label>
                <SearchableSelect
                  value={targetMethod}
                  onChange={handleMethodChange}
                  options={methods.map(m => ({
                    value: m.name,
                    label: m.name,
                    sublabel: `${m.returnType}(${m.parameters.length} params)`
                  }))}
                  placeholder={`Any ${targetLabels.method}`}
                  disabled={!targetClass}
                />
              </div>
            ) : (
              <div />
            )}

            {/* File Path (auto-filled) */}
            <div>
              <label className="block text-sm text-slate-400 mb-1">File Path</label>
              <input
                type="text"
                value={targetFile}
                onChange={(e) => { setTargetFile(e.target.value); setTargetDirty(true); }}
                placeholder="Auto-filled from type"
                className="w-full px-3 py-2 bg-slate-700 border border-slate-600 rounded-lg text-white placeholder-slate-500 focus:border-blue-500 focus:outline-none"
              />
            </div>
          </div>

          {/* Test Target: dedicated test projects (C#) or optional "where to add tests" (Go, polyglot) */}
          {testProjects.length > 0 ? (
            <div className="grid grid-cols-4 gap-4">
              <div>
                <label className="block text-sm text-slate-400 mb-1">{targetLabels.testProject}</label>
                <SearchableSelect
                  value={targetTestProject}
                  onChange={handleTestProjectChange}
                  options={testProjects.map(p => ({
                    value: p.name,
                    label: p.name,
                    sublabel: `${p.classCount} test types`
                  }))}
                  placeholder={`Select ${targetLabels.testProject}`}
                />
              </div>
              <div>
                <label className="block text-sm text-slate-400 mb-1">{targetLabels.testType}</label>
                <SearchableSelect
                  value={targetTestClass}
                  onChange={handleTestClassChange}
                  options={testClasses.map(c => ({
                    value: c.name,
                    label: c.name,
                    sublabel: c.filePath
                  }))}
                  placeholder={`Select ${targetLabels.testType}`}
                  disabled={!targetTestProject}
                />
              </div>
              <div>
                <label className="block text-sm text-slate-400 mb-1">Test File</label>
                <input
                  type="text"
                  value={targetTestFile}
                  onChange={(e) => { setTargetTestFile(e.target.value); setTargetDirty(true); }}
                  placeholder="Auto-filled from type"
                  className="w-full px-3 py-2 bg-slate-700 border border-slate-600 rounded-lg text-white placeholder-slate-500 focus:border-blue-500 focus:outline-none"
                />
              </div>
              <div />
            </div>
          ) : projects.length > 0 ? (
            <div className="grid grid-cols-4 gap-4">
              <div>
                <label className="block text-sm text-slate-400 mb-1">Where to add tests (optional)</label>
                <SearchableSelect
                  value={targetTestProject}
                  onChange={handleTestProjectChange}
                  options={projects.filter(p => !p.isTestProject).map(p => {
                    const roleLang = [p.role, p.languageId].filter(Boolean).join(' · ') || '';
                    return { value: p.name, label: p.name, sublabel: [roleLang, p.rootPath].filter(Boolean).join(' · ') };
                  })}
                  placeholder="Same module or another package"
                />
              </div>
              <div />
              <div />
              <div />
            </div>
          ) : null}

          {/* Selected Target Summary */}
          {(targetProject || targetClass || targetMethod || targetTestClass) && (
            <div className="mt-4 p-3 bg-slate-900 rounded-lg space-y-2">
              {(targetProject || targetClass || targetMethod) && (
                <div>
                  <span className="text-sm text-slate-400">Code Target: </span>
                  <span className="text-sm text-white font-mono">
                    {targetProject || '*'}
                    {targetClass && ` → ${targetClass}`}
                    {targetMethod && `.${targetMethod}()`}
                  </span>
                  <span className={`ml-3 px-2 py-0.5 rounded text-xs ${getChangeTypeColor(changeType)} text-white`}>
                    {getChangeTypeLabel(changeType)}
                  </span>
                </div>
              )}
              {targetTestClass && (
                <div>
                  <span className="text-sm text-slate-400">Test Target: </span>
                  <span className="text-sm text-white font-mono">
                    {targetTestProject || '*'} → {targetTestClass}
                  </span>
                </div>
              )}
            </div>
          )}
        </div>
      )}

      {/* Tabs */}
      <div className="flex gap-2 mb-6">
        {['overview', 'content', 'tasks', 'output'].map(tab => (
          <button
            key={tab}
            onClick={() => setActiveTab(tab as typeof activeTab)}
            className={`px-4 py-2 rounded-lg font-medium transition-colors ${activeTab === tab
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
        {activeTab === 'overview' && (
          <div className="grid grid-cols-2 gap-6">
            <div>
              <h3 className="text-lg font-semibold text-white mb-4">Details</h3>
              <dl className="space-y-3">
                <div>
                  <dt className="text-sm text-slate-400">ID</dt>
                  <dd className="text-white font-mono">{story.id}</dd>
                </div>
                <div>
                  <dt className="text-sm text-slate-400">Codebase</dt>
                  <dd className="text-white">
                    {story.codebaseId ? (
                      <>
                        {codebase ? (
                          <Link to={`/codebases/${story.codebaseId}`} className="text-emerald-400 hover:text-emerald-300">
                            {codebase.name}
                          </Link>
                        ) : (
                          story.codebaseId
                        )}
                        {codebase?.path && (
                          <span className="block text-slate-500 font-mono text-sm mt-0.5">{codebase.path}</span>
                        )}
                      </>
                    ) : (
                      'None (new project)'
                    )}
                  </dd>
                </div>
                <div>
                  <dt className="text-sm text-slate-400">Status</dt>
                  <dd><StatusBadge status={story.status} /></dd>
                </div>
                <div>
                  <dt className="text-sm text-slate-400">Created</dt>
                  <dd className="text-white">
                    {new Date(story.createdAt).toLocaleString()}
                  </dd>
                </div>
                {story.lastProcessedAt && (
                  <div>
                    <dt className="text-sm text-slate-400">Last Processed</dt>
                    <dd className="text-white">
                      {new Date(story.lastProcessedAt!).toLocaleString()}
                    </dd>
                  </div>
                )}
                {/* Show saved target info */}
                {(story.targetProject || story.targetClass || story.targetMethod) && (
                  <div>
                    <dt className="text-sm text-slate-400">Target</dt>
                    <dd className="text-white font-mono text-sm">
                      {story.targetProject || '*'}
                      {story.targetClass && ` → ${story.targetClass}`}
                      {story.targetMethod && `.${story.targetMethod}()`}
                      {story.changeType !== undefined && (
                        <span className={`ml-2 px-2 py-0.5 rounded text-xs ${getChangeTypeColor(story.changeType)} text-white`}>
                          {getChangeTypeLabel(story.changeType)}
                        </span>
                      )}
                    </dd>
                  </div>
                )}
              </dl>
            </div>
            <div>
              <h3 className="text-lg font-semibold text-white mb-4">Summary</h3>
              <div className="space-y-3">
                <div className="p-4 bg-slate-900 rounded-lg">
                  <div className="text-3xl font-bold text-blue-400">
                    {story.tasks.length}
                  </div>
                  <div className="text-sm text-slate-400">Tasks Generated</div>
                </div>
                <div className="p-4 bg-slate-900 rounded-lg">
                  <div className="text-3xl font-bold text-emerald-400">
                    {story.tasks.filter(t => t.status === TaskStatus.Completed).length}
                  </div>
                  <div className="text-sm text-slate-400">Tasks Completed</div>
                </div>
              </div>
            </div>
          </div>
        )}

        {activeTab === 'content' && (
          <div>
            <div className="flex items-center justify-between mb-4">
              <h3 className="text-lg font-semibold text-white">Story Content</h3>
              {!isEditing && canEditContent && (
                <button
                  onClick={handleEditContent}
                  className="flex items-center gap-2 px-3 py-1.5 bg-blue-600 hover:bg-blue-700 text-white text-sm rounded-lg transition-colors"
                >
                  <Edit2 className="w-4 h-4" />
                  Edit
                </button>
              )}
              {!isEditing && !canEditContent && story && (
                <div className="text-sm text-slate-500 flex items-center gap-2">
                  <span className="px-2 py-1 bg-amber-500/10 text-amber-400 rounded text-xs">
                    Reset required to edit
                  </span>
                </div>
              )}
            </div>

            {isEditing ? (
              <div className="space-y-4">
                {/* Name Input */}
                <div>
                  <label className="block text-sm text-slate-400 mb-1">Story Name</label>
                  <input
                    type="text"
                    value={editedName}
                    onChange={(e) => setEditedName(e.target.value)}
                    className="w-full px-3 py-2 bg-slate-900 border border-slate-600 rounded-lg text-white placeholder-slate-500 focus:border-blue-500 focus:outline-none"
                    placeholder="Enter story name..."
                  />
                </div>

                {/* Content Textarea */}
                <div>
                  <label className="block text-sm text-slate-400 mb-1">Content</label>
                  <textarea
                    value={editedContent}
                    onChange={(e) => setEditedContent(e.target.value)}
                    className="w-full h-96 bg-slate-900 p-4 rounded-lg text-slate-300 font-mono text-sm border border-slate-600 focus:border-blue-500 focus:outline-none resize-y"
                    placeholder="Enter story content..."
                  />
                </div>
                <div className="flex items-center justify-end gap-3">
                  <button
                    onClick={handleCancelEdit}
                    disabled={isSaving}
                    className="px-4 py-2 bg-slate-700 hover:bg-slate-600 text-white rounded-lg transition-colors disabled:opacity-50"
                  >
                    Cancel
                  </button>
                  <button
                    onClick={handleSaveContent}
                    disabled={isSaving || !editedContent.trim()}
                    className="flex items-center gap-2 px-4 py-2 bg-emerald-600 hover:bg-emerald-700 text-white rounded-lg transition-colors disabled:opacity-50"
                  >
                    {isSaving ? (
                      <RefreshCw className="w-4 h-4 animate-spin" />
                    ) : (
                      <Save className="w-4 h-4" />
                    )}
                    Save Changes
                  </button>
                </div>
              </div>
            ) : (
              <pre className="bg-slate-900 p-4 rounded-lg overflow-x-auto text-slate-300 whitespace-pre-wrap">
                {content || 'No content available'}
              </pre>
            )}
          </div>
        )}

        {activeTab === 'tasks' && (
          <div>
            <h3 className="text-lg font-semibold text-white mb-4">
              Tasks ({story.tasks.length})
            </h3>
            {story.tasks.length === 0 ? (
              <div className="text-center py-8 text-slate-400">
                No tasks yet. Start the pipeline to generate tasks.
              </div>
            ) : (
              <div className="space-y-3">
                {story.tasks.map((task, index) => (
                  <div
                    key={index}
                    className={`p-4 bg-slate-900 rounded-lg border ${task.type === TaskType.Fix
                      ? 'border-red-500/50 bg-red-950/20'
                      : task.isModification
                        ? 'border-amber-500/50'
                        : 'border-slate-700'
                      }`}
                  >
                    <div className="flex items-start justify-between">
                      <div className="flex items-center gap-3">
                        <span className="text-xl">{getTaskStatusIcon(task.status)}</span>
                        <div>
                          <div className="flex items-center gap-2">
                            <span className="font-medium text-white">{task.title}</span>
                            {task.type === TaskType.Fix && (
                              <span className="px-2 py-0.5 bg-red-500/20 text-red-400 text-xs rounded font-semibold">
                                🔧 Fix {task.retryAttempt ? `#${task.retryAttempt}` : ''}
                              </span>
                            )}
                            {task.isModification && task.type !== TaskType.Fix && (
                              <span className="px-2 py-0.5 bg-amber-500/20 text-amber-400 text-xs rounded">
                                Modify
                              </span>
                            )}
                          </div>
                          <div className="text-sm text-slate-400">
                            {task.projectName && `Project: ${task.projectName}`}
                          </div>
                        </div>
                      </div>
                      <span className="text-xs text-slate-500">#{task.index}</span>
                    </div>
                    {task.description && (
                      <p className="mt-2 text-sm text-slate-400">{task.description}</p>
                    )}
                    {task.targetFiles.length > 0 && (
                      <div className="mt-2 flex flex-wrap gap-1">
                        {task.targetFiles.map((file, i) => (
                          <span
                            key={i}
                            className={`inline-flex items-center gap-1 px-2 py-1 rounded text-xs ${task.isModification
                              ? 'bg-amber-500/10 text-amber-300'
                              : 'bg-slate-800 text-slate-300'
                              }`}
                          >
                            <FileCode className="w-3 h-3" />
                            {file}
                          </span>
                        ))}
                      </div>
                    )}
                    {task.usesExisting && task.usesExisting.length > 0 && (
                      <div className="mt-2 text-xs text-slate-500">
                        Uses: {task.usesExisting.join(', ')}
                      </div>
                    )}
                  </div>
                ))}
              </div>
            )}
          </div>
        )}

        {activeTab === 'output' && (
          <div>
            <h3 className="text-lg font-semibold text-white mb-4">Generated Output</h3>
            {Object.keys(output).length === 0 ? (
              <div className="text-center py-8 text-slate-400">
                No output yet. Complete the pipeline to see generated files.
              </div>
            ) : (
              <div className="space-y-4">
                {Object.entries(output).map(([filename, fileContent]) => (
                  <div key={filename} className="border border-slate-700 rounded-lg overflow-hidden">
                    <div className="px-4 py-2 bg-slate-700 text-white font-mono text-sm">
                      {filename}
                    </div>
                    <pre className="p-4 bg-slate-900 overflow-x-auto text-sm text-slate-300">
                      {fileContent}
                    </pre>
                  </div>
                ))}
              </div>
            )}
          </div>
        )}
      </div>

      {/* History Modal */}
      {showHistory && (
        <div className="fixed inset-0 bg-black/70 backdrop-blur-sm flex items-center justify-center z-50 p-4">
          <div className="bg-slate-800 border border-slate-700 rounded-2xl w-full max-w-4xl max-h-[90vh] overflow-hidden flex flex-col">
            {/* Modal Header */}
            <div className="flex items-center justify-between p-6 border-b border-slate-700">
              <div className="flex items-center gap-3">
                <div className="w-10 h-10 bg-purple-500/20 rounded-lg flex items-center justify-center">
                  <History className="w-5 h-5 text-purple-400" />
                </div>
                <div>
                  <h2 className="text-xl font-bold text-white">Pipeline History</h2>
                  <p className="text-sm text-slate-400">Complete execution summary for {story.name}</p>
                </div>
              </div>
              <button
                onClick={() => setShowHistory(false)}
                className="p-2 hover:bg-slate-700 rounded-lg transition-colors text-slate-400 hover:text-white"
              >
                <X className="w-5 h-5" />
              </button>
            </div>

            {/* Modal Content - Using shared PipelineHistorySummary component */}
            <div className="flex-1 overflow-y-auto p-6">
              {historyLoading ? (
                <div className="flex items-center justify-center py-12">
                  <RefreshCw className="w-8 h-8 text-blue-400 animate-spin" />
                </div>
              ) : historyData ? (
                <PipelineHistorySummary status={historyData} showSuccessBanner={true} compact={true} />
              ) : (
                <div className="text-center py-12 text-slate-400">
                  <History className="w-12 h-12 mx-auto mb-4 opacity-50" />
                  <p>No pipeline history found for this story.</p>
                  <p className="text-sm mt-2">History is saved when a pipeline completes successfully.</p>
                </div>
              )}
            </div>

            {/* Modal Footer */}
            <div className="p-4 border-t border-slate-700 flex justify-end">
              <button
                onClick={() => setShowHistory(false)}
                className="px-4 py-2 bg-slate-700 hover:bg-slate-600 text-white rounded-lg transition-colors"
              >
                Close
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
