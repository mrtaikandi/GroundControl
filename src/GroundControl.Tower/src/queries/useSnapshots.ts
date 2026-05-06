import { useQuery } from '@tanstack/react-query';
import { activateSnapshot, getSnapshot, getSnapshots, previewSnapshot, publishSnapshot, type PreviewSnapshotResponse } from '@/api/endpoints/snapshots';
import type { ApiRequestBody, ApiResponse } from '@/api/client';
import { useConflictMutation } from '@/lib/mutations';
import { queryClient } from '@/lib/query-client';

export type SnapshotSummary = NonNullable<ApiResponse<'ListSnapshotsHandler'>>['data'][number];
export type SnapshotDetail = NonNullable<ApiResponse<'GetSnapshotHandler'>>;
export type PublishSnapshotRequest = ApiRequestBody<'PublishSnapshotHandler'> & { expectedHash?: null | string };
export type SnapshotPreview = PreviewSnapshotResponse;

export function snapshotsQueryKey(projectId: string) {
  return ['projects', projectId, 'snapshots'] as const;
}

export function snapshotDetailQueryKey(projectId: string, snapshotId?: string) {
  return ['projects', projectId, 'snapshots', snapshotId] as const;
}

export function snapshotPreviewQueryKey(projectId: string) {
  return ['projects', projectId, 'snapshot-preview'] as const;
}

export function useSnapshots(projectId: string) {
  return useQuery({
    enabled: !!projectId,
    queryFn: () => getSnapshots(projectId, { Limit: 100, SortField: 'publishedAt', SortOrder: 'desc' }),
    queryKey: snapshotsQueryKey(projectId),
  });
}

export function useSnapshotDetail(projectId: string, snapshotId?: string) {
  return useQuery({
    enabled: !!projectId && !!snapshotId,
    queryFn: () => getSnapshot(projectId, snapshotId!),
    queryKey: snapshotDetailQueryKey(projectId, snapshotId),
  });
}

export function useSnapshotPreview(projectId: string, options: { decrypt?: boolean; enabled?: boolean } = {}) {
  return useQuery({
    enabled: !!projectId && (options.enabled ?? true),
    queryFn: () => previewSnapshot(projectId, { decrypt: options.decrypt }),
    queryKey: [...snapshotPreviewQueryKey(projectId), options.decrypt === true] as const,
    refetchOnWindowFocus: false,
    staleTime: 0,
  });
}

export function usePublishSnapshot(projectId: string) {
  return useConflictMutation<PublishSnapshotRequest, SnapshotSummary>(
    (variables) => publishSnapshot(projectId, { description: variables.description ?? null, expectedHash: variables.expectedHash ?? null }),
    { onSuccess: () => invalidateSnapshotQueries(projectId) },
  );
}

export function useActivateSnapshot(projectId: string) {
  return useConflictMutation<{ id: string }, ApiResponse<'ActivateSnapshotHandler'>>(
    (variables) => activateSnapshot(projectId, variables.id, variables.version),
    { onSuccess: () => invalidateSnapshotQueries(projectId) },
  );
}

function invalidateSnapshotQueries(projectId: string) {
  void queryClient.invalidateQueries({ queryKey: snapshotsQueryKey(projectId) });
  void queryClient.invalidateQueries({ queryKey: ['projects'] });
}