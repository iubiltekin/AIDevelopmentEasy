import { useState, useEffect } from 'react';
import { Save, RefreshCw, Server, Wifi, WifiOff, Brain, Zap, RotateCcw, AlertTriangle } from 'lucide-react';

interface LLMSettings {
  maxPromptTokens: number;
  maxCompletionTokens: number;
  showPromptInfo: boolean;
  estimatedCostPer1KInputTokens: number;
  estimatedCostPer1KOutputTokens: number;
}

interface LLMUsageStats {
  totalCalls: number;
  totalPromptTokens: number;
  totalCompletionTokens: number;
  totalTokens: number;
  totalCostUSD: number;
  sessionStart: string;
  sessionDuration: string;
}

const API_BASE = 'http://localhost:5000';

export function Settings() {
  const [apiUrl, setApiUrl] = useState('http://localhost:5000');
  const [healthStatus, setHealthStatus] = useState<'checking' | 'healthy' | 'unhealthy'>('checking');
  const [saved, setSaved] = useState(false);

  // LLM Settings
  const [llmSettings, setLlmSettings] = useState<LLMSettings>({
    maxPromptTokens: 8000,
    maxCompletionTokens: 4000,
    showPromptInfo: true,
    estimatedCostPer1KInputTokens: 0.01,
    estimatedCostPer1KOutputTokens: 0.03
  });
  const [llmStats, setLlmStats] = useState<LLMUsageStats | null>(null);
  const [llmSaved, setLlmSaved] = useState(false);
  const [llmError, setLlmError] = useState<string | null>(null);

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

  const loadLLMSettings = async () => {
    try {
      const response = await fetch(`${API_BASE}/api/settings/llm`);
      if (response.ok) {
        const data = await response.json();
        setLlmSettings(data);
        setLlmError(null);
      }
    } catch (err) {
      setLlmError('Failed to load LLM settings');
    }
  };

  const loadLLMStats = async () => {
    try {
      const response = await fetch(`${API_BASE}/api/settings/llm/stats`);
      if (response.ok) {
        const data = await response.json();
        setLlmStats(data);
      }
    } catch {
      // Ignore stats error
    }
  };

  const saveLLMSettings = async () => {
    try {
      const response = await fetch(`${API_BASE}/api/settings/llm`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(llmSettings)
      });

      if (response.ok) {
        setLlmSaved(true);
        setLlmError(null);
        setTimeout(() => setLlmSaved(false), 2000);
      } else {
        const errorData = await response.text();
        setLlmError(errorData || 'Failed to save LLM settings');
      }
    } catch (err) {
      setLlmError('Failed to save LLM settings');
    }
  };

  const resetLLMStats = async () => {
    try {
      await fetch(`${API_BASE}/api/settings/llm/stats/reset`, { method: 'POST' });
      loadLLMStats();
    } catch {
      // Ignore
    }
  };

  useEffect(() => {
    checkHealth();
    loadLLMSettings();
    loadLLMStats();

    // Refresh stats periodically
    const interval = setInterval(loadLLMStats, 10000);
    return () => clearInterval(interval);
  }, []);

  const handleSave = () => {
    localStorage.setItem('apiUrl', apiUrl);
    setSaved(true);
    setTimeout(() => setSaved(false), 2000);
    checkHealth();
  };

  // Convert tokens to KB for display
  const tokensToKB = (tokens: number) => Math.ceil(tokens * 4 / 1024);

  // Preset options for token limits
  const presets = [
    { label: 'Small (~8KB)', tokens: 2000 },
    { label: 'Medium (~32KB)', tokens: 8000 },
    { label: 'Large (~64KB)', tokens: 16000 },
    { label: 'XL (~128KB)', tokens: 32000 },
    { label: 'Max (~500KB)', tokens: 128000 }
  ];

  return (
    <div className="p-8 max-w-4xl mx-auto">
      <div className="mb-8">
        <h1 className="text-3xl font-bold text-white mb-2">Settings</h1>
        <p className="text-slate-400">Configure the application and LLM settings</p>
      </div>

      <div className="space-y-6">
        {/* LLM Settings */}
        <div className="bg-slate-800/50 backdrop-blur border border-slate-700 rounded-xl p-6">
          <div className="flex items-center gap-3 mb-4">
            <Brain className="w-5 h-5 text-purple-400" />
            <h2 className="text-lg font-semibold text-white">LLM Configuration</h2>
          </div>

          {llmError && (
            <div className="mb-4 p-3 bg-red-500/20 border border-red-500/30 rounded-lg flex items-center gap-2 text-red-400">
              <AlertTriangle className="w-4 h-4" />
              {llmError}
            </div>
          )}

          <div className="space-y-6">
            {/* Token Limits */}
            <div>
              <label className="block text-sm font-medium text-slate-300 mb-2">
                Max Input Tokens (Prompt Limit)
              </label>
              <div className="flex items-center gap-4">
                <input
                  type="range"
                  min="1000"
                  max="128000"
                  step="1000"
                  value={llmSettings.maxPromptTokens}
                  onChange={e => setLlmSettings({ ...llmSettings, maxPromptTokens: parseInt(e.target.value) })}
                  className="flex-1 h-2 bg-slate-700 rounded-lg appearance-none cursor-pointer accent-purple-500"
                />
                <div className="w-32 text-right">
                  <span className="text-white font-mono">{llmSettings.maxPromptTokens.toLocaleString()}</span>
                  <span className="text-slate-500 text-sm ml-1">tokens</span>
                </div>
              </div>
              <div className="mt-1 text-xs text-slate-500">
                ≈ {tokensToKB(llmSettings.maxPromptTokens)} KB | 
                Presets: {presets.map((p, i) => (
                  <button
                    key={i}
                    onClick={() => setLlmSettings({ ...llmSettings, maxPromptTokens: p.tokens })}
                    className="ml-2 text-purple-400 hover:text-purple-300"
                  >
                    {p.label}
                  </button>
                ))}
              </div>
            </div>

            <div>
              <label className="block text-sm font-medium text-slate-300 mb-2">
                Max Output Tokens (Completion Limit)
              </label>
              <div className="flex items-center gap-4">
                <input
                  type="range"
                  min="500"
                  max="16000"
                  step="500"
                  value={llmSettings.maxCompletionTokens}
                  onChange={e => setLlmSettings({ ...llmSettings, maxCompletionTokens: parseInt(e.target.value) })}
                  className="flex-1 h-2 bg-slate-700 rounded-lg appearance-none cursor-pointer accent-purple-500"
                />
                <div className="w-32 text-right">
                  <span className="text-white font-mono">{llmSettings.maxCompletionTokens.toLocaleString()}</span>
                  <span className="text-slate-500 text-sm ml-1">tokens</span>
                </div>
              </div>
              <div className="mt-1 text-xs text-slate-500">
                ≈ {tokensToKB(llmSettings.maxCompletionTokens)} KB
              </div>
            </div>

            {/* Cost Settings */}
            <div className="grid grid-cols-2 gap-4">
              <div>
                <label className="block text-sm font-medium text-slate-300 mb-2">
                  Cost per 1K Input Tokens ($)
                </label>
                <input
                  type="number"
                  step="0.001"
                  min="0"
                  value={llmSettings.estimatedCostPer1KInputTokens}
                  onChange={e => setLlmSettings({ ...llmSettings, estimatedCostPer1KInputTokens: parseFloat(e.target.value) || 0 })}
                  className="w-full px-4 py-2 bg-slate-900 border border-slate-600 rounded-lg text-white focus:outline-none focus:ring-2 focus:ring-purple-500"
                />
              </div>
              <div>
                <label className="block text-sm font-medium text-slate-300 mb-2">
                  Cost per 1K Output Tokens ($)
                </label>
                <input
                  type="number"
                  step="0.001"
                  min="0"
                  value={llmSettings.estimatedCostPer1KOutputTokens}
                  onChange={e => setLlmSettings({ ...llmSettings, estimatedCostPer1KOutputTokens: parseFloat(e.target.value) || 0 })}
                  className="w-full px-4 py-2 bg-slate-900 border border-slate-600 rounded-lg text-white focus:outline-none focus:ring-2 focus:ring-purple-500"
                />
              </div>
            </div>

            {/* Show Prompt Info */}
            <div className="flex items-center justify-between">
              <div>
                <label className="text-sm font-medium text-slate-300">Show LLM Call Info</label>
                <p className="text-xs text-slate-500">Display token count and cost estimate for each LLM call</p>
              </div>
              <button
                onClick={() => setLlmSettings({ ...llmSettings, showPromptInfo: !llmSettings.showPromptInfo })}
                className={`relative w-12 h-6 rounded-full transition-colors ${
                  llmSettings.showPromptInfo ? 'bg-purple-600' : 'bg-slate-600'
                }`}
              >
                <div className={`absolute top-0.5 w-5 h-5 bg-white rounded-full transition-transform ${
                  llmSettings.showPromptInfo ? 'translate-x-6' : 'translate-x-0.5'
                }`} />
              </button>
            </div>

            {/* Save Button */}
            <button
              onClick={saveLLMSettings}
              className="flex items-center gap-2 px-4 py-2 bg-purple-600 hover:bg-purple-700 text-white font-medium rounded-lg transition-colors"
            >
              <Save className="w-4 h-4" />
              {llmSaved ? 'Saved!' : 'Save LLM Settings'}
            </button>
          </div>
        </div>

        {/* LLM Usage Stats */}
        {llmStats && (
          <div className="bg-slate-800/50 backdrop-blur border border-slate-700 rounded-xl p-6">
            <div className="flex items-center justify-between mb-4">
              <div className="flex items-center gap-3">
                <Zap className="w-5 h-5 text-amber-400" />
                <h2 className="text-lg font-semibold text-white">Session Usage</h2>
              </div>
              <button
                onClick={resetLLMStats}
                className="flex items-center gap-1 px-3 py-1 text-sm text-slate-400 hover:text-white bg-slate-700 hover:bg-slate-600 rounded-lg transition-colors"
              >
                <RotateCcw className="w-3 h-3" />
                Reset
              </button>
            </div>

            <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
              <div className="bg-slate-900/50 rounded-lg p-4">
                <div className="text-2xl font-bold text-white">{llmStats.totalCalls}</div>
                <div className="text-xs text-slate-500">Total Calls</div>
              </div>
              <div className="bg-slate-900/50 rounded-lg p-4">
                <div className="text-2xl font-bold text-purple-400">{llmStats.totalTokens.toLocaleString()}</div>
                <div className="text-xs text-slate-500">Total Tokens</div>
              </div>
              <div className="bg-slate-900/50 rounded-lg p-4">
                <div className="text-2xl font-bold text-emerald-400">${llmStats.totalCostUSD.toFixed(4)}</div>
                <div className="text-xs text-slate-500">Estimated Cost</div>
              </div>
              <div className="bg-slate-900/50 rounded-lg p-4">
                <div className="text-sm font-mono text-white">
                  In: {llmStats.totalPromptTokens.toLocaleString()}
                </div>
                <div className="text-sm font-mono text-white">
                  Out: {llmStats.totalCompletionTokens.toLocaleString()}
                </div>
                <div className="text-xs text-slate-500 mt-1">Token Breakdown</div>
              </div>
            </div>
          </div>
        )}

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
          {saved ? 'Saved!' : 'Save API Settings'}
        </button>
      </div>
    </div>
  );
}
