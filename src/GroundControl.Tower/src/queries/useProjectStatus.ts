import { useMemo } from 'react';
import { buildResolvedDocument } from '@/lib/resolve-config';
import { snapshotToResolvedDocument } from '@/lib/snapshot-document';
import { diffDocuments, type ChangeSet } from '@/lib/snapshot-diff';
import { useProjects } from '@/queries/useProjects';
import { useResolvedConfig } from '@/queries/useResolvedConfig';
import { useSnapshotDetail, useSnapshots, type SnapshotSummary } from '@/queries/useSnapshots';

export type ProjectStatusKind = 'draft' | 'none' | 'not-latest';

export interface ProjectStatus {
  activeChanges: ChangeSet;
  activeSnapshot: SnapshotSummary | undefined;
  isLoading: boolean;
  kind: ProjectStatusKind;
  latestChanges: ChangeSet;
  latestSnapshot: SnapshotSummary | undefined;
  nextSnapshotVersion: number;
}

const EMPTY_CHANGES: ChangeSet = { additions: [], deletions: [], modifications: [] };

export function useProjectStatus(projectId: string): ProjectStatus {
  const projects = useProjects();
  const project = projects.data?.data.find((candidate) => candidate.id === projectId);
  const activeSnapshotId = project?.activeSnapshotId || undefined;
  const snapshots = useSnapshots(projectId);
  const items = snapshots.data?.data ?? [];
  const latestSnapshot = items[0];
  const activeSnapshot = activeSnapshotId ? items.find((snapshot) => snapshot.id === activeSnapshotId) : undefined;
  const activeDetail = useSnapshotDetail(projectId, activeSnapshotId);
  const latestDetail = useSnapshotDetail(projectId, latestSnapshot?.id);
  const resolvedConfig = useResolvedConfig(projectId, {});

  return useMemo<ProjectStatus>(() => {
    const isLoading = projects.isLoading || snapshots.isLoading || activeDetail.isLoading || latestDetail.isLoading || resolvedConfig.isLoading;
    const after = buildResolvedDocument(resolvedConfig.data ?? [], { maskSensitive: false });
    const activeBefore = snapshotToResolvedDocument(activeDetail.data, {}, { maskSensitive: false });
    const latestBefore = snapshotToResolvedDocument(latestDetail.data, {}, { maskSensitive: false });
    const activeChanges = activeDetail.data ? diffDocuments(activeBefore, after) : EMPTY_CHANGES;
    const latestChanges = latestDetail.data ? diffDocuments(latestBefore, after) : EMPTY_CHANGES;
    const latestVersion = latestSnapshot ? Number(latestSnapshot.snapshotVersion) || 0 : 0;
    const nextSnapshotVersion = latestVersion + 1;

    let kind: ProjectStatusKind = 'none';
    if (latestSnapshot && activeSnapshot && activeSnapshot.id !== latestSnapshot.id) {
      kind = 'not-latest';
    } else if (activeSnapshot && totalSize(activeChanges) > 0) {
      kind = 'draft';
    }

    return {
      activeChanges,
      activeSnapshot,
      isLoading,
      kind,
      latestChanges,
      latestSnapshot,
      nextSnapshotVersion,
    };
  }, [activeDetail.data, activeDetail.isLoading, activeSnapshot, latestDetail.data, latestDetail.isLoading, latestSnapshot, projects.isLoading, resolvedConfig.data, resolvedConfig.isLoading, snapshots.isLoading]);
}

function totalSize(changes: ChangeSet) {
  return changes.additions.length + changes.deletions.length + changes.modifications.length;
}
