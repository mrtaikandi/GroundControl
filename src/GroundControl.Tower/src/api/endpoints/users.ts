import { apiFetch, type ApiQuery, type ApiRequestBody, type ApiResponse } from '../client';

export function getUsers(query?: ApiQuery<'ListUsersHandler'>) {
  return apiFetch<ApiResponse<'ListUsersHandler'>>('/api/users', { query });
}

export function createUser(body: ApiRequestBody<'CreateUserHandler'>) {
  return apiFetch<ApiResponse<'CreateUserHandler'>>('/api/users', { method: 'POST', body });
}

export function getUser(id: string) {
  return apiFetch<ApiResponse<'GetUserHandler'>>(`/api/users/${encodeURIComponent(id)}`);
}

export function updateUser(id: string, body: ApiRequestBody<'UpdateUserHandler'>, version: string) {
  return apiFetch<ApiResponse<'UpdateUserHandler'>>(`/api/users/${encodeURIComponent(id)}`, { method: 'PUT', body, version });
}

export function deleteUser(id: string, version: string) {
  return apiFetch<ApiResponse<'DeleteUserHandler'>>(`/api/users/${encodeURIComponent(id)}`, { method: 'DELETE', expectType: 'empty', version });
}

export function changePassword(id: string, body: ApiRequestBody<'ChangePasswordHandler'>) {
  return apiFetch<ApiResponse<'ChangePasswordHandler'>>(`/api/users/${encodeURIComponent(id)}/password`, { method: 'POST', body, expectType: 'empty' });
}