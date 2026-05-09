import { apiFetch, type ApiQuery, type ApiRequestBody, type ApiResponse } from '../client';

export function getProjects(query?: ApiQuery<'ListProjectsHandler'>) {
  return apiFetch<ApiResponse<'ListProjectsHandler'>>('/api/projects', { query });
}

export function getGroupedProjects(query?: ApiQuery<'ListGroupedProjectsHandler'>) {
  return apiFetch<ApiResponse<'ListGroupedProjectsHandler'>>('/api/projects/grouped', { query });
}

export function getProject(id: string) {
  return apiFetch<ApiResponse<'GetProjectHandler'>>(`/api/projects/${encodeURIComponent(id)}`);
}

export function createProject(body: ApiRequestBody<'CreateProjectHandler'>) {
  return apiFetch<ApiResponse<'CreateProjectHandler'>>('/api/projects', { method: 'POST', body });
}

export function updateProject(id: string, body: ApiRequestBody<'UpdateProjectHandler'>, version: string) {
  return apiFetch<ApiResponse<'UpdateProjectHandler'>>(`/api/projects/${encodeURIComponent(id)}`, { method: 'PUT', body, version });
}

export function deleteProject(id: string, version: string) {
  return apiFetch<ApiResponse<'DeleteProjectHandler'>>(`/api/projects/${encodeURIComponent(id)}`, { method: 'DELETE', expectType: 'empty', version });
}

export function addProjectTemplate(id: string, templateId: string, version: string) {
  return apiFetch<ApiResponse<'AddProjectTemplateHandler'>>(`/api/projects/${encodeURIComponent(id)}/templates/${encodeURIComponent(templateId)}`, { method: 'PUT', version });
}

export function removeProjectTemplate(id: string, templateId: string, version: string) {
  return apiFetch<ApiResponse<'RemoveProjectTemplateHandler'>>(`/api/projects/${encodeURIComponent(id)}/templates/${encodeURIComponent(templateId)}`, { method: 'DELETE', version });
}