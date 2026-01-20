import { BrowserRouter, Routes, Route } from 'react-router-dom';
import { Layout } from './components/Layout';
import { Stories } from './pages/Stories';
import { StoryDetail } from './pages/StoryDetail';
import { PipelineView } from './pages/PipelineView';
import { Settings } from './pages/Settings';
import { Codebases } from './pages/Codebases';
import { CodebaseDetail } from './pages/CodebaseDetail';
import Requirements from './pages/Requirements';
import RequirementDetail from './pages/RequirementDetail';
import Knowledge from './pages/Knowledge';
import KnowledgeDetail from './pages/KnowledgeDetail';
import KnowledgeCreate from './pages/KnowledgeCreate';

function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/" element={<Layout />}>
          <Route index element={<Stories />} />
          <Route path="stories/:id" element={<StoryDetail />} />
          <Route path="pipeline/:id" element={<PipelineView />} />
          <Route path="requirements" element={<Requirements />} />
          <Route path="requirements/:id" element={<RequirementDetail />} />
          <Route path="codebases" element={<Codebases />} />
          <Route path="codebases/:id" element={<CodebaseDetail />} />
          <Route path="knowledge" element={<Knowledge />} />
          <Route path="knowledge/new/:type" element={<KnowledgeCreate />} />
          <Route path="knowledge/:id" element={<KnowledgeDetail />} />
          <Route path="settings" element={<Settings />} />
        </Route>
      </Routes>
    </BrowserRouter>
  );
}

export default App;
