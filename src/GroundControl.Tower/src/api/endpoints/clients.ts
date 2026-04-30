import { apiFetch, type ApiQuery, type ApiRequestBody, type ApiResponse } from '../client';

export function getClients(projectId: string, query?: ApiQuery<'ListClientsHandler'>) {
  return apiFetch<ApiResponse<'ListClientsHandler'>>(`/api/projects/${encodeURIComponent(projectId)}/clients`, { query });
}

export function createClient(projectId: string, body: ApiRequestBody<'CreateClientHandler'>) {
  return apiFetch<ApiResponse<'CreateClientHandler'>>(`/api/projects/${encodeURIComponent(projectId)}/clients`, { method: 'POST', body });
}

export function getClient(projectId: string, id: string) {
  return apiFetch<ApiResponse<'GetClientHandler'>>(`/api/projects/${encodeURIComponent(projectId)}/clients/${encodeURIComponent(id)}`);
}

export function updateClient(projectId: string, id: string, body: ApiRequestBody<'UpdateClientHandler'>, version: string) {
  return apiFetch<ApiResponse<'UpdateClientHandler'>>(`/api/projects/${encodeURIComponent(projectId)}/clients/${encodeURIComponent(id)}`, { method: 'PUT', body, version });
}

export function deleteClient(projectId: string, id: string, version: string) {
  return apiFetch<ApiResponse<'DeleteClientHandler'>>(`/api/projects/${encodeURIComponent(projectId)}/clients/${encodeURIComponent(id)}`, { method: 'DELETE', expectType: 'empty', version });
}