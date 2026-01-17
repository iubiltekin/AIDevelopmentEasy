import type { 
  StoryDto, 
  PipelineStatusDto, 
  CreateStoryRequest,
  PipelinePhase,
  CodebaseDto,
  CreateCodebaseRequest,
  ProjectSummaryDto,
  RetryInfoDto,
  RetryAction
} from '../types';

const API_BASE = '/api';

async function handleResponse<T>(response: Response): Promise<T> {
  if (!response.ok) {
    const error = await response.text();
    throw new Error(error || `HTTP ${response.status}`);
  }
  
  const text = await response.text();
  return text ? JSON.parse(text) : ({} as T);
}

// Stories API
export const storiesApi = {
  getAll: async (): Promise<StoryDto[]> => {
    const response = await fetch(`${API_BASE}/stories`);
    return handleResponse<StoryDto[]>(response);
  },

  getById: async (id: string): Promise<StoryDto> => {
    const response = await fetch(`${API_BASE}/stories/${id}`);
    return handleResponse<StoryDto>(response);
  },

  getContent: async (id: string): Promise<string> => {
    const response = await fetch(`${API_BASE}/stories/${id}/content`);
    if (!response.ok) {
      throw new Error(`HTTP ${response.status}`);
    }
    return response.text(); // Content is plain text, not JSON
  },

  create: async (request: CreateStoryRequest): Promise<StoryDto> => {
    const response = await fetch(`${API_BASE}/stories`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request)
    });
    return handleResponse<StoryDto>(response);
  },

  delete: async (id: string): Promise<void> => {
    const response = await fetch(`${API_BASE}/stories/${id}`, {
      method: 'DELETE'
    });
    await handleResponse<void>(response);
  },

  reset: async (id: string, clearTasks: boolean = true): Promise<void> => {
    const response = await fetch(
      `${API_BASE}/stories/${id}/reset?clearTasks=${clearTasks}`,
      { method: 'POST' }
    );
    await handleResponse<void>(response);
  },

  updateContent: async (id: string, content: string): Promise<void> => {
    const response = await fetch(`${API_BASE}/stories/${id}/content`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ content })
    });
    await handleResponse<void>(response);
  }
};

// Pipeline API
export const pipelineApi = {
  start: async (storyId: string, autoApproveAll: boolean = false): Promise<PipelineStatusDto> => {
    const response = await fetch(`${API_BASE}/pipeline/${storyId}/start`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ autoApproveAll })
    });
    return handleResponse<PipelineStatusDto>(response);
  },

  getStatus: async (storyId: string): Promise<PipelineStatusDto> => {
    const response = await fetch(`${API_BASE}/pipeline/${storyId}/status`);
    return handleResponse<PipelineStatusDto>(response);
  },

  approvePhase: async (storyId: string, phase: PipelinePhase): Promise<void> => {
    const response = await fetch(
      `${API_BASE}/pipeline/${storyId}/approve/${phase}`,
      { method: 'POST' }
    );
    await handleResponse<void>(response);
  },

  rejectPhase: async (storyId: string, phase: PipelinePhase, reason?: string): Promise<void> => {
    const response = await fetch(
      `${API_BASE}/pipeline/${storyId}/reject/${phase}`,
      {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ reason })
      }
    );
    await handleResponse<void>(response);
  },

  cancel: async (storyId: string): Promise<void> => {
    const response = await fetch(
      `${API_BASE}/pipeline/${storyId}/cancel`,
      { method: 'POST' }
    );
    await handleResponse<void>(response);
  },

  getRunning: async (): Promise<string[]> => {
    const response = await fetch(`${API_BASE}/pipeline/running`);
    return handleResponse<string[]>(response);
  },

  getOutput: async (storyId: string): Promise<Record<string, string>> => {
    const response = await fetch(`${API_BASE}/pipeline/${storyId}/output`);
    return handleResponse<Record<string, string>>(response);
  },

  getReviewReport: async (storyId: string): Promise<string> => {
    const response = await fetch(`${API_BASE}/pipeline/${storyId}/review`);
    return handleResponse<string>(response);
  },

  // Retry endpoints
  approveRetry: async (storyId: string, action: RetryAction): Promise<void> => {
    const response = await fetch(`${API_BASE}/pipeline/${storyId}/retry`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ action })
    });
    await handleResponse<void>(response);
  },

  getRetryInfo: async (storyId: string): Promise<RetryInfoDto | null> => {
    const response = await fetch(`${API_BASE}/pipeline/${storyId}/retry`);
    return handleResponse<RetryInfoDto | null>(response);
  },

  // History endpoint - get completed pipeline details
  getHistory: async (storyId: string): Promise<PipelineStatusDto | null> => {
    const response = await fetch(`${API_BASE}/pipeline/${storyId}/history`);
    if (response.status === 404) {
      return null;
    }
    return handleResponse<PipelineStatusDto>(response);
  }
};

// Codebases API
export const codebasesApi = {
  getAll: async (): Promise<CodebaseDto[]> => {
    const response = await fetch(`${API_BASE}/codebases`);
    return handleResponse<CodebaseDto[]>(response);
  },

  getById: async (id: string): Promise<CodebaseDto> => {
    const response = await fetch(`${API_BASE}/codebases/${id}`);
    return handleResponse<CodebaseDto>(response);
  },

  getAnalysis: async (id: string): Promise<unknown> => {
    const response = await fetch(`${API_BASE}/codebases/${id}/analysis`);
    return handleResponse<unknown>(response);
  },

  getContext: async (id: string): Promise<string> => {
    const response = await fetch(`${API_BASE}/codebases/${id}/context`);
    if (!response.ok) {
      throw new Error(`HTTP ${response.status}`);
    }
    return response.text();
  },

  getProjects: async (id: string): Promise<ProjectSummaryDto[]> => {
    const response = await fetch(`${API_BASE}/codebases/${id}/projects`);
    return handleResponse<ProjectSummaryDto[]>(response);
  },

  create: async (request: CreateCodebaseRequest): Promise<CodebaseDto> => {
    const response = await fetch(`${API_BASE}/codebases`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request)
    });
    return handleResponse<CodebaseDto>(response);
  },

  analyze: async (id: string): Promise<void> => {
    const response = await fetch(`${API_BASE}/codebases/${id}/analyze`, {
      method: 'POST'
    });
    await handleResponse<void>(response);
  },

  delete: async (id: string): Promise<void> => {
    const response = await fetch(`${API_BASE}/codebases/${id}`, {
      method: 'DELETE'
    });
    await handleResponse<void>(response);
  }
};
