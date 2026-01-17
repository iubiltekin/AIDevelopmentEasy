import { useState, useEffect } from 'react';
import { useNavigate, Link } from 'react-router-dom';
import { ArrowLeft, FileText, Save, Database } from 'lucide-react';
import { StoryType, CreateStoryRequest, CodebaseDto, CodebaseStatus } from '../types';
import { storiesApi, codebasesApi } from '../services/api';

export function NewStory() {
  const navigate = useNavigate();
  const [name, setName] = useState('');
  const [content, setContent] = useState('');
  const [codebaseId, setCodebaseId] = useState<string>('');
  const [codebases, setCodebases] = useState<CodebaseDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    // Load available codebases
    codebasesApi.getAll()
      .then(data => setCodebases(data.filter(c => c.status === CodebaseStatus.Ready)))
      .catch(console.error);
  }, []);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();

    if (!name.trim()) {
      setError('Please enter a story name');
      return;
    }

    if (!content.trim()) {
      setError('Please enter story content');
      return;
    }

    try {
      setLoading(true);
      setError(null);

      const request: CreateStoryRequest = {
        name: name.trim(),
        content: content.trim(),
        type: StoryType.Single, // Always use Single type
        codebaseId: codebaseId || undefined
      };

      const created = await storiesApi.create(request);
      navigate(`/storys/${created.id}`);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to create story');
    } finally {
      setLoading(false);
    }
  };

  const storyTemplate = `# Feature Name

## Description
Brief description of the feature or functionality needed.

## Storys
- Story 1
- Story 2
- Story 3

## Technical Details
- Target framework: .NET 8.0
- Additional libraries needed: (if any)

## Example Usage
\`\`\`csharp
// Example code showing how the feature should be used
var service = new MyService();
var result = service.DoSomething();
\`\`\`

## Acceptance Criteria
- [ ] Criteria 1
- [ ] Criteria 2
- [ ] Unit tests included
`;

  const applyTemplate = () => {
    setContent(storyTemplate);
  };

  return (
    <div className="p-8 max-w-4xl mx-auto">
      {/* Header */}
      <div className="flex items-center gap-4 mb-8">
        <Link
          to="/"
          className="p-2 text-slate-400 hover:text-white hover:bg-slate-700 rounded-lg transition-colors"
        >
          <ArrowLeft className="w-5 h-5" />
        </Link>
        <div>
          <h1 className="text-2xl font-bold text-white">New Story</h1>
          <p className="text-slate-400">Create a new story for the AI pipeline</p>
        </div>
      </div>

      {/* Error */}
      {error && (
        <div className="mb-6 p-4 bg-red-500/10 border border-red-500/30 rounded-xl text-red-400">
          {error}
        </div>
      )}

      <form onSubmit={handleSubmit} className="space-y-6">
        {/* Name */}
        <div>
          <label className="block text-sm font-medium text-slate-300 mb-2">
            Story Name
          </label>
          <input
            type="text"
            value={name}
            onChange={e => setName(e.target.value)}
            placeholder="e.g., Log Rotation Helper, Authentication Service"
            className="w-full px-4 py-3 bg-slate-800 border border-slate-600 rounded-lg text-white placeholder-slate-500 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent"
          />
        </div>

        {/* Codebase Selection */}
        <div>
          <label className="block text-sm font-medium text-slate-300 mb-2">
            <div className="flex items-center gap-2">
              <Database className="w-4 h-4" />
              Target Codebase (Optional)
            </div>
          </label>
          <select
            value={codebaseId}
            onChange={e => setCodebaseId(e.target.value)}
            className="w-full px-4 py-3 bg-slate-800 border border-slate-600 rounded-lg text-white focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent"
          >
            <option value="">No codebase (new project)</option>
            {codebases.map(cb => (
              <option key={cb.id} value={cb.id}>
                {cb.name} - {cb.summary?.totalProjects || 0} projects, {cb.summary?.primaryFramework || 'Unknown'}
              </option>
            ))}
          </select>
          {codebaseId ? (
            <div className="mt-2 p-3 bg-blue-500/10 border border-blue-500/30 rounded-lg">
              <p className="text-xs text-blue-300 font-medium mb-2">🔍 Codebase-Aware Pipeline Enabled:</p>
              <ol className="text-xs text-slate-400 space-y-1 list-decimal list-inside">
                <li><span className="text-blue-300">Analysis</span> → CodeAnalysisAgent finds classes &amp; references</li>
                <li><span className="text-blue-300">Planning</span> → PlannerAgent creates tasks for existing files</li>
                <li><span className="text-blue-300">Coding</span> → CoderAgent modifies existing code</li>
                <li><span className="text-blue-300">Debugging</span> → DebuggerAgent verifies changes</li>
                <li><span className="text-blue-300">Reviewing</span> → ReviewerAgent checks quality</li>
              </ol>
            </div>
          ) : (
            <p className="mt-1 text-xs text-slate-500">
              Select a codebase to enable modification mode. Without a codebase, the pipeline will
              generate new standalone code.
            </p>
          )}
        </div>

        {/* Content */}
        <div>
          <div className="flex items-center justify-between mb-2">
            <label className="flex items-center gap-2 text-sm font-medium text-slate-300">
              <FileText className="w-4 h-4" />
              Story Content
            </label>
            <button
              type="button"
              onClick={applyTemplate}
              className="text-sm text-blue-400 hover:text-blue-300"
            >
              Use Template
            </button>
          </div>
          <textarea
            value={content}
            onChange={e => setContent(e.target.value)}
            rows={20}
            placeholder="Enter your story in Markdown format...

Describe:
- What you want to build
- Key features and functionality
- Technical constraints
- Expected behavior"
            className="w-full px-4 py-3 bg-slate-900 border border-slate-600 rounded-lg text-white font-mono text-sm placeholder-slate-500 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent resize-none"
          />
        </div>

        {/* Submit */}
        <div className="flex gap-4">
          <button
            type="submit"
            disabled={loading}
            className="flex items-center gap-2 px-6 py-3 bg-blue-600 hover:bg-blue-700 disabled:opacity-50 disabled:cursor-not-allowed text-white font-medium rounded-lg transition-colors"
          >
            <Save className="w-5 h-5" />
            {loading ? 'Creating...' : 'Create Story'}
          </button>
          <Link
            to="/"
            className="px-6 py-3 bg-slate-700 hover:bg-slate-600 text-white font-medium rounded-lg transition-colors"
          >
            Cancel
          </Link>
        </div>
      </form>
    </div>
  );
}
