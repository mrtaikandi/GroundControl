import { Link } from '@tanstack/react-router';
import { RotateCcw } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { useActivateSnapshot } from '@/queries/useSnapshots';
import type { ProjectStatus } from '@/queries/useProjectStatus';

interface ProjectStatusBarProps {
  onPublish: () => void;
  projectId: string;
  status: ProjectStatus;
}

export function ProjectStatusBar({ onPublish, projectId, status }: ProjectStatusBarProps) {
  if (status.kind === 'none') {
    return null;
  }

  return <NotLatestBar onPublish={onPublish} projectId={projectId} status={status} />;
}

function NotLatestBar({ onPublish, projectId, status }: { onPublish: () => void; projectId: string; status: ProjectStatus }) {
  const activate = useActivateSnapshot(projectId);
  const activeVersion = status.activeSnapshot?.snapshotVersion;
  const latestVersion = status.latestSnapshot?.snapshotVersion;

  function activateLatest() {
    if (!status.latestSnapshot) {
      return;
    }

    void activate.mutateAsync({ id: status.latestSnapshot.id, version: status.latestSnapshot.snapshotVersion.toString() });
  }

  return (
    <div className="flex flex-wrap items-center gap-3 rounded-xl border border-stroke-subtle bg-badge-warning-bg px-4 py-3" role="status">
      <span className="grid size-7 place-items-center rounded-full bg-bg-surface text-badge-warning-fg">
        <RotateCcw aria-hidden="true" className="size-3.5" strokeWidth={1.8} />
      </span>
      <div className="flex min-w-0 flex-1 flex-wrap items-baseline gap-x-2 gap-y-1">
        <span className="text-[12.5px] font-semibold text-badge-warning-fg">Rollback Active</span>
        <span className="text-[12.5px] text-fg-body">
          — <span className="font-mono font-semibold text-fg-heading">v{activeVersion}</span> is being served, but{' '}
          <span className="font-mono font-semibold text-fg-heading">v{latestVersion}</span> is the latest snapshot.
        </span>
        <Link
          className="text-[12.5px] font-medium text-fg-link transition-colors hover:underline"
          params={{ projectId }}
          to="/projects/$projectId/snapshots"
        >
          Open snapshots →
        </Link>
      </div>
      <div className="flex shrink-0 items-center gap-2">
        <Button disabled={!status.latestSnapshot || activate.isPending} onClick={activateLatest} size="sm" type="button" variant="secondary">
          Activate v{latestVersion}
        </Button>
        <Button onClick={onPublish} size="sm" type="button">Publish v{status.nextSnapshotVersion}</Button>
      </div>
    </div>
  );
}
