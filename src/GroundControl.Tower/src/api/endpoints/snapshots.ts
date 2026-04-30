import { apiFetch, type ApiQuery, type ApiRequestBody, type ApiResponse } from '../client';

export function getSnapshots(projectId: string, query?: ApiQuery<'ListSnapshotsHandler'>) {
  return apiFetch<ApiResponse<'ListSnapshotsHandler'>>(`/api/projects/${encodeURIComponent(projectId)}/snapshots`, { query });
}

export function publishSnapshot(projectId: string, body: ApiRequestBody<'PublishSnapshotHandler'>) {
  return apiFetch<ApiResponse<'PublishSnapshotHandler'>>(`/api/projects/${encodeURIComponent(projectId)}/snapshots`, { method: 'POST', body });
}

export function activateSnapshot(projectId: string, id: string, version: string) {
  return apiFetch<ApiResponse<'ActivateSnapshotHandler'>>(`/api/projects/${encodeURIComponent(projectId)}/snapshots/${encodeURIComponent(id)}/activate`, { method: 'POST', version });
}

export function getSnapshot(projectId: string, id: string) {
  return apiFetch<ApiResponse<'GetSnapshotHandler'>>(`/api/projects/${encodeURIComponent(projectId)}/snapshots/${encodeURIComponent(id)}`);
}