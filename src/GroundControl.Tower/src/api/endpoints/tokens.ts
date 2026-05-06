import { apiFetch, type ApiRequestBody, type ApiResponse } from '../client';

export function getTokens() {
  return apiFetch<ApiResponse<'ListPatsHandler'>>('/api/personal-access-tokens');
}

export function createToken(body: ApiRequestBody<'CreatePatHandler'>) {
  return apiFetch<ApiResponse<'CreatePatHandler'>>('/api/personal-access-tokens', { method: 'POST', body });
}

export function getToken(id: string) {
  return apiFetch<ApiResponse<'GetPatHandler'>>(`/api/personal-access-tokens/${encodeURIComponent(id)}`);
}

export function revokeToken(id: string) {
  return apiFetch<ApiResponse<'RevokePatHandler'>>(`/api/personal-access-tokens/${encodeURIComponent(id)}`, { method: 'DELETE', expectType: 'empty' });
}