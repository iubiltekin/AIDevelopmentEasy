import { useState, useEffect } from 'react';
import { Save, RefreshCw, Server, Wifi, WifiOff } from 'lucide-react';

export function Settings() {
  const [apiUrl, setApiUrl] = useState('http://localhost:5000');
  const [healthStatus, setHealthStatus] = useState<'checking' | 'healthy' | 'unhealthy'>('checking');
  const [saved, setSaved] = useState(false);

  const checkHealth = async () => {
    setHealthStatus('checking');
    try {
      const response = await fetch(`${apiUrl}/health`);
      if (response.ok) {
        setHealthStatus('healthy');
      } else {
        setHealthStatus('unhealthy');
      }
    } catch {
      setHealthStatus('unhealthy');
    }
  };

  useEffect(() => {
    checkHealth();
  }, []);

  const handleSave = () => {
    localStorage.setItem('apiUrl', apiUrl);
    setSaved(true);
    setTimeout(() => setSaved(false), 2000);
    checkHealth();
  };

  return (
    <div className="p-8 max-w-2xl mx-auto">
      <div className="mb-8">
        <h1 className="text-3xl font-bold text-white mb-2">Settings</h1>
        <p className="text-slate-400">Configure the application settings</p>
      </div>

      <div className="space-y-6">
        {/* API Connection */}
        <div className="bg-slate-800/50 backdrop-blur border border-slate-700 rounded-xl p-6">
          <div className="flex items-center gap-3 mb-4">
            <Server className="w-5 h-5 text-blue-400" />
            <h2 className="text-lg font-semibold text-white">API Connection</h2>
          </div>

          <div className="space-y-4">
            <div>
              <label className="block text-sm font-medium text-slate-300 mb-2">
                API Base URL
              </label>
              <input
                type="text"
                value={apiUrl}
                onChange={e => setApiUrl(e.target.value)}
                className="w-full px-4 py-3 bg-slate-900 border border-slate-600 rounded-lg text-white focus:outline-none focus:ring-2 focus:ring-blue-500"
              />
            </div>

            <div className="flex items-center justify-between">
              <div className="flex items-center gap-2">
                {healthStatus === 'checking' ? (
                  <>
                    <RefreshCw className="w-4 h-4 text-slate-400 animate-spin" />
                    <span className="text-slate-400">Checking connection...</span>
                  </>
                ) : healthStatus === 'healthy' ? (
                  <>
                    <Wifi className="w-4 h-4 text-emerald-400" />
                    <span className="text-emerald-400">Connected</span>
                  </>
                ) : (
                  <>
                    <WifiOff className="w-4 h-4 text-red-400" />
                    <span className="text-red-400">Not connected</span>
                  </>
                )}
              </div>
              <button
                onClick={checkHealth}
                className="px-4 py-2 bg-slate-700 hover:bg-slate-600 text-white rounded-lg transition-colors"
              >
                Test Connection
              </button>
            </div>
          </div>
        </div>

        {/* About */}
        <div className="bg-slate-800/50 backdrop-blur border border-slate-700 rounded-xl p-6">
          <h2 className="text-lg font-semibold text-white mb-4">About</h2>
          <div className="space-y-3 text-slate-400">
            <p>
              <strong className="text-white">AI Development Easy</strong> is a multi-agent 
              framework for AI-assisted software development.
            </p>
            <p>
              It uses multiple specialized AI agents (Planner, Coder, Debugger, Reviewer) 
              to process requirements and generate high-quality code.
            </p>
            <div className="pt-4 border-t border-slate-700">
              <p className="text-sm">
                Version: <span className="text-white">1.0.0</span>
              </p>
              <p className="text-sm">
                Made with ❤️ using Azure OpenAI
              </p>
            </div>
          </div>
        </div>

        {/* Save Button */}
        <button
          onClick={handleSave}
          className="flex items-center gap-2 px-6 py-3 bg-blue-600 hover:bg-blue-700 text-white font-medium rounded-lg transition-colors"
        >
          <Save className="w-5 h-5" />
          {saved ? 'Saved!' : 'Save Settings'}
        </button>
      </div>
    </div>
  );
}
