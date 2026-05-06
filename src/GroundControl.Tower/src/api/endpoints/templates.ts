import { apiFetch, type ApiQuery, type ApiRequestBody, type ApiResponse } from '../client';

export function getTemplates(query?: ApiQuery<'ListTemplatesHandler'>) {
  return apiFetch<ApiResponse<'ListTemplatesHandler'>>('/api/templates', { query });
}

export function createTemplate(body: ApiRequestBody<'CreateTemplateHandler'>) {
  return apiFetch<ApiResponse<'CreateTemplateHandler'>>('/api/templates', { method: 'POST', body });
}

export function getTemplate(id: string) {
  return apiFetch<ApiResponse<'GetTemplateHandler'>>(`/api/templates/${encodeURIComponent(id)}`);
}

export function updateTemplate(id: string, body: ApiRequestBody<'UpdateTemplateHandler'>, version: string) {
  return apiFetch<ApiResponse<'UpdateTemplateHandler'>>(`/api/templates/${encodeURIComponent(id)}`, { method: 'PUT', body, version });
}

export function deleteTemplate(id: string, version: string) {
  return apiFetch<ApiResponse<'DeleteTemplateHandler'>>(`/api/templates/${encodeURIComponent(id)}`, { method: 'DELETE', expectType: 'empty', version });
}