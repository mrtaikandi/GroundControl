import { apiFetch, type ApiRequestBody, type ApiResponse } from '../client';

export function getRoles() {
  return apiFetch<ApiResponse<'ListRolesHandler'>>('/api/roles');
}

export function createRole(body: ApiRequestBody<'CreateRoleHandler'>) {
  return apiFetch<ApiResponse<'CreateRoleHandler'>>('/api/roles', { method: 'POST', body });
}

export function getRole(id: string) {
  return apiFetch<ApiResponse<'GetRoleHandler'>>(`/api/roles/${encodeURIComponent(id)}`);
}

export function updateRole(id: string, body: ApiRequestBody<'UpdateRoleHandler'>, version: string) {
  return apiFetch<ApiResponse<'UpdateRoleHandler'>>(`/api/roles/${encodeURIComponent(id)}`, { method: 'PUT', body, version });
}

export function deleteRole(id: string, version: string) {
  return apiFetch<ApiResponse<'DeleteRoleHandler'>>(`/api/roles/${encodeURIComponent(id)}`, { method: 'DELETE', expectType: 'empty', version });
}