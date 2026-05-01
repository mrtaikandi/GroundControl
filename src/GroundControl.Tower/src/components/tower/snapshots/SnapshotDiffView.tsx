import { JsonDiff } from '@/components/tower/code/JsonDiff';
import { Skeleton } from '@/components/ui/skeleton';
import { snapshotToDocument } from '@/lib/snapshot-document';
import type { SnapshotDetail } from '@/queries/useSnapshots';

interface SnapshotDiffViewProps {
  activeSnapshot?: SnapshotDetail;
  changeCount: number;
  isLoading?: boolean;
  snapshot?: SnapshotDetail;
  targetLabel: string;
}

export function SnapshotDiffView({ activeSnapshot, changeCount, isLoading = false, snapshot, targetLabel }: SnapshotDiffViewProps) {
  if (isLoading) {
    return <Skeleton className="h-[520px]" />;
  }

  return (
    <div className="rounded-lg border border-stroke-subtle bg-bg-container">
      <div className="flex items-center justify-between gap-3 border-b border-stroke-subtle px-4 py-2.5 text-[12px] font-medium text-fg-caption">
        <span>Diff vs {targetLabel}</span>
        <span>{changeCount} {changeCount === 1 ? 'change' : 'changes'}</span>
      </div>
      <JsonDiff after={snapshotToDocument(snapshot)} before={snapshotToDocument(activeSnapshot)} className="min-h-[440px]" mode="unified" />
    </div>
  );
}
