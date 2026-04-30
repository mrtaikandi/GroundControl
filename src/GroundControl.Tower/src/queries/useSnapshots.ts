import { useQuery } from '@tanstack/react-query';
import { getSnapshot, getSnapshots } from '@/api/endpoints/snapshots';
import type { ApiResponse } from '@/api/client';

export type SnapshotSummary = NonNullable<ApiResponse<'ListSnapshotsHandler'>>['data'][number];
export type SnapshotDetail = NonNullable<ApiResponse<'GetSnapshotHandler'>>;

export function snapshotsQueryKey(projectId: string) {
  return ['projects', projectId, 'snapshots'] as const;
}

export function snapshotDetailQueryKey(projectId: string, snapshotId?: string) {
  return ['projects', projectId, 'snapshots', snapshotId] as const;
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