import { useState } from 'react';
import { useNavigate, Link } from 'react-router-dom';
import { ArrowLeft, FileJson, FileText, Save } from 'lucide-react';
import { RequirementType, CreateRequirementRequest } from '../types';
import { requirementsApi } from '../services/api';

export function NewRequirement() {
  const navigate = useNavigate();
  const [name, setName] = useState('');
  const [content, setContent] = useState('');
  const [type, setType] = useState<RequirementType>(RequirementType.Single);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    
    if (!name.trim()) {
      setError('Please enter a requirement name');
      return;
    }
    
    if (!content.trim()) {
      setError('Please enter requirement content');
      return;
    }

    try {
      setLoading(true);
      setError(null);
      
      const request: CreateRequirementRequest = {
        name: name.trim(),
        content: content.trim(),
        type
      };
      
      const created = await requirementsApi.create(request);
      navigate(`/requirements/${created.id}`);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to create requirement');
    } finally {
      setLoading(false);
    }
  };

  const singleProjectTemplate = `# Feature Name

## Description
Brief description of the feature or functionality needed.

## Requirements
- Requirement 1
- Requirement 2
- Requirement 3

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

  const multiProjectTemplate = `{
  "name": "Feature Name",
  "description": "Brief description of the multi-project feature",
  "projects": [
    {
      "name": "MyLibrary",
      "type": "classlib",
      "framework": "net8.0",
      "requirements": [
        "Core functionality",
        "Public API"
      ]
    },
    {
      "name": "MyLibrary.Tests",
      "type": "xunit",
      "framework": "net8.0",
      "requirements": [
        "Unit tests for core functionality"
      ],
      "dependsOn": ["MyLibrary"]
    }
  ],
  "sharedRequirements": [
    "Follow SOLID principles",
    "Include XML documentation"
  ]
}`;

  const applyTemplate = () => {
    setContent(type === RequirementType.Multi ? multiProjectTemplate : singleProjectTemplate);
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
          <h1 className="text-2xl font-bold text-white">New Requirement</h1>
          <p className="text-slate-400">Create a new requirement for the AI pipeline</p>
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
            Requirement Name
          </label>
          <input
            type="text"
            value={name}
            onChange={e => setName(e.target.value)}
            placeholder="e.g., Log Rotation Helper, Authentication Service"
            className="w-full px-4 py-3 bg-slate-800 border border-slate-600 rounded-lg text-white placeholder-slate-500 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent"
          />
        </div>

        {/* Type */}
        <div>
          <label className="block text-sm font-medium text-slate-300 mb-2">
            Requirement Type
          </label>
          <div className="grid grid-cols-2 gap-4">
            <button
              type="button"
              onClick={() => setType(RequirementType.Single)}
              className={`p-4 rounded-xl border-2 transition-all ${
                type === RequirementType.Single
                  ? 'border-blue-500 bg-blue-500/10'
                  : 'border-slate-600 hover:border-slate-500'
              }`}
            >
              <div className="flex items-center gap-3">
                <div className={`p-2 rounded-lg ${
                  type === RequirementType.Single ? 'bg-blue-500/20' : 'bg-slate-700'
                }`}>
                  <FileText className={`w-5 h-5 ${
                    type === RequirementType.Single ? 'text-blue-400' : 'text-slate-400'
                  }`} />
                </div>
                <div className="text-left">
                  <div className={`font-medium ${
                    type === RequirementType.Single ? 'text-white' : 'text-slate-300'
                  }`}>
                    Single Project
                  </div>
                  <div className="text-sm text-slate-400">
                    Markdown or text format
                  </div>
                </div>
              </div>
            </button>

            <button
              type="button"
              onClick={() => setType(RequirementType.Multi)}
              className={`p-4 rounded-xl border-2 transition-all ${
                type === RequirementType.Multi
                  ? 'border-purple-500 bg-purple-500/10'
                  : 'border-slate-600 hover:border-slate-500'
              }`}
            >
              <div className="flex items-center gap-3">
                <div className={`p-2 rounded-lg ${
                  type === RequirementType.Multi ? 'bg-purple-500/20' : 'bg-slate-700'
                }`}>
                  <FileJson className={`w-5 h-5 ${
                    type === RequirementType.Multi ? 'text-purple-400' : 'text-slate-400'
                  }`} />
                </div>
                <div className="text-left">
                  <div className={`font-medium ${
                    type === RequirementType.Multi ? 'text-white' : 'text-slate-300'
                  }`}>
                    Multi-Project
                  </div>
                  <div className="text-sm text-slate-400">
                    JSON format with projects
                  </div>
                </div>
              </div>
            </button>
          </div>
        </div>

        {/* Content */}
        <div>
          <div className="flex items-center justify-between mb-2">
            <label className="block text-sm font-medium text-slate-300">
              Content
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
            placeholder={type === RequirementType.Multi 
              ? 'Enter JSON with project definitions...'
              : 'Enter your requirement in Markdown format...'
            }
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
            {loading ? 'Creating...' : 'Create Requirement'}
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
