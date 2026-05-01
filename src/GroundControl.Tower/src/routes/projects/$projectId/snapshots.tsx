import { createFileRoute } from '@tanstack/react-router';
import { Maximize2 } from 'lucide-react';
import { useEffect, useMemo, useState } from 'react';
import { AlertDialog, AlertDialogAction, AlertDialogCancel, AlertDialogContent, AlertDialogDescription, AlertDialogFooter, AlertDialogHeader, AlertDialogTitle } from '@/components/ui/alert-dialog';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Skeleton } from '@/components/ui/skeleton';
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from '@/components/ui/tooltip';
import { Badge } from '@/components/tower/data/Badge';
import { InlineCode } from '@/components/tower/data/InlineCode';
import { SegmentedControl } from '@/components/tower/data/SegmentedControl';
import { PublishModal } from '@/components/tower/snapshots/PublishModal';
import { SnapshotDiffView } from '@/components/tower/snapshots/SnapshotDiffView';
import { SnapshotJsonDiffView } from '@/components/tower/snapshots/SnapshotJsonDiffView';
import { SnapshotJsonView } from '@/components/tower/snapshots/SnapshotJsonView';
import { useProjects } from '@/queries/useProjects';
import { useActivateSnapshot, useSnapshotDetail, useSnapshots, type SnapshotSummary } from '@/queries/useSnapshots';
import { useTweaksStore } from '@/store/tweaks';
import { formatUserId } from '@/lib/user';

const snapshotViewOptions = [{ label: 'JSON', value: 'json' }, { label: 'Diff', value: 'diff' }, { label: 'JSON diff', value: 'json-diff' }] as const;

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
  const [publishing, setPublishing] = useState(false);
  const [detailExpanded, setDetailExpanded] = useState(false);
  const [activatingSnapshot, setActivatingSnapshot] = useState<SnapshotSummary | undefined>();
  const items = snapshots.data?.data ?? [];
  const project = projects.data?.data.find((candidate) => candidate.id === projectId);
  const activeSnapshotId = project?.activeSnapshotId || undefined;
  const selectedSnapshot = items.find((snapshot) => snapshot.id === selectedSnapshotId) ?? items[0];
  const selectedIsActive = Boolean(selectedSnapshot && activeSnapshotId && selectedSnapshot.id === activeSnapshotId);
  const selectedIndex = items.findIndex((snapshot) => snapshot.id === selectedSnapshot?.id);
  const previousSnapshot = selectedIndex >= 0 ? items[selectedIndex + 1] : undefined;
  const selectedDetail = useSnapshotDetail(projectId, selectedSnapshot?.id);
  const activeDetail = useSnapshotDetail(projectId, activeSnapshotId);
  const previousDetail = useSnapshotDetail(projectId, previousSnapshot?.id);
  const activateSnapshot = useActivateSnapshot(projectId);

  useEffect(() => {
    if (!selectedSnapshotId && items[0]) {
      setSelectedSnapshotId(items[0].id);
    }
  }, [items, selectedSnapshotId, setSelectedSnapshotId]);

  const detailTitle = useMemo(() => selectedSnapshot ? `v${selectedSnapshot.snapshotVersion}` : 'No snapshot selected', [selectedSnapshot]);

  return (
    <div className="grid gap-8">
      <div className="flex items-start justify-between gap-4">
        <div>
          <h1 className="text-[34px] font-bold leading-tight text-fg-heading">Snapshots</h1>
          <p className="mt-2 text-[14.5px] text-fg-caption">Inspect immutable resolved configuration captures</p>
        </div>
        <div className="flex flex-wrap justify-end gap-3">
          <SegmentedControl onChange={setSnapshotViewMode} options={[...snapshotViewOptions]} value={snapshotViewMode} />
          <TooltipProvider>
            <Tooltip>
              <TooltipTrigger asChild>
                <span><Button disabled={selectedIsActive} onClick={() => setPublishing(true)} type="button">Publish snapshot</Button></span>
              </TooltipTrigger>
              {selectedIsActive ? <TooltipContent>Selected snapshot is already active</TooltipContent> : null}
            </Tooltip>
          </TooltipProvider>
        </div>
      </div>

      {snapshots.isLoading ? <Skeleton className="h-96" /> : null}
      {!snapshots.isLoading && items.length === 0 ? <div className="rounded-xl border border-stroke-subtle bg-bg-surface p-8 text-center text-fg-caption">No snapshots have been published for this project.</div> : null}
      {items.length > 0 ? (
        <div className="grid gap-5 xl:grid-cols-[1fr_460px]">
          <div className="overflow-hidden rounded-xl border border-stroke-subtle bg-bg-surface">
            <div className="grid grid-cols-[90px_96px_170px_1fr_auto] gap-3 border-b border-stroke-subtle px-4 py-3 text-[11.5px] font-medium uppercase text-fg-caption">
              <div>Version</div>
              <div>State</div>
              <div>Published</div>
              <div>Published by</div>
              <div />
            </div>
            {items.map((snapshot) => <SnapshotRow active={snapshot.id === activeSnapshotId} key={snapshot.id} onActivate={() => setActivatingSnapshot(snapshot)} onSelect={() => setSelectedSnapshotId(snapshot.id)} selected={snapshot.id === selectedSnapshot?.id} snapshot={snapshot} />)}
          </div>
          <div className="min-w-0 rounded-xl border border-stroke-subtle bg-bg-surface p-5">
            <div className="mb-4 flex items-start justify-between gap-3">
              <div className="min-w-0">
                <div className="text-[11px] font-medium uppercase text-fg-caption">Snapshot detail</div>
                <h2 className="mt-1 font-mono text-[19px] font-semibold text-fg-heading">{detailTitle}</h2>
                {selectedSnapshot ? <p className="mt-1 text-[12.5px] text-fg-caption">{selectedSnapshot.entryCount} entries · {selectedSnapshot.description || 'No publication comment'}</p> : null}
              </div>
              <button
                aria-label="Expand snapshot detail"
                className="grid size-8 shrink-0 place-items-center rounded-lg text-fg-icon-subtle transition-colors hover:bg-bg-container hover:text-fg-body"
                disabled={!selectedSnapshot}
                onClick={() => setDetailExpanded(true)}
                type="button"
              >
                <Maximize2 aria-hidden="true" className="size-4" strokeWidth={1.8} />
              </button>
            </div>
            {renderSnapshotView()}
          </div>
        </div>
      ) : null}

      <Dialog open={detailExpanded} onOpenChange={setDetailExpanded}>
        <DialogContent className="w-[min(calc(100vw-32px),1100px)]">
          <DialogHeader className="pr-10">
            <DialogTitle>Snapshot {detailTitle}</DialogTitle>
            {selectedSnapshot ? <DialogDescription>{selectedSnapshot.entryCount} entries · {selectedSnapshot.description || 'No publication comment'}</DialogDescription> : null}
          </DialogHeader>
          <div className="flex justify-end">
            <SegmentedControl onChange={setSnapshotViewMode} options={[...snapshotViewOptions]} value={snapshotViewMode} />
          </div>
          <div className="max-h-[calc(100vh-220px)] overflow-auto">
            {renderSnapshotView()}
          </div>
        </DialogContent>
      </Dialog>

      <PublishModal activeSnapshotId={activeSnapshotId} onOpenChange={setPublishing} open={publishing} projectId={projectId} />
      <AlertDialog open={Boolean(activatingSnapshot)} onOpenChange={(open) => !open && setActivatingSnapshot(undefined)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Activate snapshot v{activatingSnapshot?.snapshotVersion}?</AlertDialogTitle>
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

    if (snapshotViewMode === 'diff') {
      return <SnapshotDiffView activeSnapshot={activeDetail.data} isLoading={selectedDetail.isLoading || activeDetail.isLoading} snapshot={selectedDetail.data} />;
    }

    return <SnapshotJsonDiffView isLoading={selectedDetail.isLoading || previousDetail.isLoading} previousSnapshot={previousDetail.data} snapshot={selectedDetail.data} />;
  }
}

function SnapshotRow({ active, onActivate, onSelect, selected, snapshot }: { active: boolean; onActivate: () => void; onSelect: () => void; selected: boolean; snapshot: SnapshotSummary }) {
  return (
    <div className={`group grid cursor-pointer grid-cols-[90px_96px_170px_1fr_auto] gap-3 px-4 py-3 text-[12.5px] transition-colors ${selected ? 'bg-bg-selected' : 'hover:bg-bg-container'}`} onClick={onSelect}>
      <div className="font-mono font-semibold text-fg-heading">v{snapshot.snapshotVersion}</div>
      <div>{active ? <Badge variant="success">active</Badge> : <Badge variant="neutral">stored</Badge>}</div>
      <div className="font-mono text-fg-caption">{formatDate(snapshot.publishedAt)}</div>
      <div className="min-w-0">
        <InlineCode>{formatUserId(snapshot.publishedBy)}</InlineCode>
        <div className="mt-1 truncate text-[11.5px] text-fg-caption">{snapshot.description || 'No comment'}</div>
      </div>
      <div className="flex justify-end gap-1 opacity-0 transition-opacity group-hover:opacity-100" onClick={(event) => event.stopPropagation()}>
        <Button disabled={active} onClick={onActivate} size="sm" type="button" variant="ghost">Set as active</Button>
        <Button onClick={onSelect} size="sm" type="button" variant="ghost">View</Button>
      </div>
    </div>
  );
}

function formatDate(value: string) {
  return new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value));
}
