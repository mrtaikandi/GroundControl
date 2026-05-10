import { Maximize2 } from 'lucide-react';
import { DiffLayoutToggle } from '@/components/tower/code/DiffLayoutToggle';
import { JsonDiff } from '@/components/tower/code/JsonDiff';
import { Skeleton } from '@/components/ui/skeleton';
import { entriesToDocument } from '@/lib/snapshot-document';
import { cn } from '@/lib/utils';
import type { SnapshotDetail } from '@/queries/useSnapshots';
import { useTweaksStore } from '@/store/tweaks';

type EntriesLike = Pick<SnapshotDetail, 'entries'>;

interface SnapshotDiffViewProps {
  baseline?: EntriesLike;
  changeCount: number;
  contentClassName?: string;
  isLoading?: boolean;
  onExpand?: () => void;
  snapshot?: EntriesLike;
  targetLabel: string;
}

export function SnapshotDiffView({ baseline, changeCount, contentClassName, isLoading = false, onExpand, snapshot, targetLabel }: SnapshotDiffViewProps) {
  const diffLayout = useTweaksStore((state) => state.diffLayout);

  if (isLoading) {
    return <Skeleton className="h-[520px]" />;
  }

  return (
    <div className="overflow-hidden rounded-lg border border-stroke-subtle bg-bg-container">
      <div className="flex items-center justify-between gap-3 border-b border-stroke-subtle px-4 py-2 text-[12px] font-medium text-fg-caption">
        <span>Diff vs {targetLabel}</span>
        <div className="flex items-center gap-3">
          <span>{changeCount} {changeCount === 1 ? 'change' : 'changes'}</span>
          <DiffLayoutToggle size="sm" />
          {onExpand ? (
            <button
              aria-label="Expand snapshot detail"
              className="grid size-7 shrink-0 place-items-center rounded-md text-fg-icon-subtle transition-colors hover:bg-bg-container hover:text-fg-body"
              onClick={onExpand}
              type="button"
            >
              <Maximize2 aria-hidden="true" className="size-3.5" strokeWidth={1.8} />
            </button>
          ) : null}
        </div>
      </div>
      <JsonDiff after={entriesToDocument(snapshot?.entries)} bare before={entriesToDocument(baseline?.entries)} className={cn('min-h-[440px]', contentClassName)} mode={diffLayout} />
    </div>
  );
}
