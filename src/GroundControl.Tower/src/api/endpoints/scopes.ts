import { apiFetch, type ApiQuery, type ApiRequestBody, type ApiResponse } from '../client';

export function getScopes(query?: ApiQuery<'ListScopesHandler'>) {
  return apiFetch<ApiResponse<'ListScopesHandler'>>('/api/scopes', { query });
}

export function createScope(body: ApiRequestBody<'CreateScopeHandler'>) {
  return apiFetch<ApiResponse<'CreateScopeHandler'>>('/api/scopes', { method: 'POST', body });
}

export function getScope(id: string) {
  return apiFetch<ApiResponse<'GetScopeHandler'>>(`/api/scopes/${encodeURIComponent(id)}`);
}

export function updateScope(id: string, body: ApiRequestBody<'UpdateScopeHandler'>, version: string) {
  return apiFetch<ApiResponse<'UpdateScopeHandler'>>(`/api/scopes/${encodeURIComponent(id)}`, { method: 'PUT', body, version });
}

export function deleteScope(id: string, version: string) {
  return apiFetch<ApiResponse<'DeleteScopeHandler'>>(`/api/scopes/${encodeURIComponent(id)}`, { method: 'DELETE', expectType: 'empty', version });
}