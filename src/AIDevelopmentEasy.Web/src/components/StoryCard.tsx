import { Link } from 'react-router-dom';
import { FileText, Play, RotateCcw, Trash2, Database } from 'lucide-react';
import { StoryDto, StoryStatus } from '../types';
import { StatusBadge } from './StatusBadge';

interface StoryCardProps {
  story: StoryDto;
  onStart: (id: string) => void;
  onReset: (id: string) => void;
  onDelete: (id: string) => void;
}

export function StoryCard({ story, onStart, onReset, onDelete }: StoryCardProps) {
  const isCompleted = story.status === StoryStatus.Completed;
  const isRunning = story.status === StoryStatus.InProgress;
  const canStart = !isRunning && story.status !== StoryStatus.Completed;

  return (
    <div className="bg-slate-800/50 backdrop-blur border border-slate-700 rounded-xl p-5 hover:border-blue-500/50 transition-all duration-300 animate-slide-in">
      <div className="flex items-start justify-between mb-4">
        <div className="flex items-center gap-3">
          <div className="p-2 bg-blue-500/20 rounded-lg">
            <FileText className="w-5 h-5 text-blue-400" />
          </div>
          <div>
            <Link 
              to={`/stories/${story.id}`}
              className="text-lg font-semibold text-white hover:text-blue-400 transition-colors"
            >
              {story.name}
            </Link>
            {story.codebaseId && (
              <div className="flex items-center gap-1 text-xs text-emerald-400 mt-0.5">
                <Database className="w-3 h-3" />
                With codebase context
              </div>
            )}
          </div>
        </div>
        <StatusBadge status={story.status} />
      </div>

      {story.tasks.length > 0 && (
        <div className="mb-4 text-sm text-slate-400">
          {story.tasks.length} task{story.tasks.length !== 1 ? 's' : ''} generated
        </div>
      )}

      <div className="flex items-center gap-2 mt-4 pt-4 border-t border-slate-700">
        {canStart && (
          <button
            onClick={() => onStart(story.id)}
            className="flex items-center gap-2 px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white text-sm font-medium rounded-lg transition-colors"
          >
            <Play className="w-4 h-4" />
            Start
          </button>
        )}
        
        {isRunning && (
          <Link
            to={`/pipeline/${story.id}`}
            className="flex items-center gap-2 px-4 py-2 bg-amber-600 hover:bg-amber-700 text-white text-sm font-medium rounded-lg transition-colors"
          >
            <span className="animate-spin">🔄</span>
            View Progress
          </Link>
        )}

        {isCompleted && (
          <Link
            to={`/stories/${story.id}`}
            className="flex items-center gap-2 px-4 py-2 bg-emerald-600 hover:bg-emerald-700 text-white text-sm font-medium rounded-lg transition-colors"
          >
            View Output
          </Link>
        )}

        <button
          onClick={() => onReset(story.id)}
          className="p-2 text-slate-400 hover:text-white hover:bg-slate-700 rounded-lg transition-colors"
          title="Reset"
        >
          <RotateCcw className="w-4 h-4" />
        </button>

        <button
          onClick={() => onDelete(story.id)}
          className="p-2 text-slate-400 hover:text-red-400 hover:bg-slate-700 rounded-lg transition-colors"
          title="Delete"
        >
          <Trash2 className="w-4 h-4" />
        </button>
      </div>
    </div>
  );
}
