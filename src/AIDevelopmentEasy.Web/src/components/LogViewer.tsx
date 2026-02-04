import { useEffect, useRef } from 'react';
import { PipelineUpdateMessage } from '../types';

interface LogViewerProps {
  logs: PipelineUpdateMessage[];
  className?: string;
}

export function LogViewer({ logs, className = '' }: LogViewerProps) {
  const containerRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (containerRef.current) {
      containerRef.current.scrollTop = containerRef.current.scrollHeight;
    }
  }, [logs]);

  const getLogColor = (updateType: string) => {
    switch (updateType.toLowerCase()) {
      case 'error':
        return 'text-red-400';
      case 'warning':
        return 'text-amber-400';
      case 'success':
      case 'completed':
        return 'text-emerald-400';
      case 'info':
        return 'text-blue-400';
      case 'llmcallstarting':
        return 'text-cyan-400';
      case 'llmcallcompleted':
        return 'text-emerald-400';
      default:
        return 'text-slate-300';
    }
  };

  const formatTime = (timestamp: string) => {
    return new Date(timestamp).toLocaleTimeString('en-US', {
      hour: '2-digit',
      minute: '2-digit',
      second: '2-digit'
    });
  };

  return (
    <div
      ref={containerRef}
      className={`bg-slate-900 rounded-xl border border-slate-700 p-4 font-mono text-sm overflow-y-auto ${className}`}
    >
      {logs.length === 0 ? (
        <div className="text-slate-500 italic">Waiting for logs...</div>
      ) : (
        logs.map((log, index) => (
          <div
            key={index}
            className="py-1 animate-slide-in"
            style={{ animationDelay: `${index * 50}ms` }}
          >
            <span className="text-slate-500">[{formatTime(log.timestamp)}]</span>
            {' '}
            <span className={getLogColor(log.updateType)}>
              [{log.updateType}]
            </span>
            {' '}
            <span className="text-slate-300">{log.message}</span>
          </div>
        ))
      )}
    </div>
  );
}
