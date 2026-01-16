import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { Layout } from './components/Layout';
import { Dashboard } from './pages/Dashboard';
import { RequirementDetail } from './pages/RequirementDetail';
import { NewRequirement } from './pages/NewRequirement';
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
          <Route path="requirements" element={<Navigate to="/" replace />} />
          <Route path="requirements/new" element={<NewRequirement />} />
          <Route path="requirements/:id" element={<RequirementDetail />} />
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
