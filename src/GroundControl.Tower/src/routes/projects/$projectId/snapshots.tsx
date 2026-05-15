import { createFileRoute } from '@tanstack/react-router';
import { useEffect, useMemo, useState } from 'react';
import { AlertDialog, AlertDialogAction, AlertDialogCancel, AlertDialogContent, AlertDialogDescription, AlertDialogFooter, AlertDialogHeader, AlertDialogTitle } from '@/components/ui/alert-dialog';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Skeleton } from '@/components/ui/skeleton';
import { Badge } from '@/components/tower/data/Badge';
import { CompareSnapshotPicker } from '@/components/tower/snapshots/CompareSnapshotPicker';
import { summarizeChanges } from '@/components/tower/snapshots/PublishModal';
import { SnapshotDiffView } from '@/components/tower/snapshots/SnapshotDiffView';
import { SnapshotJsonView } from '@/components/tower/snapshots/SnapshotJsonView';
import { snapshotToDocument } from '@/lib/snapshot-document';
import { formatUserId } from '@/lib/user';
import { cn } from '@/lib/utils';
import { useProjects } from '@/queries/useProjects';
import { useActivateSnapshot, useSnapshotDetail, useSnapshots, type SnapshotDetail, type SnapshotSummary } from '@/queries/useSnapshots';
import { useTweaksStore } from '@/store/tweaks';

export const Route = createFileRoute('/projects/$projectId/snapshots')({
  component: SnapshotsRoute,
});

function SnapshotsRoute() {
  const { projectId } = Route.useParams();
  const snapshots = useSnapshots(projectId);
  const projects = useProjects();
  const snapshotViewMode = useTweaksStore((state) => state.snapshotViewMode);
  const setSnapshotViewMode = useTweaksStore((state) => state.setSnapshotViewMode);
  const [selectedSnapshotId, setSelectedSnapshotId] = useState<string | undefined>();
  const [compareSnapshotId, setCompareSnapshotId] = useState<string | undefined>();
  const [detailExpanded, setDetailExpanded] = useState(false);
  const [activatingSnapshot, setActivatingSnapshot] = useState<SnapshotSummary | undefined>();
  const items = snapshots.data?.data ?? [];
  const project = projects.data?.data.find((candidate) => candidate.id === projectId);
  const projectName = project?.name ?? '—';
  const activeSnapshotId = project?.activeSnapshotId || undefined;
  const selectedSnapshot = items.find((snapshot) => snapshot.id === selectedSnapshotId) ?? items[0];
  const selectedIsActive = Boolean(selectedSnapshot && activeSnapshotId && selectedSnapshot.id === activeSnapshotId);
  const selectedIndex = items.findIndex((snapshot) => snapshot.id === selectedSnapshot?.id);
  const previousSummary = selectedIndex >= 0 ? items[selectedIndex + 1] : undefined;
  // Default the compare target to the active snapshot when it differs from the one being viewed,
  // otherwise fall back to the previous snapshot. The picker can override this at any time.
  const defaultCompareId = activeSnapshotId && activeSnapshotId !== selectedSnapshot?.id ? activeSnapshotId : previousSummary?.id;
  const effectiveCompareId = compareSnapshotId && items.some((snapshot) => snapshot.id === compareSnapshotId)
    ? compareSnapshotId
    : defaultCompareId;
  const compareSummary = items.find((snapshot) => snapshot.id === effectiveCompareId);
  const selectedDetail = useSnapshotDetail(projectId, selectedSnapshot?.id);
  const compareDetail = useSnapshotDetail(projectId, effectiveCompareId);
  const activateSnapshot = useActivateSnapshot(projectId);

  useEffect(() => {
    setSelectedSnapshotId(undefined);
    setCompareSnapshotId(undefined);
  }, [projectId]);

  useEffect(() => {
    if (!selectedSnapshotId && items[0]) {
      setSelectedSnapshotId(items[0].id);
    }
  }, [items, selectedSnapshotId]);

  const detailHeading = useMemo(() => {
    if (!selectedSnapshot) {
      return 'No Snapshot Selected';
    }

    const description = selectedSnapshot.description?.trim();
    return description ? `v${selectedSnapshot.snapshotVersion} — ${description}` : `v${selectedSnapshot.snapshotVersion}`;
  }, [selectedSnapshot]);

  const compareChangeSummary = useMemo(() => {
    if (snapshotViewMode !== 'compare') {
      return null;
    }

    return summarizeChanges(snapshotToDocument(compareDetail.data), snapshotToDocument(selectedDetail.data));
  }, [compareDetail.data, selectedDetail.data, snapshotViewMode]);

  const selectedSnapshotLabel = selectedSnapshot ? `v${selectedSnapshot.snapshotVersion}` : 'selected snapshot';
  const compareTargetLabel = compareSummary
    ? `v${compareSummary.snapshotVersion}${compareSummary.id === activeSnapshotId ? ' (active)' : ''}`
    : 'no snapshot selected';

  return (
    <div className="grid gap-4">
      {snapshots.isLoading ? <Skeleton className="h-96" /> : null}
      {!snapshots.isLoading && items.length === 0 ? <div className="rounded-xl border border-stroke-subtle bg-bg-surface p-8 text-center text-fg-caption">No snapshots have been published for this project.</div> : null}
      {items.length > 0 ? (
        <div className="grid gap-5 xl:grid-cols-[minmax(280px,340px)_minmax(0,1fr)]">
          <div className="overflow-hidden rounded-xl border border-stroke-subtle bg-bg-surface p-2">
            {items.map((snapshot) => (
              <SnapshotRow
                active={snapshot.id === activeSnapshotId}
                key={snapshot.id}
                onSelect={() => setSelectedSnapshotId(snapshot.id)}
                selected={snapshot.id === selectedSnapshot?.id}
                snapshot={snapshot}
              />
            ))}
          </div>

          <div className="min-w-0 rounded-xl border border-stroke-subtle bg-bg-surface p-5">
            <div className="flex flex-wrap items-start justify-between gap-3">
              <div className="min-w-0 flex-1">
                <h3 className="text-[18px] font-semibold text-fg-heading [overflow-wrap:anywhere]">{detailHeading}</h3>
                {selectedSnapshot ? (
                  <p className="mt-1 text-[12.5px] text-fg-caption">
                    Published {formatRelative(selectedSnapshot.publishedAt)} by {formatUserId(selectedSnapshot.publishedBy)} · {selectedSnapshot.entryCount} resolved {selectedSnapshot.entryCount === 1 ? 'entry' : 'entries'} · project {projectName}
                  </p>
                ) : null}
              </div>
              <div className="flex shrink-0 items-center gap-2">
                <Button
                  disabled={!selectedSnapshot || selectedIsActive || activateSnapshot.isPending}
                  onClick={() => selectedSnapshot && setActivatingSnapshot(selectedSnapshot)}
                  size="sm"
                  type="button"
                  variant="secondary"
                >
                  {selectedIsActive ? 'Active' : 'Activate'}
                </Button>
                <Button
                  disabled={!selectedDetail.data}
                  onClick={() => selectedDetail.data && exportSnapshotJson(selectedDetail.data, projectName)}
                  size="sm"
                  type="button"
                  variant="secondary"
                >
                  Export JSON
                </Button>
              </div>
            </div>

            <div className="mt-5 flex flex-wrap items-center justify-between gap-3">
              <div className="inline-flex items-center gap-[3px] rounded-full bg-bg-container p-[3px]" role="group">
                <button
                  aria-pressed={snapshotViewMode === 'json'}
                  className={cn(
                    'ui-text-body-sm inline-flex h-7 items-center gap-1.5 rounded-full px-3 font-medium transition-colors duration-150 ease-out',
                    snapshotViewMode === 'json' ? 'bg-bg-surface font-semibold text-fg-heading shadow-ui-button-subtle' : 'text-fg-caption hover:text-fg-body',
                  )}
                  onClick={() => setSnapshotViewMode('json')}
                  type="button"
                >
                  JSON
                </button>
                <CompareSnapshotPicker
                  active={snapshotViewMode === 'compare'}
                  activeSnapshotId={activeSnapshotId}
                  compareSnapshotId={effectiveCompareId}
                  disabled={items.length < 2}
                  onSelect={(id) => {
                    setCompareSnapshotId(id);
                    setSnapshotViewMode('compare');
                  }}
                  selectedSnapshotId={selectedSnapshot?.id}
                  snapshots={items}
                />
              </div>
            </div>

            <div className="mt-4">{renderSnapshotView()}</div>
          </div>
        </div>
      ) : null}

      <Dialog open={detailExpanded} onOpenChange={setDetailExpanded}>
        <DialogContent className="w-[min(calc(100vw-32px),1280px)] p-0" showCloseButton={false}>
          <DialogHeader className="sr-only">
            <DialogTitle>{detailHeading}</DialogTitle>
            <DialogDescription>
              Expanded snapshot diff view.
            </DialogDescription>
          </DialogHeader>
          {renderExpandedDiffView()}
        </DialogContent>
      </Dialog>

      <AlertDialog open={Boolean(activatingSnapshot)} onOpenChange={(open) => !open && setActivatingSnapshot(undefined)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Activate Snapshot v{activatingSnapshot?.snapshotVersion}?</AlertDialogTitle>
            <AlertDialogDescription>Clients will immediately start receiving the config from this snapshot.</AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancel</AlertDialogCancel>
            <AlertDialogAction disabled={activateSnapshot.isPending} onClick={(event) => { event.preventDefault(); void confirmActivate(); }}>Activate</AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );

  async function confirmActivate() {
    if (!activatingSnapshot) {
      return;
    }

    await activateSnapshot.mutateAsync({ id: activatingSnapshot.id, version: activatingSnapshot.snapshotVersion.toString() });
    setActivatingSnapshot(undefined);
  }

  function renderSnapshotView() {
    if (snapshotViewMode === 'json') {
      return <SnapshotJsonView isLoading={selectedDetail.isLoading} snapshot={selectedDetail.data} />;
    }

    return renderExpandedDiffView(false);
  }

  function renderExpandedDiffView(expanded = true) {
    return (
      <SnapshotDiffView
        baseline={compareDetail.data}
        changeCount={countChanges(compareChangeSummary)}
        className={expanded ? 'max-h-[calc(100vh-32px)] rounded-2xl border-0' : 'min-h-[520px] max-h-[620px]'}
        isLoading={selectedDetail.isLoading || compareDetail.isLoading}
        onClose={expanded ? () => setDetailExpanded(false) : undefined}
        onExpand={expanded ? undefined : () => setDetailExpanded(true)}
        snapshot={selectedDetail.data}
        sourceLabel={selectedSnapshotLabel}
        targetLabel={compareTargetLabel}
      />
    );
  }
}

function SnapshotRow({ active, onSelect, selected, snapshot }: { active: boolean; onSelect: () => void; selected: boolean; snapshot: SnapshotSummary }) {
  return (
    <button
      className={cn(
        'block w-full rounded-lg px-3 py-2.5 text-left transition-colors',
        selected ? 'bg-bg-selected' : 'hover:bg-bg-container',
      )}
      onClick={onSelect}
      type="button"
    >
      <div className="flex items-start justify-between gap-3">
        <div className="flex items-center gap-2 min-w-0">
          <span className="font-mono text-[13px] font-semibold text-fg-heading">v{snapshot.snapshotVersion}</span>
          {active ? <Badge variant="success">active</Badge> : null}
        </div>
        <span className="shrink-0 font-mono text-[11.5px] text-fg-caption">{formatRelative(snapshot.publishedAt)}</span>
      </div>
      {snapshot.description?.trim() ? <div className="mt-1 text-[13px] text-fg-body [overflow-wrap:anywhere]">{snapshot.description.trim()}</div> : null}
      <div className="mt-2 text-[11.5px] text-fg-caption [overflow-wrap:anywhere]">
        {formatUserId(snapshot.publishedBy)} · {snapshot.entryCount} {snapshot.entryCount === 1 ? 'entry' : 'entries'}
      </div>
    </button>
  );
}

function exportSnapshotJson(snapshot: SnapshotDetail, projectName: string) {
  const payload = JSON.stringify(snapshot, null, 2);
  const blob = new Blob([payload], { type: 'application/json' });
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = `snapshot-v${snapshot.snapshotVersion}-${projectName}.json`;
  link.click();
  URL.revokeObjectURL(url);
}

function countChanges(summary: { additions: number; deletions: number; modifications: number } | null) {
  if (!summary) {
    return 0;
  }

  return summary.additions + summary.deletions + summary.modifications;
}

function formatRelative(value: string) {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return value;
  }

  const now = new Date();
  const time = `${String(date.getHours()).padStart(2, '0')}:${String(date.getMinutes()).padStart(2, '0')}`;
  const isSameDay = date.toDateString() === now.toDateString();
  if (isSameDay) {
    return `${time} today`;
  }

  const daysDiff = Math.floor((now.getTime() - date.getTime()) / (24 * 60 * 60 * 1000));
  if (daysDiff < 7 && daysDiff >= 0) {
    return `${date.toLocaleDateString(undefined, { weekday: 'short' })} ${time}`;
  }

  return `${date.toLocaleDateString(undefined, { day: '2-digit', month: 'short' })} ${time}`;
}
