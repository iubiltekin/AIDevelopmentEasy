import type { 
  RequirementDto, 
  PipelineStatusDto, 
  CreateRequirementRequest,
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

// Requirements API
export const requirementsApi = {
  getAll: async (): Promise<RequirementDto[]> => {
    const response = await fetch(`${API_BASE}/requirements`);
    return handleResponse<RequirementDto[]>(response);
  },

  getById: async (id: string): Promise<RequirementDto> => {
    const response = await fetch(`${API_BASE}/requirements/${id}`);
    return handleResponse<RequirementDto>(response);
  },

  getContent: async (id: string): Promise<string> => {
    const response = await fetch(`${API_BASE}/requirements/${id}/content`);
    if (!response.ok) {
      throw new Error(`HTTP ${response.status}`);
    }
    return response.text(); // Content is plain text, not JSON
  },

  create: async (request: CreateRequirementRequest): Promise<RequirementDto> => {
    const response = await fetch(`${API_BASE}/requirements`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request)
    });
    return handleResponse<RequirementDto>(response);
  },

  delete: async (id: string): Promise<void> => {
    const response = await fetch(`${API_BASE}/requirements/${id}`, {
      method: 'DELETE'
    });
    await handleResponse<void>(response);
  },

  reset: async (id: string, clearTasks: boolean = true): Promise<void> => {
    const response = await fetch(
      `${API_BASE}/requirements/${id}/reset?clearTasks=${clearTasks}`,
      { method: 'POST' }
    );
    await handleResponse<void>(response);
  }
};

// Pipeline API
export const pipelineApi = {
  start: async (requirementId: string, autoApproveAll: boolean = false): Promise<PipelineStatusDto> => {
    const response = await fetch(`${API_BASE}/pipeline/${requirementId}/start`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ autoApproveAll })
    });
    return handleResponse<PipelineStatusDto>(response);
  },

  getStatus: async (requirementId: string): Promise<PipelineStatusDto> => {
    const response = await fetch(`${API_BASE}/pipeline/${requirementId}/status`);
    return handleResponse<PipelineStatusDto>(response);
  },

  approvePhase: async (requirementId: string, phase: PipelinePhase): Promise<void> => {
    const response = await fetch(
      `${API_BASE}/pipeline/${requirementId}/approve/${phase}`,
      { method: 'POST' }
    );
    await handleResponse<void>(response);
  },

  rejectPhase: async (requirementId: string, phase: PipelinePhase, reason?: string): Promise<void> => {
    const response = await fetch(
      `${API_BASE}/pipeline/${requirementId}/reject/${phase}`,
      {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ reason })
      }
    );
    await handleResponse<void>(response);
  },

  cancel: async (requirementId: string): Promise<void> => {
    const response = await fetch(
      `${API_BASE}/pipeline/${requirementId}/cancel`,
      { method: 'POST' }
    );
    await handleResponse<void>(response);
  },

  getRunning: async (): Promise<string[]> => {
    const response = await fetch(`${API_BASE}/pipeline/running`);
    return handleResponse<string[]>(response);
  },

  getOutput: async (requirementId: string): Promise<Record<string, string>> => {
    const response = await fetch(`${API_BASE}/pipeline/${requirementId}/output`);
    return handleResponse<Record<string, string>>(response);
  },

  getReviewReport: async (requirementId: string): Promise<string> => {
    const response = await fetch(`${API_BASE}/pipeline/${requirementId}/review`);
    return handleResponse<string>(response);
  },

  // Retry endpoints
  approveRetry: async (requirementId: string, action: RetryAction): Promise<void> => {
    const response = await fetch(`${API_BASE}/pipeline/${requirementId}/retry`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ action })
    });
    await handleResponse<void>(response);
  },

  getRetryInfo: async (requirementId: string): Promise<RetryInfoDto | null> => {
    const response = await fetch(`${API_BASE}/pipeline/${requirementId}/retry`);
    return handleResponse<RetryInfoDto | null>(response);
  },

  // History endpoint - get completed pipeline details
  getHistory: async (requirementId: string): Promise<PipelineStatusDto | null> => {
    const response = await fetch(`${API_BASE}/pipeline/${requirementId}/history`);
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
