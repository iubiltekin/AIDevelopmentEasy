import { StoryStatus, getStatusLabel, getStatusColor } from '../types';

interface StatusBadgeProps {
  status: StoryStatus;
  className?: string;
}

export function StatusBadge({ status, className = '' }: StatusBadgeProps) {
  const icons: Record<StoryStatus, string> = {
    [StoryStatus.NotStarted]: '⬜',
    [StoryStatus.Planned]: '📋',
    [StoryStatus.Approved]: '✅',
    [StoryStatus.InProgress]: '🔄',
    [StoryStatus.Completed]: '✔️',
    [StoryStatus.Failed]: '❌'
  };

  return (
    <span 
      className={`inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-medium ${getStatusColor(status)} text-white ${className}`}
    >
      <span>{icons[status]}</span>
      <span>{getStatusLabel(status)}</span>
    </span>
  );
}
