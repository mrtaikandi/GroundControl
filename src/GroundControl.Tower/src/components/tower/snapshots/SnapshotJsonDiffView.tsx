import { JsonDiff } from '@/components/tower/code/JsonDiff';
import { Skeleton } from '@/components/ui/skeleton';
import { snapshotToDocument } from '@/lib/snapshot-document';
import type { SnapshotDetail } from '@/queries/useSnapshots';

interface SnapshotJsonDiffViewProps {
  changeCount: number;
  isLoading?: boolean;
  previousSnapshot?: SnapshotDetail;
  snapshot?: SnapshotDetail;
  targetLabel: string;
}

export function SnapshotJsonDiffView({ changeCount, isLoading = false, previousSnapshot, snapshot, targetLabel }: SnapshotJsonDiffViewProps) {
  if (isLoading) {
    return <Skeleton className="h-[520px]" />;
  }

  return (
    <div className="rounded-lg border border-stroke-subtle bg-bg-container">
      <div className="flex items-center justify-between gap-3 border-b border-stroke-subtle px-4 py-2.5 text-[12px] font-medium text-fg-caption">
        <span>Diff vs {targetLabel}</span>
        <span>{changeCount} {changeCount === 1 ? 'change' : 'changes'}</span>
      </div>
      <JsonDiff after={snapshotToDocument(snapshot)} before={snapshotToDocument(previousSnapshot)} className="min-h-[440px]" mode="unified" />
    </div>
  );
}
