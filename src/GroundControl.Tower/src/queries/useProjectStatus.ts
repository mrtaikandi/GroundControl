import { useMemo } from 'react';
import { useProjects } from '@/queries/useProjects';
import { useSnapshots, type SnapshotSummary } from '@/queries/useSnapshots';

export type ProjectStatusKind = 'none' | 'not-latest';

export interface ProjectStatus {
  activeSnapshot: SnapshotSummary | undefined;
  isLoading: boolean;
  kind: ProjectStatusKind;
  latestSnapshot: SnapshotSummary | undefined;
  nextSnapshotVersion: number;
}

export function useProjectStatus(projectId: string): ProjectStatus {
  const projects = useProjects();
  const project = projects.data?.data.find((candidate) => candidate.id === projectId);
  const activeSnapshotId = project?.activeSnapshotId || undefined;
  const snapshots = useSnapshots(projectId);
  const items = snapshots.data?.data ?? [];
  const latestSnapshot = items[0];
  const activeSnapshot = activeSnapshotId ? items.find((snapshot) => snapshot.id === activeSnapshotId) : undefined;

  return useMemo<ProjectStatus>(() => {
    const isLoading = projects.isLoading || snapshots.isLoading;
    const latestVersion = latestSnapshot ? Number(latestSnapshot.snapshotVersion) || 0 : 0;
    const nextSnapshotVersion = latestVersion + 1;
    const kind: ProjectStatusKind = latestSnapshot && activeSnapshot && activeSnapshot.id !== latestSnapshot.id ? 'not-latest' : 'none';

    return {
      activeSnapshot,
      isLoading,
      kind,
      latestSnapshot,
      nextSnapshotVersion,
    };
  }, [activeSnapshot, latestSnapshot, projects.isLoading, snapshots.isLoading]);
}
