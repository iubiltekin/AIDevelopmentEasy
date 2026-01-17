import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { Layout } from './components/Layout';
import { Dashboard } from './pages/Dashboard';
import { StoryDetail } from './pages/StoryDetail';
import { NewStory } from './pages/NewStory';
import { PipelineView } from './pages/PipelineView';
import { Settings } from './pages/Settings';
import { Codebases } from './pages/Codebases';
import { CodebaseDetail } from './pages/CodebaseDetail';

function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/" element={<Layout />}>
          <Route index element={<Dashboard />} />
          <Route path="stories" element={<Navigate to="/" replace />} />
          <Route path="stories/new" element={<NewStory />} />
          <Route path="stories/:id" element={<StoryDetail />} />
          <Route path="pipeline/:id" element={<PipelineView />} />
          <Route path="codebases" element={<Codebases />} />
          <Route path="codebases/:id" element={<CodebaseDetail />} />
          <Route path="settings" element={<Settings />} />
        </Route>
      </Routes>
    </BrowserRouter>
  );
}

export default App;
