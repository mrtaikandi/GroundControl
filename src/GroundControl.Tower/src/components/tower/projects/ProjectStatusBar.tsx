import { Link } from '@tanstack/react-router';
import { History, RotateCcw } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { totalChanges } from '@/lib/snapshot-diff';
import { useActivateSnapshot } from '@/queries/useSnapshots';
import type { ProjectStatus } from '@/queries/useProjectStatus';

interface ProjectStatusBarProps {
  onPublish: () => void;
  onReviewDiff: () => void;
  projectId: string;
  status: ProjectStatus;
}

export function ProjectStatusBar({ onPublish, onReviewDiff, projectId, status }: ProjectStatusBarProps) {
  if (status.kind === 'none') {
    return null;
  }

  if (status.kind === 'draft') {
    return <DraftBar onPublish={onPublish} onReviewDiff={onReviewDiff} status={status} />;
  }

  return <NotLatestBar onPublish={onPublish} projectId={projectId} status={status} />;
}

function DraftBar({ onPublish, onReviewDiff, status }: { onPublish: () => void; onReviewDiff: () => void; status: ProjectStatus }) {
  const count = totalChanges(status.activeChanges);
  const sinceVersion = status.latestSnapshot?.snapshotVersion ?? status.activeSnapshot?.snapshotVersion;

  return (
    <div className="flex flex-wrap items-center gap-3 rounded-xl border border-stroke-subtle bg-badge-info-bg px-4 py-3" role="status">
      <span className="grid size-7 place-items-center rounded-full bg-bg-surface text-badge-info-fg">
        <History aria-hidden="true" className="size-3.5" strokeWidth={1.8} />
      </span>
      <div className="flex min-w-0 flex-1 items-center text-[12.5px]">
        <span className="font-semibold text-badge-info-fg">{count} draft {count === 1 ? 'change' : 'changes'}</span>
        {sinceVersion !== undefined ? <span className="text-fg-caption">&nbsp;since v{sinceVersion}</span> : null}
      </div>
      <div className="flex shrink-0 items-center gap-2">
        <Button onClick={onReviewDiff} size="sm" type="button" variant="secondary">Review diff</Button>
        <Button onClick={onPublish} size="sm" type="button">Publish v{status.nextSnapshotVersion}</Button>
      </div>
    </div>
  );
}

function NotLatestBar({ onPublish, projectId, status }: { onPublish: () => void; projectId: string; status: ProjectStatus }) {
  const activate = useActivateSnapshot(projectId);
  const activeVersion = status.activeSnapshot?.snapshotVersion;
  const latestVersion = status.latestSnapshot?.snapshotVersion;
  const hasConfigChanges = totalChanges(status.latestChanges) > 0;

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
        <span className="text-[12.5px] font-semibold text-badge-warning-fg">Rollback active</span>
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
        {hasConfigChanges ? (
          <Button onClick={onPublish} size="sm" type="button">Publish v{status.nextSnapshotVersion}</Button>
        ) : null}
      </div>
    </div>
  );
}
