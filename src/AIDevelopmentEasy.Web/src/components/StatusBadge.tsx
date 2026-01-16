import { RequirementStatus, getStatusLabel, getStatusColor } from '../types';

interface StatusBadgeProps {
  status: RequirementStatus;
  className?: string;
}

export function StatusBadge({ status, className = '' }: StatusBadgeProps) {
  const icons: Record<RequirementStatus, string> = {
    [RequirementStatus.NotStarted]: '⬜',
    [RequirementStatus.Planned]: '📋',
    [RequirementStatus.Approved]: '✅',
    [RequirementStatus.InProgress]: '🔄',
    [RequirementStatus.Completed]: '✔️',
    [RequirementStatus.Failed]: '❌'
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
