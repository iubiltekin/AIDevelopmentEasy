import { Check, Clock, Loader2, AlertCircle, SkipForward, FileText, HelpCircle, Sparkles, GitBranch, CheckSquare } from 'lucide-react';
import { 
  WizardStatusDto, 
  WizardPhaseState,
  WizardPhase,
  RequirementDetailDto,
  QuestionDto,
  StoryDefinitionDto,
  AnswerDto,
  QuestionType,
  getWizardPhaseLabel,
  getQuestionCategoryLabel,
  getStoryComplexityLabel,
  getStoryComplexityColor
} from '../types';

interface WizardStatusProps {
  status: WizardStatusDto;
  requirement: RequirementDetailDto;
  answers: Record<string, AnswerDto>;
  aiNotes: string;
  selectedStories: Set<string>;
  onAnswerChange: (questionId: string, selectedOptions: string[], textResponse?: string) => void;
  onAiNotesChange: (notes: string) => void;
  onStoryToggle: (storyId: string) => void;
  onApprove: () => void;
  onSubmitAnswers: () => void;
  onCreateStories: () => void;
  actionLoading: boolean;
}

// Phase type icon
function getPhaseTypeIcon(phase: WizardPhase) {
  switch (phase) {
    case WizardPhase.Input:
      return <FileText className="w-5 h-5" />;
    case WizardPhase.Analysis:
      return <Sparkles className="w-5 h-5" />;
    case WizardPhase.Questions:
      return <HelpCircle className="w-5 h-5" />;
    case WizardPhase.Refinement:
      return <FileText className="w-5 h-5" />;
    case WizardPhase.Decomposition:
      return <GitBranch className="w-5 h-5" />;
    case WizardPhase.Review:
      return <CheckSquare className="w-5 h-5" />;
    case WizardPhase.Completed:
      return <Check className="w-5 h-5" />;
    default:
      return <FileText className="w-5 h-5" />;
  }
}

// Phase description
function getPhaseDescription(phase: WizardPhase): string {
  const descriptions: Record<WizardPhase, string> = {
    [WizardPhase.Input]: 'Raw requirement entered',
    [WizardPhase.Analysis]: 'AI analyzing requirement',
    [WizardPhase.Questions]: 'Answer clarifying questions',
    [WizardPhase.Refinement]: 'AI creating final document',
    [WizardPhase.Decomposition]: 'AI generating stories',
    [WizardPhase.Review]: 'Select stories to create',
    [WizardPhase.Completed]: 'Wizard completed'
  };
  return descriptions[phase] || '';
}

export function WizardStatus({ 
  status, 
  requirement, 
  answers, 
  aiNotes,
  selectedStories,
  onAnswerChange,
  onAiNotesChange,
  onStoryToggle,
  onApprove,
  onSubmitAnswers,
  onCreateStories,
  actionLoading 
}: WizardStatusProps) {
  
  const getPhaseIcon = (state: WizardPhaseState, phase: WizardPhase) => {
    switch (state) {
      case WizardPhaseState.Completed:
        return <Check className="w-5 h-5 text-emerald-400" />;
      case WizardPhaseState.Running:
        return <Loader2 className="w-5 h-5 text-blue-400 animate-spin" />;
      case WizardPhaseState.WaitingApproval:
        return <Clock className="w-5 h-5 text-amber-400" />;
      case WizardPhaseState.Failed:
        return <AlertCircle className="w-5 h-5 text-red-400" />;
      case WizardPhaseState.Skipped:
        return <SkipForward className="w-5 h-5 text-slate-500" />;
      default:
        return <span className="text-slate-600">{getPhaseTypeIcon(phase)}</span>;
    }
  };

  const getPhaseStateLabel = (state: WizardPhaseState) => {
    const labels: Record<WizardPhaseState, string> = {
      [WizardPhaseState.Pending]: 'Pending',
      [WizardPhaseState.Running]: 'Running',
      [WizardPhaseState.WaitingApproval]: 'Waiting',
      [WizardPhaseState.Completed]: 'Completed',
      [WizardPhaseState.Failed]: 'Failed',
      [WizardPhaseState.Skipped]: 'Skipped'
    };
    return labels[state];
  };

  const isCompleted = status.currentPhase === WizardPhase.Completed;
  const completedCount = status.phases.filter(p => 
    p.state === WizardPhaseState.Completed || p.state === WizardPhaseState.Skipped
  ).length;
  const progressPercent = isCompleted ? 100 : Math.min(((completedCount) / (status.phases.length - 1)) * 100, 100);

  return (
    <div className="space-y-6">
      {/* Phase Steps with Progress Line */}
      <div className="bg-slate-800/50 backdrop-blur border border-slate-700 rounded-xl p-6">
        <h2 className="text-lg font-semibold text-white mb-6">Wizard Progress</h2>
        <div className="relative py-2">
          {/* Background line */}
          <div className="absolute top-7 left-5 right-5 h-0.5 bg-slate-700" />
          {/* Progress line */}
          <div 
            className={`absolute top-7 left-5 h-0.5 transition-all duration-500 ${
              isCompleted ? 'bg-emerald-500' : 'bg-blue-500'
            }`}
            style={{ width: `calc(${progressPercent}% - 20px)` }}
          />
          
          <div className="relative flex justify-between">
            {status.phases.map((phase, index) => (
              <div key={phase.phase} className="flex flex-col items-center" style={{ animationDelay: `${index * 100}ms` }}>
                <div 
                  className={`w-10 h-10 rounded-full flex items-center justify-center transition-all duration-300 ${
                    phase.state === WizardPhaseState.Completed ? 'bg-emerald-500/20 ring-2 ring-emerald-500' :
                    phase.state === WizardPhaseState.Running ? 'bg-blue-500/20 ring-2 ring-blue-500 animate-pulse' :
                    phase.state === WizardPhaseState.WaitingApproval ? 'bg-amber-500/20 ring-2 ring-amber-500' :
                    phase.state === WizardPhaseState.Failed ? 'bg-red-500/20 ring-2 ring-red-500' :
                    phase.state === WizardPhaseState.Skipped ? 'bg-slate-600/50 ring-2 ring-slate-600' :
                    'bg-slate-800'
                  }`}
                >
                  {getPhaseIcon(phase.state, phase.phase)}
                </div>
                <span className="mt-2 text-xs font-medium text-slate-300">
                  {getWizardPhaseLabel(phase.phase)}
                </span>
                <span className={`text-xs ${
                  phase.state === WizardPhaseState.Completed ? 'text-emerald-400' :
                  phase.state === WizardPhaseState.Running ? 'text-blue-400' :
                  phase.state === WizardPhaseState.WaitingApproval ? 'text-amber-400' :
                  phase.state === WizardPhaseState.Failed ? 'text-red-400' :
                  'text-slate-500'
                }`}>
                  {getPhaseStateLabel(phase.state)}
                </span>
              </div>
            ))}
          </div>
        </div>
      </div>

      {/* Running Phase */}
      {status.phases.some(p => p.state === WizardPhaseState.Running) && (
        <div className="p-4 bg-blue-500/10 border border-blue-500/30 rounded-xl">
          <div className="flex items-center gap-3">
            <Loader2 className="w-6 h-6 text-blue-400 animate-spin" />
            <div>
              <span className="text-blue-300 font-medium">
                {getWizardPhaseLabel(status.currentPhase)} in progress...
              </span>
              <div className="text-xs text-slate-400">
                {getPhaseDescription(status.currentPhase)}
              </div>
            </div>
          </div>
        </div>
      )}

      {/* Analysis Complete - Show Questions Preview */}
      {status.currentPhase === WizardPhase.Analysis && 
       status.phases.find(p => p.phase === WizardPhase.Analysis)?.state === WizardPhaseState.WaitingApproval && (
        <div className="p-4 bg-amber-500/10 border border-amber-500/30 rounded-xl">
          <div className="flex items-center gap-2 mb-2">
            <Sparkles className="w-5 h-5 text-amber-400" />
            <h3 className="text-lg font-semibold text-amber-400">
              Analysis Complete - {requirement.questions?.questions.length || 0} Questions Generated
            </h3>
          </div>
          <p className="text-slate-300 mb-4">
            AI has analyzed your requirement and generated clarifying questions. Review and proceed to answer them.
          </p>
          <div className="flex gap-3">
            <button
              onClick={onApprove}
              disabled={actionLoading}
              className="px-6 py-2 bg-emerald-600 hover:bg-emerald-700 disabled:bg-slate-600 text-white font-medium rounded-lg transition-colors flex items-center gap-2"
            >
              {actionLoading ? <Loader2 className="w-4 h-4 animate-spin" /> : <Check className="w-4 h-4" />}
              Approve & Continue
            </button>
          </div>
        </div>
      )}

      {/* Questions Phase - Answer Form */}
      {status.currentPhase === WizardPhase.Questions && requirement.questions && (
        <div className="p-4 bg-amber-500/10 border border-amber-500/30 rounded-xl">
          <div className="flex items-center gap-2 mb-4">
            <HelpCircle className="w-5 h-5 text-amber-400" />
            <h3 className="text-lg font-semibold text-amber-400">
              Answer Clarifying Questions ({requirement.questions.questions.length})
            </h3>
          </div>
          
          <div className="space-y-4 max-h-[500px] overflow-y-auto pr-2">
            {requirement.questions.questions.map((q: QuestionDto) => (
              <QuestionCard
                key={q.id}
                question={q}
                answer={answers[q.id]}
                onAnswerChange={onAnswerChange}
              />
            ))}
          </div>
          
          {/* AI Notes */}
          <div className="mt-4 p-4 bg-slate-800/50 rounded-lg">
            <h4 className="text-white font-medium mb-2">Additional Notes for AI (optional)</h4>
            <textarea
              value={aiNotes}
              onChange={(e) => onAiNotesChange(e.target.value)}
              placeholder="Add any extra context, constraints, or requirements..."
              rows={3}
              className="w-full px-3 py-2 bg-slate-900 border border-slate-600 rounded-lg text-white placeholder-slate-500 focus:ring-2 focus:ring-blue-500"
            />
          </div>
          
          <div className="mt-4 flex gap-3">
            <button
              onClick={onSubmitAnswers}
              disabled={actionLoading}
              className="px-6 py-2 bg-emerald-600 hover:bg-emerald-700 disabled:bg-slate-600 text-white font-medium rounded-lg transition-colors flex items-center gap-2"
            >
              {actionLoading ? <Loader2 className="w-4 h-4 animate-spin" /> : <Check className="w-4 h-4" />}
              Submit Answers & Continue
            </button>
          </div>
        </div>
      )}

      {/* Refinement Complete - Show Final Document */}
      {status.currentPhase === WizardPhase.Refinement && 
       status.phases.find(p => p.phase === WizardPhase.Refinement)?.state === WizardPhaseState.WaitingApproval && (
        <div className="p-4 bg-amber-500/10 border border-amber-500/30 rounded-xl">
          <div className="flex items-center gap-2 mb-2">
            <FileText className="w-5 h-5 text-amber-400" />
            <h3 className="text-lg font-semibold text-amber-400">
              Refinement Complete - Final Requirement Document
            </h3>
          </div>
          <div className="mb-4 p-4 bg-slate-900/50 rounded-lg border border-slate-700 max-h-80 overflow-y-auto">
            <pre className="text-sm text-slate-300 whitespace-pre-wrap font-mono">
              {requirement.finalContent}
            </pre>
          </div>
          <div className="flex gap-3">
            <button
              onClick={onApprove}
              disabled={actionLoading}
              className="px-6 py-2 bg-emerald-600 hover:bg-emerald-700 disabled:bg-slate-600 text-white font-medium rounded-lg transition-colors flex items-center gap-2"
            >
              {actionLoading ? <Loader2 className="w-4 h-4 animate-spin" /> : <Check className="w-4 h-4" />}
              Approve & Generate Stories
            </button>
          </div>
        </div>
      )}

      {/* Decomposition Complete - Show Stories Preview */}
      {status.currentPhase === WizardPhase.Decomposition && 
       status.phases.find(p => p.phase === WizardPhase.Decomposition)?.state === WizardPhaseState.WaitingApproval && (
        <div className="p-4 bg-amber-500/10 border border-amber-500/30 rounded-xl">
          <div className="flex items-center gap-2 mb-2">
            <GitBranch className="w-5 h-5 text-amber-400" />
            <h3 className="text-lg font-semibold text-amber-400">
              Decomposition Complete - {requirement.generatedStories.length} Stories Generated
            </h3>
          </div>
          <p className="text-slate-300 mb-4">
            AI has broken down your requirement into implementable stories. Review and proceed to select which ones to create.
          </p>
          <div className="flex gap-3">
            <button
              onClick={onApprove}
              disabled={actionLoading}
              className="px-6 py-2 bg-emerald-600 hover:bg-emerald-700 disabled:bg-slate-600 text-white font-medium rounded-lg transition-colors flex items-center gap-2"
            >
              {actionLoading ? <Loader2 className="w-4 h-4 animate-spin" /> : <Check className="w-4 h-4" />}
              Approve & Select Stories
            </button>
          </div>
        </div>
      )}

      {/* Review Phase - Story Selection */}
      {status.currentPhase === WizardPhase.Review && requirement.generatedStories.length > 0 && (
        <div className="p-4 bg-amber-500/10 border border-amber-500/30 rounded-xl">
          <div className="flex items-center gap-2 mb-4">
            <CheckSquare className="w-5 h-5 text-amber-400" />
            <h3 className="text-lg font-semibold text-amber-400">
              Select Stories to Create ({selectedStories.size} of {requirement.generatedStories.length} selected)
            </h3>
          </div>
          
          <div className="space-y-3 max-h-[500px] overflow-y-auto pr-2">
            {requirement.generatedStories.map((story: StoryDefinitionDto) => (
              <StoryCard
                key={story.id}
                story={story}
                selected={selectedStories.has(story.id)}
                onToggle={() => onStoryToggle(story.id)}
              />
            ))}
          </div>
          
          <div className="mt-4 flex gap-3">
            <button
              onClick={onCreateStories}
              disabled={actionLoading || selectedStories.size === 0}
              className="px-6 py-2 bg-emerald-600 hover:bg-emerald-700 disabled:bg-slate-600 text-white font-medium rounded-lg transition-colors flex items-center gap-2"
            >
              {actionLoading ? <Loader2 className="w-4 h-4 animate-spin" /> : <Check className="w-4 h-4" />}
              Create {selectedStories.size} Stories
            </button>
          </div>
        </div>
      )}

      {/* Completed */}
      {isCompleted && (
        <div className="p-6 bg-emerald-500/10 border border-emerald-500/30 rounded-xl text-center">
          <div className="inline-flex items-center justify-center w-16 h-16 bg-emerald-500/20 rounded-full mb-4">
            <Check className="w-8 h-8 text-emerald-400" />
          </div>
          <h2 className="text-xl font-semibold text-white mb-2">Wizard Completed Successfully!</h2>
          <p className="text-slate-400">
            {requirement.createdStoryIds.length} stories have been created and are ready for the pipeline.
          </p>
        </div>
      )}

      {/* Error Display */}
      {status.error && (
        <div className="p-4 bg-red-500/10 border border-red-500/30 rounded-xl">
          <div className="flex items-center gap-2 text-red-400">
            <AlertCircle className="w-5 h-5" />
            <span className="font-medium">Error</span>
          </div>
          <p className="mt-2 text-red-300">{status.error}</p>
        </div>
      )}
    </div>
  );
}

// Question Card Component
function QuestionCard({ 
  question, 
  answer, 
  onAnswerChange 
}: { 
  question: QuestionDto; 
  answer?: AnswerDto;
  onAnswerChange: (questionId: string, selectedOptions: string[], textResponse?: string) => void;
}) {
  return (
    <div className="p-4 bg-slate-800/50 rounded-lg border border-slate-700">
      <div className="flex items-start gap-3 mb-3">
        <span className="px-2 py-0.5 text-xs font-medium bg-slate-700 text-slate-300 rounded">
          {getQuestionCategoryLabel(question.category)}
        </span>
        {question.required && (
          <span className="text-red-400 text-xs">Required</span>
        )}
      </div>
      <p className="text-white font-medium mb-2">{question.text}</p>
      {question.context && (
        <p className="text-slate-400 text-sm mb-3">{question.context}</p>
      )}
      
      {/* Answer Input */}
      {question.type === QuestionType.Single && (
        <div className="space-y-2">
          {question.options.map((opt) => (
            <label key={opt} className="flex items-center gap-2 cursor-pointer hover:bg-slate-700/50 p-2 rounded">
              <input
                type="radio"
                name={question.id}
                checked={answer?.selectedOptions?.includes(opt) ?? false}
                onChange={() => onAnswerChange(question.id, [opt])}
                className="w-4 h-4 text-blue-600"
              />
              <span className="text-slate-300">{opt}</span>
            </label>
          ))}
        </div>
      )}
      
      {question.type === QuestionType.Multiple && (
        <div className="space-y-2">
          {question.options.map((opt) => (
            <label key={opt} className="flex items-center gap-2 cursor-pointer hover:bg-slate-700/50 p-2 rounded">
              <input
                type="checkbox"
                checked={answer?.selectedOptions?.includes(opt) ?? false}
                onChange={(e) => {
                  const current = answer?.selectedOptions ?? [];
                  const newOpts = e.target.checked
                    ? [...current, opt]
                    : current.filter(o => o !== opt);
                  onAnswerChange(question.id, newOpts);
                }}
                className="w-4 h-4 text-blue-600 rounded"
              />
              <span className="text-slate-300">{opt}</span>
            </label>
          ))}
        </div>
      )}
      
      {question.type === QuestionType.Text && (
        <textarea
          value={answer?.textResponse ?? ''}
          onChange={(e) => onAnswerChange(question.id, [], e.target.value)}
          placeholder="Enter your answer..."
          rows={3}
          className="w-full px-3 py-2 bg-slate-900 border border-slate-600 rounded-lg text-white placeholder-slate-500 focus:ring-2 focus:ring-blue-500"
        />
      )}
    </div>
  );
}

// Story Card Component
function StoryCard({ 
  story, 
  selected, 
  onToggle 
}: { 
  story: StoryDefinitionDto; 
  selected: boolean;
  onToggle: () => void;
}) {
  return (
    <div 
      className={`p-4 bg-slate-800/50 rounded-lg border-2 transition-colors cursor-pointer hover:bg-slate-700/50 ${
        selected ? 'border-blue-500' : 'border-transparent'
      }`}
      onClick={onToggle}
    >
      <div className="flex items-start gap-3">
        <input
          type="checkbox"
          checked={selected}
          onChange={() => {}}
          className="w-5 h-5 mt-1 text-blue-600 rounded"
        />
        <div className="flex-1 min-w-0">
          <div className="flex items-center gap-2 mb-1">
            <span className="text-slate-500 text-sm font-mono">{story.id}</span>
            <span className={`px-2 py-0.5 text-xs font-medium rounded ${getStoryComplexityColor(story.estimatedComplexity)} text-white`}>
              {getStoryComplexityLabel(story.estimatedComplexity)}
            </span>
          </div>
          <h4 className="text-white font-medium">{story.title}</h4>
          <p className="text-slate-400 text-sm mt-1 line-clamp-2">{story.description}</p>
          
          {story.acceptanceCriteria.length > 0 && (
            <div className="mt-2">
              <p className="text-slate-500 text-xs font-medium">Acceptance Criteria:</p>
              <ul className="text-slate-400 text-sm list-disc list-inside">
                {story.acceptanceCriteria.slice(0, 2).map((ac, i) => (
                  <li key={i} className="truncate">{ac}</li>
                ))}
                {story.acceptanceCriteria.length > 2 && (
                  <li className="text-slate-500">+{story.acceptanceCriteria.length - 2} more...</li>
                )}
              </ul>
            </div>
          )}
          
          {story.dependencies.length > 0 && (
            <div className="mt-2 text-xs text-slate-500">
              Dependencies: {story.dependencies.join(', ')}
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
