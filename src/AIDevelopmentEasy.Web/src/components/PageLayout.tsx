import { ReactNode } from 'react';
import { RefreshCw, LucideIcon } from 'lucide-react';

interface PageAction {
  label: string;
  onClick: () => void;
  icon?: LucideIcon;
  variant?: 'primary' | 'secondary';
}

interface PageLayoutProps {
  title: string;
  description: string;
  loading?: boolean;
  onRefresh?: () => void;
  actions?: PageAction[];
  children: ReactNode;
}

export function PageLayout({ 
  title, 
  description, 
  loading = false, 
  onRefresh, 
  actions = [],
  children 
}: PageLayoutProps) {
  return (
    <div className="p-8">
      {/* Header */}
      <div className="flex items-center justify-between mb-8">
        <div>
          <h1 className="text-3xl font-bold text-white mb-2">{title}</h1>
          <p className="text-slate-400">{description}</p>
        </div>
        <div className="flex gap-3">
          {onRefresh && (
            <button
              onClick={onRefresh}
              className="flex items-center gap-2 px-4 py-2 bg-slate-700 hover:bg-slate-600 text-white rounded-lg transition-colors"
            >
              <RefreshCw className={`w-4 h-4 ${loading ? 'animate-spin' : ''}`} />
              Refresh
            </button>
          )}
          {actions.map((action, index) => {
            const Icon = action.icon;
            const isPrimary = action.variant !== 'secondary';
            return (
              <button
                key={index}
                onClick={action.onClick}
                className={`flex items-center gap-2 px-4 py-2 rounded-lg transition-colors ${
                  isPrimary 
                    ? 'bg-blue-600 hover:bg-blue-700 text-white' 
                    : 'bg-slate-700 hover:bg-slate-600 text-white'
                }`}
              >
                {Icon && <Icon className="w-4 h-4" />}
                {action.label}
              </button>
            );
          })}
        </div>
      </div>
      
      {children}
    </div>
  );
}

// Stat Card Component
interface StatCardProps {
  icon: LucideIcon;
  iconColor: string;
  bgColor: string;
  value: number | string;
  label: string;
}

export function StatCard({ icon: Icon, iconColor, bgColor, value, label }: StatCardProps) {
  return (
    <div className="bg-slate-800/50 backdrop-blur border border-slate-700 rounded-xl p-5">
      <div className="flex items-center gap-3">
        <div className={`p-3 ${bgColor} rounded-lg`}>
          <Icon className={`w-6 h-6 ${iconColor}`} />
        </div>
        <div>
          <div className="text-2xl font-bold text-white">{value}</div>
          <div className="text-sm text-slate-400">{label}</div>
        </div>
      </div>
    </div>
  );
}

// Stats Grid Container
interface StatsGridProps {
  children: ReactNode;
}

export function StatsGrid({ children }: StatsGridProps) {
  return (
    <div className="grid grid-cols-4 gap-4 mb-8">
      {children}
    </div>
  );
}

// Error Alert Component
interface ErrorAlertProps {
  message: string;
}

export function ErrorAlert({ message }: ErrorAlertProps) {
  return (
    <div className="mb-6 p-4 bg-red-500/10 border border-red-500/30 rounded-xl text-red-400">
      {message}
    </div>
  );
}

// Loading Spinner
export function LoadingSpinner() {
  return (
    <div className="flex justify-center items-center py-20">
      <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-blue-500"></div>
    </div>
  );
}

// Empty State
interface EmptyStateProps {
  message: string;
  actionLabel?: string;
  onAction?: () => void;
}

export function EmptyState({ message, actionLabel, onAction }: EmptyStateProps) {
  return (
    <div className="text-center py-20">
      <p className="text-slate-400 text-lg mb-4">{message}</p>
      {actionLabel && onAction && (
        <button
          onClick={onAction}
          className="px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white rounded-lg transition-colors"
        >
          {actionLabel}
        </button>
      )}
    </div>
  );
}
