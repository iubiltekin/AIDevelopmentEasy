import { Link, Outlet, useLocation } from 'react-router-dom';
import { FileCode, Settings, Wifi, WifiOff, Database, ClipboardList, BookOpen, ScrollText } from 'lucide-react';
import { useSignalR } from '../hooks/useSignalR';

export function Layout() {
  const location = useLocation();
  const { isConnected } = useSignalR();

  const mainNavItems = [
    { path: '/requirements', label: 'Requirements', icon: ClipboardList },
    { path: '/', label: 'Stories', icon: FileCode },
    { path: '/knowledge', label: 'Knowledge Base', icon: BookOpen },
  ];

  const utilityNavItems = [
    { path: '/codebases', label: 'Codebases', icon: Database },
    { path: '/prompts', label: 'Prompts', icon: ScrollText },
    { path: '/settings', label: 'Settings', icon: Settings },
  ];

  return (
    <div className="h-screen flex overflow-hidden">
      {/* Sidebar */}
      <aside className="w-64 h-full bg-slate-900/80 backdrop-blur-xl border-r border-slate-700 flex flex-col flex-shrink-0">
        <div className="p-6 border-b border-slate-700">
          <h1 className="text-xl font-bold text-white flex items-center gap-2">
            <span className="text-3xl">🤖</span>
            <div>
              <span className="bg-gradient-to-r from-blue-400 to-purple-400 bg-clip-text text-transparent">
                AI Development
              </span>
              <span className="block text-xs text-slate-400 font-normal">
                Easy Framework
              </span>
            </div>
          </h1>
        </div>

        <nav className="flex-1 p-4">
          <ul className="space-y-2">
            {mainNavItems.map(({ path, label, icon: Icon }) => {
              const isActive = location.pathname === path ||
                (path !== '/' && location.pathname.startsWith(path));

              return (
                <li key={path}>
                  <Link
                    to={path}
                    className={`flex items-center gap-3 px-4 py-3 rounded-xl transition-all duration-200 ${isActive
                      ? 'bg-blue-600/20 text-blue-400 border border-blue-500/30'
                      : 'text-slate-400 hover:bg-slate-800 hover:text-white'
                      }`}
                  >
                    <Icon className="w-5 h-5" />
                    <span className="font-medium">{label}</span>
                  </Link>
                </li>
              );
            })}
          </ul>
        </nav>

        {/* Utility Navigation */}
        <div className="p-4 border-t border-slate-700">
          <ul className="space-y-2">
            {utilityNavItems.map(({ path, label, icon: Icon }) => {
              const isActive = location.pathname === path ||
                (path !== '/' && location.pathname.startsWith(path));

              return (
                <li key={path}>
                  <Link
                    to={path}
                    className={`flex items-center gap-3 px-4 py-3 rounded-xl transition-all duration-200 ${isActive
                      ? 'bg-blue-600/20 text-blue-400 border border-blue-500/30'
                      : 'text-slate-400 hover:bg-slate-800 hover:text-white'
                      }`}
                  >
                    <Icon className="w-5 h-5" />
                    <span className="font-medium">{label}</span>
                  </Link>
                </li>
              );
            })}
          </ul>
        </div>

        {/* Connection Status */}
        <div className="p-4 border-t border-slate-700">
          <div className={`flex items-center gap-2 px-4 py-2 rounded-lg ${isConnected ? 'bg-emerald-500/10 text-emerald-400' : 'bg-red-500/10 text-red-400'
            }`}>
            {isConnected ? (
              <>
                <Wifi className="w-4 h-4" />
                <span className="text-sm">Connected</span>
              </>
            ) : (
              <>
                <WifiOff className="w-4 h-4" />
                <span className="text-sm">Disconnected</span>
              </>
            )}
          </div>
        </div>
      </aside>

      {/* Main Content */}
      <main className="flex-1 overflow-y-auto">
        <Outlet />
      </main>
    </div>
  );
}
