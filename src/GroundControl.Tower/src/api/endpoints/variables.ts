import { apiFetch, type ApiQuery, type ApiRequestBody, type ApiResponse } from '../client';

export function getVariables(query?: ApiQuery<'ListVariablesHandler'>) {
  return apiFetch<ApiResponse<'ListVariablesHandler'>>('/api/variables', { query });
}

export function createVariable(body: ApiRequestBody<'CreateVariableHandler'>) {
  return apiFetch<ApiResponse<'CreateVariableHandler'>>('/api/variables', { method: 'POST', body });
}

export function getVariable(id: string, options: { decrypt?: boolean } = {}) {
  return apiFetch<ApiResponse<'GetVariableHandler'>>(`/api/variables/${encodeURIComponent(id)}`, {
    query: options.decrypt ? { decrypt: true } : undefined,
  });
}

export function updateVariable(id: string, body: ApiRequestBody<'UpdateVariableHandler'>, version: string) {
  return apiFetch<ApiResponse<'UpdateVariableHandler'>>(`/api/variables/${encodeURIComponent(id)}`, { method: 'PUT', body, version });
}

export function deleteVariable(id: string, version: string) {
  return apiFetch<ApiResponse<'DeleteVariableHandler'>>(`/api/variables/${encodeURIComponent(id)}`, { method: 'DELETE', expectType: 'empty', version });
}