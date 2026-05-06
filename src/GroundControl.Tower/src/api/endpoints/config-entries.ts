import { apiFetch, type ApiQuery, type ApiRequestBody, type ApiResponse } from '../client';

export function getConfigEntries(query?: ApiQuery<'ListConfigEntriesHandler'>) {
  return apiFetch<ApiResponse<'ListConfigEntriesHandler'>>('/api/config-entries', { query });
}

export function getConfigEntry(id: string, query?: ApiQuery<'GetConfigEntryHandler'>) {
  return apiFetch<ApiResponse<'GetConfigEntryHandler'>>(`/api/config-entries/${encodeURIComponent(id)}`, { query });
}

export function createConfigEntry(body: ApiRequestBody<'CreateConfigEntryHandler'>) {
  return apiFetch<ApiResponse<'CreateConfigEntryHandler'>>('/api/config-entries', { method: 'POST', body });
}

export function updateConfigEntry(id: string, body: ApiRequestBody<'UpdateConfigEntryHandler'>, version: string) {
  return apiFetch<ApiResponse<'UpdateConfigEntryHandler'>>(`/api/config-entries/${encodeURIComponent(id)}`, { method: 'PUT', body, version });
}

export function deleteConfigEntry(id: string, version: string) {
  return apiFetch<ApiResponse<'DeleteConfigEntryHandler'>>(`/api/config-entries/${encodeURIComponent(id)}`, { method: 'DELETE', expectType: 'empty', version });
}