import { useState, useEffect, useCallback, useRef } from 'react';
import { useParams, useNavigate, Link } from 'react-router-dom';
import { ArrowLeft, RefreshCw, XCircle, Play, Edit2, Save, X } from 'lucide-react';
import { requirementsApi, codebasesApi } from '../services/api';
import type { 
  RequirementDetailDto, 
  WizardStatusDto,
  AnswerDto,
  CodebaseDto,
  RequirementType
} from '../types';
import { 
  RequirementStatus,
  getRequirementTypeLabel,
  getRequirementTypeColor,
  getRequirementStatusLabel,
  getRequirementStatusColor
} from '../types';
import { WizardStatus } from '../components/WizardStatus';

export default function RequirementDetail() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  
  const [requirement, setRequirement] = useState<RequirementDetailDto | null>(null);
  const [wizardStatus, setWizardStatus] = useState<WizardStatusDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [actionLoading, setActionLoading] = useState(false);
  
  // Edit mode state
  const [isEditing, setIsEditing] = useState(false);
  const [editTitle, setEditTitle] = useState('');
  const [editContent, setEditContent] = useState('');
  const [editType, setEditType] = useState<RequirementType>(0);
  const [editCodebaseId, setEditCodebaseId] = useState<string>('');
  const [codebases, setCodebases] = useState<CodebaseDto[]>([]);
  const [saving, setSaving] = useState(false);
  
  // Answers state
  const [answers, setAnswers] = useState<Record<string, AnswerDto>>({});
  const [aiNotes, setAiNotes] = useState('');
  
  // Story selection state
  const [selectedStories, setSelectedStories] = useState<Set<string>>(new Set());
  
  // Track if stories have been initialized (to prevent auto-refresh from resetting selection)
  const storiesInitializedRef = useRef(false);

  const loadData = useCallback(async () => {
    if (!id) return;
    
    try {
      const [req, status] = await Promise.all([
        requirementsApi.getById(id),
        requirementsApi.getWizardStatus(id)
      ]);
      setRequirement(req);
      setWizardStatus(status);
      
      // Initialize edit fields
      setEditTitle(req.title);
      setEditContent(req.rawContent);
      setEditType(req.type);
      setEditCodebaseId(req.codebaseId || '');
      
      // Initialize answers from saved data (only if not already set)
      if (req.answers?.answers && Object.keys(answers).length === 0) {
        const answerMap: Record<string, AnswerDto> = {};
        req.answers.answers.forEach(a => {
          answerMap[a.questionId] = a;
        });
        setAnswers(answerMap);
      }
      
      // Initialize AI notes (only if not already set)
      if (req.aiNotes && !aiNotes) {
        setAiNotes(req.aiNotes);
      }
      
      // Initialize story selection ONLY on first load (all selected by default)
      // Don't reset on auto-refresh to preserve user's selection
      if (req.generatedStories?.length > 0 && !storiesInitializedRef.current) {
        setSelectedStories(new Set(req.generatedStories.map(s => s.id)));
        storiesInitializedRef.current = true;
      }
      
      setError(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load requirement');
    } finally {
      setLoading(false);
    }
  }, [id, answers, aiNotes]);

  // Load codebases for edit form
  useEffect(() => {
    codebasesApi.getAll().then(setCodebases).catch(console.error);
  }, []);

  useEffect(() => {
    loadData();
  }, [loadData]);

  // Auto-refresh when wizard is running
  useEffect(() => {
    if (wizardStatus?.isRunning) {
      const interval = setInterval(loadData, 2000);
      return () => clearInterval(interval);
    }
  }, [wizardStatus?.isRunning, loadData]);

  const handleStartEdit = () => {
    if (requirement) {
      setEditTitle(requirement.title);
      setEditContent(requirement.rawContent);
      setEditType(requirement.type);
      setEditCodebaseId(requirement.codebaseId || '');
      setIsEditing(true);
    }
  };

  const handleCancelEdit = () => {
    setIsEditing(false);
    if (requirement) {
      setEditTitle(requirement.title);
      setEditContent(requirement.rawContent);
      setEditType(requirement.type);
      setEditCodebaseId(requirement.codebaseId || '');
    }
  };

  const handleSaveEdit = async () => {
    if (!id || !editContent.trim()) return;
    
    try {
      setSaving(true);
      await requirementsApi.update(id, {
        title: editTitle || undefined,
        rawContent: editContent,
        type: editType,
        codebaseId: editCodebaseId || null
      });
      setIsEditing(false);
      await loadData();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to save changes');
    } finally {
      setSaving(false);
    }
  };

  const handleStartWizard = async () => {
    if (!id) return;
    try {
      setActionLoading(true);
      const status = await requirementsApi.startWizard(id, false);
      setWizardStatus(status);
      await loadData();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to start wizard');
    } finally {
      setActionLoading(false);
    }
  };

  const handleApprove = async () => {
    if (!id) return;
    try {
      setActionLoading(true);
      const status = await requirementsApi.approvePhase(id, true);
      setWizardStatus(status);
      await loadData();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to approve phase');
    } finally {
      setActionLoading(false);
    }
  };

  const handleSubmitAnswers = async () => {
    if (!id) return;
    try {
      setActionLoading(true);
      const answerList = Object.values(answers);
      const status = await requirementsApi.submitAnswers(id, {
        answers: answerList,
        aiNotes: aiNotes || undefined
      });
      setWizardStatus(status);
      await loadData();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to submit answers');
    } finally {
      setActionLoading(false);
    }
  };

  const handleCreateStories = async () => {
    if (!id || selectedStories.size === 0) return;
    try {
      setActionLoading(true);
      const status = await requirementsApi.createStories(id, {
        selectedStoryIds: Array.from(selectedStories)
      });
      setWizardStatus(status);
      await loadData();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to create stories');
    } finally {
      setActionLoading(false);
    }
  };

  const handleCancel = async () => {
    if (!id || !confirm('Are you sure you want to cancel the wizard?')) return;
    try {
      setActionLoading(true);
      const status = await requirementsApi.cancelWizard(id);
      setWizardStatus(status);
      await loadData();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to cancel wizard');
    } finally {
      setActionLoading(false);
    }
  };

  const updateAnswer = (questionId: string, selectedOptions: string[], textResponse?: string) => {
    setAnswers(prev => ({
      ...prev,
      [questionId]: { questionId, selectedOptions, textResponse }
    }));
  };

  const toggleStory = (storyId: string) => {
    setSelectedStories(prev => {
      const next = new Set(prev);
      if (next.has(storyId)) {
        next.delete(storyId);
      } else {
        next.add(storyId);
      }
      return next;
    });
  };

  if (loading) {
    return (
      <div className="flex items-center justify-center h-full">
        <RefreshCw className="w-8 h-8 text-blue-400 animate-spin" />
      </div>
    );
  }

  if (!requirement) {
    return (
      <div className="p-8">
        <div className="bg-red-500/10 border border-red-500/20 rounded-lg p-8 text-center">
          <h2 className="text-xl font-semibold text-red-400 mb-2">Requirement Not Found</h2>
          <button onClick={() => navigate('/requirements')} className="text-blue-400 hover:text-blue-300">
            Back to Requirements
          </button>
        </div>
      </div>
    );
  }

  const isWizardRunning = wizardStatus?.isRunning ?? false;
  const canEdit = requirement.status === RequirementStatus.Draft && !isWizardRunning;

  return (
    <div className="p-8">
      {/* Header */}
      <div className="flex items-center justify-between mb-8">
        <div className="flex items-center gap-4">
          <Link
            to="/requirements"
            className="p-2 text-slate-400 hover:text-white hover:bg-slate-700 rounded-lg transition-colors"
          >
            <ArrowLeft className="w-5 h-5" />
          </Link>
          <div>
            <div className="flex items-center gap-2 mb-1">
              <span className={`px-2 py-0.5 text-xs font-medium rounded ${getRequirementTypeColor(requirement.type)} text-white`}>
                {getRequirementTypeLabel(requirement.type)}
              </span>
              <span className={`px-2 py-0.5 text-xs font-medium rounded ${getRequirementStatusColor(requirement.status)} text-white`}>
                {getRequirementStatusLabel(requirement.status)}
              </span>
            </div>
            <h1 className="text-2xl font-bold text-white">
              {requirement.title}
            </h1>
            <p className="text-slate-400 text-sm">{requirement.id}</p>
          </div>
        </div>
        
        {/* Action Buttons */}
        <div className="flex gap-3">
          <button
            onClick={loadData}
            className="flex items-center gap-2 px-4 py-2 bg-slate-700 hover:bg-slate-600 text-white rounded-lg transition-colors"
          >
            <RefreshCw className="w-4 h-4" />
            Refresh
          </button>
          
          {canEdit && !isEditing && (
            <button
              onClick={handleStartEdit}
              className="flex items-center gap-2 px-4 py-2 bg-slate-700 hover:bg-slate-600 text-white rounded-lg transition-colors"
            >
              <Edit2 className="w-4 h-4" />
              Edit
            </button>
          )}
          
          {requirement.status === RequirementStatus.Draft && !isEditing && (
            <button
              onClick={handleStartWizard}
              disabled={actionLoading}
              className="flex items-center gap-2 px-4 py-2 bg-blue-600 hover:bg-blue-700 disabled:bg-slate-600 text-white rounded-lg transition-colors"
            >
              {actionLoading ? (
                <RefreshCw className="w-4 h-4 animate-spin" />
              ) : (
                <Play className="w-4 h-4" />
              )}
              Start Wizard
            </button>
          )}
          
          {isWizardRunning && (
            <button
              onClick={handleCancel}
              disabled={actionLoading}
              className="flex items-center gap-2 px-4 py-2 bg-red-600 hover:bg-red-700 disabled:bg-slate-600 text-white rounded-lg transition-colors"
            >
              <XCircle className="w-4 h-4" />
              Cancel
            </button>
          )}
        </div>
      </div>

      {/* Error Display */}
      {error && (
        <div className="mb-6 p-4 bg-red-500/10 border border-red-500/30 rounded-xl text-red-400">
          {error}
          <button onClick={() => setError(null)} className="ml-4 text-red-300 hover:text-white">
            Dismiss
          </button>
        </div>
      )}

      {/* Edit Form or Raw Requirement Display */}
      {isEditing ? (
        <div className="mb-6 bg-slate-800/50 backdrop-blur border border-blue-500/50 rounded-xl p-6">
          <div className="flex items-center justify-between mb-4">
            <h2 className="text-lg font-semibold text-white">Edit Requirement</h2>
            <div className="flex gap-2">
              <button
                onClick={handleCancelEdit}
                className="flex items-center gap-2 px-3 py-1.5 text-slate-400 hover:text-white transition-colors"
              >
                <X className="w-4 h-4" />
                Cancel
              </button>
              <button
                onClick={handleSaveEdit}
                disabled={saving || !editContent.trim()}
                className="flex items-center gap-2 px-4 py-1.5 bg-emerald-600 hover:bg-emerald-700 disabled:bg-slate-600 text-white rounded-lg transition-colors"
              >
                {saving ? (
                  <RefreshCw className="w-4 h-4 animate-spin" />
                ) : (
                  <Save className="w-4 h-4" />
                )}
                Save
              </button>
            </div>
          </div>
          
          <div className="space-y-4">
            {/* Title */}
            <div>
              <label className="block text-sm font-medium text-slate-300 mb-1">
                Title
              </label>
              <input
                type="text"
                value={editTitle}
                onChange={(e) => setEditTitle(e.target.value)}
                placeholder="Auto-generated from content if empty"
                className="w-full px-4 py-2 bg-slate-900 border border-slate-600 rounded-lg text-white placeholder-slate-500 focus:ring-2 focus:ring-blue-500 focus:border-transparent"
              />
            </div>
            
            {/* Type Selection */}
            <div>
              <label className="block text-sm font-medium text-slate-300 mb-2">
                Requirement Type
              </label>
              <div className="grid grid-cols-2 md:grid-cols-4 gap-2">
                {[0, 1, 2, 3].map((type) => (
                  <button
                    key={type}
                    type="button"
                    onClick={() => setEditType(type as RequirementType)}
                    className={`px-4 py-2 rounded-lg border transition-colors ${
                      editType === type
                        ? 'bg-blue-600 border-blue-500 text-white'
                        : 'bg-slate-900 border-slate-600 text-slate-300 hover:border-slate-500'
                    }`}
                  >
                    <span className={`inline-block w-2 h-2 rounded-full mr-2 ${getRequirementTypeColor(type as RequirementType)}`}></span>
                    {getRequirementTypeLabel(type as RequirementType)}
                  </button>
                ))}
              </div>
            </div>
            
            {/* Codebase Selection */}
            <div>
              <label className="block text-sm font-medium text-slate-300 mb-1">
                Codebase (optional)
              </label>
              <select
                value={editCodebaseId}
                onChange={(e) => setEditCodebaseId(e.target.value)}
                className="w-full px-4 py-2 bg-slate-900 border border-slate-600 rounded-lg text-white focus:ring-2 focus:ring-blue-500 focus:border-transparent"
              >
                <option value="">No codebase</option>
                {codebases.map((cb) => (
                  <option key={cb.id} value={cb.id}>
                    {cb.name} ({cb.path})
                  </option>
                ))}
              </select>
            </div>
            
            {/* Raw Content */}
            <div>
              <label className="block text-sm font-medium text-slate-300 mb-1">
                Raw Requirement <span className="text-red-400">*</span>
              </label>
              <textarea
                value={editContent}
                onChange={(e) => setEditContent(e.target.value)}
                placeholder="Describe your requirement in plain text..."
                rows={10}
                className="w-full px-4 py-3 bg-slate-900 border border-slate-600 rounded-lg text-white placeholder-slate-500 focus:ring-2 focus:ring-blue-500 focus:border-transparent resize-none font-mono text-sm"
              />
            </div>
          </div>
        </div>
      ) : (
        <div className="mb-6 bg-slate-800/50 backdrop-blur border border-slate-700 rounded-xl p-6">
          <div className="flex items-center justify-between mb-3">
            <h2 className="text-lg font-semibold text-white">Raw Requirement</h2>
            {canEdit && (
              <button
                onClick={handleStartEdit}
                className="flex items-center gap-1 text-sm text-slate-400 hover:text-white transition-colors"
              >
                <Edit2 className="w-4 h-4" />
                Edit
              </button>
            )}
          </div>
          <pre className="text-slate-300 whitespace-pre-wrap font-mono text-sm bg-slate-900/50 p-4 rounded-lg border border-slate-700">
            {requirement.rawContent}
          </pre>
        </div>
      )}

      {/* Wizard Status */}
      {wizardStatus && !isEditing && (
        <WizardStatus
          status={wizardStatus}
          requirement={requirement}
          answers={answers}
          aiNotes={aiNotes}
          selectedStories={selectedStories}
          onAnswerChange={updateAnswer}
          onAiNotesChange={setAiNotes}
          onStoryToggle={toggleStory}
          onApprove={handleApprove}
          onSubmitAnswers={handleSubmitAnswers}
          onCreateStories={handleCreateStories}
          actionLoading={actionLoading}
        />
      )}

      {/* Final Requirement Document - Show when available and not in earlier phases */}
      {requirement.finalContent && wizardStatus && 
       wizardStatus.currentPhase > 3 && !isEditing && (
        <div className="mt-6 bg-slate-800/50 backdrop-blur border border-slate-700 rounded-xl p-6">
          <h2 className="text-lg font-semibold text-white mb-3">Final Requirement Document</h2>
          <pre className="text-slate-300 whitespace-pre-wrap font-mono text-sm bg-slate-900/50 p-4 rounded-lg border border-slate-700 max-h-80 overflow-y-auto">
            {requirement.finalContent}
          </pre>
        </div>
      )}

      {/* Completed Actions */}
      {requirement.status === RequirementStatus.Completed && !isEditing && (
        <div className="mt-6 flex gap-4">
          <Link
            to="/"
            className="flex-1 text-center py-4 bg-emerald-600 hover:bg-emerald-700 text-white font-medium rounded-xl transition-colors"
          >
            View Stories in Dashboard
          </Link>
          <Link
            to="/requirements"
            className="flex-1 text-center py-4 bg-slate-600 hover:bg-slate-700 text-white font-medium rounded-xl transition-colors"
          >
            Back to Requirements
          </Link>
        </div>
      )}
    </div>
  );
}
