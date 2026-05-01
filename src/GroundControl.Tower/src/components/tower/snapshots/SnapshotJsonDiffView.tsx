import { JsonDiff } from '@/components/tower/code/JsonDiff';
import { Skeleton } from '@/components/ui/skeleton';
import { snapshotToDocument } from '@/lib/snapshot-document';
import type { SnapshotDetail } from '@/queries/useSnapshots';

interface SnapshotJsonDiffViewProps {
  isLoading?: boolean;
  previousSnapshot?: SnapshotDetail;
  snapshot?: SnapshotDetail;
}

export function SnapshotJsonDiffView({ isLoading = false, previousSnapshot, snapshot }: SnapshotJsonDiffViewProps) {
  if (isLoading) {
    return <Skeleton className="h-[520px]" />;
  }

  return (
    <div className="grid gap-3">
      <div className="rounded-lg bg-bg-container px-3 py-2 font-mono text-[11.5px] text-fg-caption">Previous → This snapshot</div>
      <JsonDiff after={snapshotToDocument(snapshot)} before={snapshotToDocument(previousSnapshot)} className="min-h-[520px] border border-stroke-subtle bg-bg-container" mode="unified" />
    </div>
  );
}