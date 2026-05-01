import { JsonDiff } from '@/components/tower/code/JsonDiff';
import { Skeleton } from '@/components/ui/skeleton';
import { snapshotToDocument } from '@/lib/snapshot-document';
import type { SnapshotDetail } from '@/queries/useSnapshots';

interface SnapshotDiffViewProps {
  activeSnapshot?: SnapshotDetail;
  isLoading?: boolean;
  snapshot?: SnapshotDetail;
}

export function SnapshotDiffView({ activeSnapshot, isLoading = false, snapshot }: SnapshotDiffViewProps) {
  if (isLoading) {
    return <Skeleton className="h-[520px]" />;
  }

  return (
    <div className="grid gap-3">
      <div className="rounded-lg bg-bg-container px-3 py-2 font-mono text-[11.5px] text-fg-caption">Active → This snapshot</div>
      <JsonDiff after={snapshotToDocument(snapshot)} before={snapshotToDocument(activeSnapshot)} className="min-h-[520px] border border-stroke-subtle bg-bg-container" mode="unified" />
    </div>
  );
}