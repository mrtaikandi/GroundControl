import { DiffLayoutToggle } from '@/components/tower/code/DiffLayoutToggle';
import { JsonDiff } from '@/components/tower/code/JsonDiff';
import { Skeleton } from '@/components/ui/skeleton';
import { snapshotToDocument } from '@/lib/snapshot-document';
import type { SnapshotDetail } from '@/queries/useSnapshots';
import { useTweaksStore } from '@/store/tweaks';

interface SnapshotDiffViewProps {
  baseline?: SnapshotDetail;
  changeCount: number;
  isLoading?: boolean;
  snapshot?: SnapshotDetail;
  targetLabel: string;
}

export function SnapshotDiffView({ baseline, changeCount, isLoading = false, snapshot, targetLabel }: SnapshotDiffViewProps) {
  const diffLayout = useTweaksStore((state) => state.diffLayout);

  if (isLoading) {
    return <Skeleton className="h-[520px]" />;
  }

  return (
    <div className="rounded-lg border border-stroke-subtle bg-bg-container">
      <div className="flex items-center justify-between gap-3 border-b border-stroke-subtle px-4 py-2 text-[12px] font-medium text-fg-caption">
        <span>Diff vs {targetLabel}</span>
        <div className="flex items-center gap-3">
          <span>{changeCount} {changeCount === 1 ? 'change' : 'changes'}</span>
          <DiffLayoutToggle size="sm" />
        </div>
      </div>
      <JsonDiff after={snapshotToDocument(snapshot)} before={snapshotToDocument(baseline)} className="min-h-[440px]" mode={diffLayout} />
    </div>
  );
}
