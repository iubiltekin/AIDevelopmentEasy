import { useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { knowledgeApi } from '../services/api';
import {
  PatternSubcategory,
  ErrorType,
  getPatternSubcategoryLabel,
  getErrorTypeLabel
} from '../types';
import type { TemplateFileDto, PackageInfoDto } from '../types';

type CreateType = 'pattern' | 'error' | 'template' | 'insight';

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

  // Template fields
  const [description, setDescription] = useState('');
  const [templateType, setTemplateType] = useState('WebAPI');
  const [targetFramework, setTargetFramework] = useState('net8.0');
  const [templateFilePath, setTemplateFilePath] = useState('');
  const [templateFileContent, setTemplateFileContent] = useState('');
  const [packagesStr, setPackagesStr] = useState('');
  const [setupInstructions, setSetupInstructions] = useState('');

  // Insight fields
  const [agentName, setAgentName] = useState('');
  const [promptInsight, setPromptInsight] = useState('');
  const [optimalTemperature, setOptimalTemperature] = useState<string>('');
  const [scenario, setScenario] = useState('');
  const [improvementDescription, setImprovementDescription] = useState('');

  const isPattern = type === 'pattern';
  const isError = type === 'error';
  const isTemplate = type === 'template';
  const isInsight = type === 'insight';

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
      } else if (isError) {
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
      } else if (isTemplate) {
        const templateFiles: TemplateFileDto[] = [];
        if (templateFilePath.trim()) {
          templateFiles.push({ path: templateFilePath.trim(), content: templateFileContent, isRequired: true });
        }
        const packages: PackageInfoDto[] = packagesStr.split(',').map(p => {
          const trimmed = p.trim();
          const at = trimmed.indexOf('@');
          if (at > 0) {
            return { name: trimmed.slice(0, at), version: trimmed.slice(at + 1), isRequired: true };
          }
          return { name: trimmed, isRequired: true };
        }).filter(p => p.name);
        await knowledgeApi.createTemplate({
          title,
          description,
          tags: tagList,
          language,
          templateType: templateType.trim(),
          targetFramework: targetFramework.trim(),
          templateFiles,
          packages,
          setupInstructions: setupInstructions.trim() || undefined
        });
      } else if (isInsight) {
        await knowledgeApi.createInsight({
          title,
          description,
          tags: tagList,
          language,
          agentName: agentName.trim(),
          promptInsight: promptInsight.trim() || undefined,
          optimalTemperature: optimalTemperature === '' ? undefined : parseFloat(optimalTemperature),
          scenario: scenario.trim(),
          improvementDescription: improvementDescription.trim() || undefined
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
          {isPattern && 'Add New Pattern'}
          {isError && 'Add New Error Fix'}
          {isTemplate && 'Add New Template'}
          {isInsight && 'Add New Agent Insight'}
          {!isPattern && !isError && !isTemplate && !isInsight && 'Add Knowledge Entry'}
        </h1>
        <p className="text-slate-400 text-sm mt-1">
          {isPattern && 'Document a successful code pattern for future reference'}
          {isError && 'Document an error and its fix to help with future debugging'}
          {isTemplate && 'Add a project template for scaffolding (files, packages, setup)'}
          {isInsight && 'Record an agent tuning insight (prompt, temperature, scenario)'}
          {!isPattern && !isError && !isTemplate && !isInsight && 'Choose a type from Knowledge Base.'}
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
            {(isPattern || isError) && (
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
            )}
          </div>

          {(isTemplate || isInsight) && (
            <div>
              <label className="block text-sm font-medium text-slate-300 mb-1">
                Description
              </label>
              <textarea
                value={description}
                onChange={(e) => setDescription(e.target.value)}
                rows={3}
                placeholder="Short description of this template or insight..."
                className="w-full px-4 py-2 bg-slate-700 border border-slate-600 rounded-lg text-white placeholder-slate-400 focus:outline-none focus:border-blue-500"
              />
            </div>
          )}

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

        {/* Template-specific fields */}
        {isTemplate && (
          <div className="bg-slate-800 rounded-lg p-6 border border-slate-700 space-y-4">
            <h2 className="text-lg font-semibold text-white mb-4">Template Details</h2>
            <div className="grid grid-cols-2 gap-4">
              <div>
                <label className="block text-sm font-medium text-slate-300 mb-1">Template Type *</label>
                <input
                  type="text"
                  value={templateType}
                  onChange={(e) => setTemplateType(e.target.value)}
                  placeholder="e.g. WebAPI, Library, Console"
                  className="w-full px-4 py-2 bg-slate-700 border border-slate-600 rounded-lg text-white placeholder-slate-400 focus:outline-none focus:border-blue-500"
                />
              </div>
              <div>
                <label className="block text-sm font-medium text-slate-300 mb-1">Target Framework *</label>
                <input
                  type="text"
                  value={targetFramework}
                  onChange={(e) => setTargetFramework(e.target.value)}
                  placeholder="e.g. net8.0, go1.21"
                  className="w-full px-4 py-2 bg-slate-700 border border-slate-600 rounded-lg text-white placeholder-slate-400 focus:outline-none focus:border-blue-500"
                />
              </div>
            </div>
            <div>
              <label className="block text-sm font-medium text-slate-300 mb-1">Packages (comma separated, optional version: name@version)</label>
              <input
                type="text"
                value={packagesStr}
                onChange={(e) => setPackagesStr(e.target.value)}
                placeholder="Microsoft.EntityFrameworkCore, Newtonsoft.Json@13.0.1"
                className="w-full px-4 py-2 bg-slate-700 border border-slate-600 rounded-lg text-white placeholder-slate-400 focus:outline-none focus:border-blue-500"
              />
            </div>
            <div>
              <label className="block text-sm font-medium text-slate-300 mb-1">Template File Path</label>
              <input
                type="text"
                value={templateFilePath}
                onChange={(e) => setTemplateFilePath(e.target.value)}
                placeholder="e.g. Controllers/ValuesController.cs"
                className="w-full px-4 py-2 bg-slate-700 border border-slate-600 rounded-lg text-white placeholder-slate-400 focus:outline-none focus:border-blue-500"
              />
            </div>
            <div>
              <label className="block text-sm font-medium text-slate-300 mb-1">Template File Content</label>
              <textarea
                value={templateFileContent}
                onChange={(e) => setTemplateFileContent(e.target.value)}
                rows={10}
                placeholder="// File content (placeholders like {{ProjectName}} allowed)..."
                className="w-full px-4 py-2 bg-slate-900 border border-slate-600 rounded-lg text-slate-300 font-mono text-sm placeholder-slate-500 focus:outline-none focus:border-blue-500"
              />
            </div>
            <div>
              <label className="block text-sm font-medium text-slate-300 mb-1">Setup Instructions</label>
              <textarea
                value={setupInstructions}
                onChange={(e) => setSetupInstructions(e.target.value)}
                rows={4}
                placeholder="Steps to apply this template..."
                className="w-full px-4 py-2 bg-slate-700 border border-slate-600 rounded-lg text-white placeholder-slate-400 focus:outline-none focus:border-blue-500"
              />
            </div>
          </div>
        )}

        {/* Insight-specific fields */}
        {isInsight && (
          <div className="bg-slate-800 rounded-lg p-6 border border-slate-700 space-y-4">
            <h2 className="text-lg font-semibold text-white mb-4">Agent Insight Details</h2>
            <div>
              <label className="block text-sm font-medium text-slate-300 mb-1">Agent Name *</label>
              <input
                type="text"
                value={agentName}
                onChange={(e) => setAgentName(e.target.value)}
                required={isInsight}
                placeholder="e.g. CoderAgent, PlannerAgent"
                className="w-full px-4 py-2 bg-slate-700 border border-slate-600 rounded-lg text-white placeholder-slate-400 focus:outline-none focus:border-blue-500"
              />
            </div>
            <div>
              <label className="block text-sm font-medium text-slate-300 mb-1">Scenario *</label>
              <input
                type="text"
                value={scenario}
                onChange={(e) => setScenario(e.target.value)}
                required={isInsight}
                placeholder="e.g. When modifying Go handlers, when generating C# repositories"
                className="w-full px-4 py-2 bg-slate-700 border border-slate-600 rounded-lg text-white placeholder-slate-400 focus:outline-none focus:border-blue-500"
              />
            </div>
            <div>
              <label className="block text-sm font-medium text-slate-300 mb-1">Prompt Insight</label>
              <textarea
                value={promptInsight}
                onChange={(e) => setPromptInsight(e.target.value)}
                rows={4}
                placeholder="What prompt change improved results..."
                className="w-full px-4 py-2 bg-slate-700 border border-slate-600 rounded-lg text-white placeholder-slate-400 focus:outline-none focus:border-blue-500"
              />
            </div>
            <div>
              <label className="block text-sm font-medium text-slate-300 mb-1">Optimal Temperature (0–1)</label>
              <input
                type="text"
                value={optimalTemperature}
                onChange={(e) => setOptimalTemperature(e.target.value)}
                placeholder="e.g. 0.2"
                className="w-full px-4 py-2 bg-slate-700 border border-slate-600 rounded-lg text-white placeholder-slate-400 focus:outline-none focus:border-blue-500"
              />
            </div>
            <div>
              <label className="block text-sm font-medium text-slate-300 mb-1">Improvement Description</label>
              <textarea
                value={improvementDescription}
                onChange={(e) => setImprovementDescription(e.target.value)}
                rows={3}
                placeholder="What improved (speed, accuracy, fewer retries)..."
                className="w-full px-4 py-2 bg-slate-700 border border-slate-600 rounded-lg text-white placeholder-slate-400 focus:outline-none focus:border-blue-500"
              />
            </div>
          </div>
        )}

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
        {isError && (
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
            {loading ? 'Creating...' : `Create ${isPattern ? 'Pattern' : isError ? 'Error Fix' : isTemplate ? 'Template' : isInsight ? 'Agent Insight' : 'Entry'}`}
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
