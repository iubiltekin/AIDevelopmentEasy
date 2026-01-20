import type { 
  StoryDto, 
  PipelineStatusDto, 
  CreateStoryRequest,
  PipelinePhase,
  CodebaseDto,
  CreateCodebaseRequest,
  ProjectSummaryDto,
  RequirementContextDto,
  PipelineContextDto,
  RetryInfoDto,
  RetryAction,
  RequirementDto,
  RequirementDetailDto,
  CreateRequirementRequest,
  WizardStatusDto,
  SubmitAnswersRequest,
  CreateStoriesRequest,
  // Knowledge types
  KnowledgeEntryDto,
  SuccessfulPatternDto,
  CommonErrorDto,
  ProjectTemplateDto,
  KnowledgeStatsDto,
  PatternSearchResultDto,
  ErrorMatchResultDto,
  CreatePatternRequest,
  CreateErrorRequest,
  SearchKnowledgeRequest,
  KnowledgeCategory,
  PatternSubcategory,
  ErrorType
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

  getRequirementContext: async (id: string): Promise<RequirementContextDto> => {
    const response = await fetch(`${API_BASE}/codebases/${id}/context/requirement`);
    return handleResponse<RequirementContextDto>(response);
  },

  getPipelineContext: async (id: string): Promise<PipelineContextDto> => {
    const response = await fetch(`${API_BASE}/codebases/${id}/context/pipeline`);
    return handleResponse<PipelineContextDto>(response);
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

// Requirements API
export const requirementsApi = {
  getAll: async (): Promise<RequirementDto[]> => {
    const response = await fetch(`${API_BASE}/requirements`);
    return handleResponse<RequirementDto[]>(response);
  },

  getById: async (id: string): Promise<RequirementDetailDto> => {
    const response = await fetch(`${API_BASE}/requirements/${id}`);
    return handleResponse<RequirementDetailDto>(response);
  },

  create: async (request: CreateRequirementRequest): Promise<RequirementDto> => {
    const response = await fetch(`${API_BASE}/requirements`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request)
    });
    return handleResponse<RequirementDto>(response);
  },

  update: async (id: string, request: { title?: string; rawContent?: string; type?: number; codebaseId?: string | null }): Promise<RequirementDto> => {
    const response = await fetch(`${API_BASE}/requirements/${id}`, {
      method: 'PUT',
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

  // Wizard operations
  startWizard: async (id: string, autoApproveAll: boolean = false): Promise<WizardStatusDto> => {
    const response = await fetch(`${API_BASE}/requirements/${id}/wizard/start`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ autoApproveAll })
    });
    return handleResponse<WizardStatusDto>(response);
  },

  getWizardStatus: async (id: string): Promise<WizardStatusDto | null> => {
    const response = await fetch(`${API_BASE}/requirements/${id}/wizard/status`);
    if (response.status === 404) {
      return null;
    }
    return handleResponse<WizardStatusDto>(response);
  },

  approvePhase: async (id: string, approved: boolean = true, comment?: string): Promise<WizardStatusDto> => {
    const response = await fetch(`${API_BASE}/requirements/${id}/wizard/approve`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ approved, comment })
    });
    return handleResponse<WizardStatusDto>(response);
  },

  submitAnswers: async (id: string, request: SubmitAnswersRequest): Promise<WizardStatusDto> => {
    const response = await fetch(`${API_BASE}/requirements/${id}/wizard/answers`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request)
    });
    return handleResponse<WizardStatusDto>(response);
  },

  createStories: async (id: string, request: CreateStoriesRequest): Promise<WizardStatusDto> => {
    const response = await fetch(`${API_BASE}/requirements/${id}/wizard/stories`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request)
    });
    return handleResponse<WizardStatusDto>(response);
  },

  cancelWizard: async (id: string): Promise<WizardStatusDto> => {
    const response = await fetch(`${API_BASE}/requirements/${id}/wizard/cancel`, {
      method: 'POST'
    });
    return handleResponse<WizardStatusDto>(response);
  },

  getStories: async (id: string): Promise<string[]> => {
    const response = await fetch(`${API_BASE}/requirements/${id}/stories`);
    return handleResponse<string[]>(response);
  }
};

// ════════════════════════════════════════════════════════════════════════════
// Knowledge Base API
// ════════════════════════════════════════════════════════════════════════════

export const knowledgeApi = {
  // General CRUD
  getAll: async (category?: KnowledgeCategory): Promise<KnowledgeEntryDto[]> => {
    const params = category !== undefined ? `?category=${category}` : '';
    const response = await fetch(`${API_BASE}/knowledge${params}`);
    return handleResponse<KnowledgeEntryDto[]>(response);
  },

  getById: async (id: string): Promise<KnowledgeEntryDto> => {
    const response = await fetch(`${API_BASE}/knowledge/${id}`);
    return handleResponse<KnowledgeEntryDto>(response);
  },

  delete: async (id: string): Promise<void> => {
    const response = await fetch(`${API_BASE}/knowledge/${id}`, {
      method: 'DELETE'
    });
    if (!response.ok) {
      throw new Error(`HTTP ${response.status}`);
    }
  },

  update: async (id: string, data: { title: string; description: string; tags: string[]; context?: string }): Promise<KnowledgeEntryDto> => {
    const response = await fetch(`${API_BASE}/knowledge/${id}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(data)
    });
    return handleResponse<KnowledgeEntryDto>(response);
  },

  // Patterns
  getPatterns: async (subcategory?: PatternSubcategory): Promise<SuccessfulPatternDto[]> => {
    const params = subcategory !== undefined ? `?subcategory=${subcategory}` : '';
    const response = await fetch(`${API_BASE}/knowledge/patterns${params}`);
    return handleResponse<SuccessfulPatternDto[]>(response);
  },

  getPattern: async (id: string): Promise<SuccessfulPatternDto> => {
    const response = await fetch(`${API_BASE}/knowledge/patterns/${id}`);
    return handleResponse<SuccessfulPatternDto>(response);
  },

  createPattern: async (request: CreatePatternRequest): Promise<SuccessfulPatternDto> => {
    const response = await fetch(`${API_BASE}/knowledge/patterns`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request)
    });
    return handleResponse<SuccessfulPatternDto>(response);
  },

  findSimilarPatterns: async (problemDescription: string, limit: number = 5): Promise<PatternSearchResultDto> => {
    const response = await fetch(`${API_BASE}/knowledge/patterns/search`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ problemDescription, limit })
    });
    return handleResponse<PatternSearchResultDto>(response);
  },

  // Errors
  getErrors: async (errorType?: ErrorType): Promise<CommonErrorDto[]> => {
    const params = errorType !== undefined ? `?errorType=${errorType}` : '';
    const response = await fetch(`${API_BASE}/knowledge/errors${params}`);
    return handleResponse<CommonErrorDto[]>(response);
  },

  getError: async (id: string): Promise<CommonErrorDto> => {
    const response = await fetch(`${API_BASE}/knowledge/errors/${id}`);
    return handleResponse<CommonErrorDto>(response);
  },

  createError: async (request: CreateErrorRequest): Promise<CommonErrorDto> => {
    const response = await fetch(`${API_BASE}/knowledge/errors`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request)
    });
    return handleResponse<CommonErrorDto>(response);
  },

  findMatchingError: async (errorMessage: string, errorType?: ErrorType): Promise<ErrorMatchResultDto> => {
    const response = await fetch(`${API_BASE}/knowledge/errors/match`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ errorMessage, errorType })
    });
    return handleResponse<ErrorMatchResultDto>(response);
  },

  // Templates
  getTemplates: async (type?: string): Promise<ProjectTemplateDto[]> => {
    const params = type ? `?type=${encodeURIComponent(type)}` : '';
    const response = await fetch(`${API_BASE}/knowledge/templates${params}`);
    return handleResponse<ProjectTemplateDto[]>(response);
  },

  getTemplate: async (id: string): Promise<ProjectTemplateDto> => {
    const response = await fetch(`${API_BASE}/knowledge/templates/${id}`);
    return handleResponse<ProjectTemplateDto>(response);
  },

  // Search
  search: async (request: SearchKnowledgeRequest): Promise<KnowledgeEntryDto[]> => {
    const response = await fetch(`${API_BASE}/knowledge/search`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request)
    });
    return handleResponse<KnowledgeEntryDto[]>(response);
  },

  getTags: async (): Promise<string[]> => {
    const response = await fetch(`${API_BASE}/knowledge/tags`);
    return handleResponse<string[]>(response);
  },

  // Usage tracking
  recordUsage: async (id: string): Promise<void> => {
    const response = await fetch(`${API_BASE}/knowledge/${id}/usage`, {
      method: 'POST'
    });
    if (!response.ok) {
      throw new Error(`HTTP ${response.status}`);
    }
  },

  updateSuccess: async (id: string, wasSuccessful: boolean): Promise<void> => {
    const response = await fetch(`${API_BASE}/knowledge/${id}/success?wasSuccessful=${wasSuccessful}`, {
      method: 'POST'
    });
    if (!response.ok) {
      throw new Error(`HTTP ${response.status}`);
    }
  },

  verify: async (id: string): Promise<void> => {
    const response = await fetch(`${API_BASE}/knowledge/${id}/verify`, {
      method: 'POST'
    });
    if (!response.ok) {
      throw new Error(`HTTP ${response.status}`);
    }
  },

  // Statistics
  getStats: async (): Promise<KnowledgeStatsDto> => {
    const response = await fetch(`${API_BASE}/knowledge/stats`);
    return handleResponse<KnowledgeStatsDto>(response);
  }
};
