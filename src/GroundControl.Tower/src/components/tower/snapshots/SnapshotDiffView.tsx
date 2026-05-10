import { Maximize2, X } from 'lucide-react';
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
  className?: string;
  contentClassName?: string;
  isLoading?: boolean;
  onClose?: () => void;
  onExpand?: () => void;
  snapshot?: EntriesLike;
  sourceLabel: string;
  targetLabel: string;
}

export function SnapshotDiffView({ baseline, changeCount, className, contentClassName, isLoading = false, onClose, onExpand, snapshot, sourceLabel, targetLabel }: SnapshotDiffViewProps) {
  const diffLayout = useTweaksStore((state) => state.diffLayout);
  const lineWrap = useTweaksStore((state) => state.diffLineWrap);
  const setLineWrap = useTweaksStore((state) => state.setDiffLineWrap);

  if (isLoading) {
    return <Skeleton className="h-[520px]" />;
  }

  return (
    <div className={cn('flex min-h-0 flex-col overflow-hidden rounded-lg border border-stroke-subtle', className)}>
      <div className="flex items-center justify-between gap-3 border-b border-stroke-subtle bg-bg-container px-4 py-2 text-[12px] font-medium text-fg-caption">
        <span>Compare {sourceLabel} with {targetLabel}</span>
        <div className="flex items-center gap-3">
          <span>{changeCount} {changeCount === 1 ? 'change' : 'changes'}</span>
          <DiffLayoutToggle size="sm" />
          <label className="flex cursor-pointer select-none items-center gap-1.5">
            <input
              checked={lineWrap}
              className="size-3.5 accent-[var(--tower-stroke-field-focus)]"
              onChange={(event) => setLineWrap(event.target.checked)}
              type="checkbox"
            />
            Wrap
          </label>
          {onClose ? (
            <button
              aria-label="Close expanded snapshot diff"
              className="grid size-7 shrink-0 place-items-center rounded-md text-fg-icon-subtle transition-colors hover:bg-bg-container hover:text-fg-body"
              onClick={onClose}
              type="button"
            >
              <X aria-hidden="true" className="size-3.5" strokeWidth={1.8} />
            </button>
          ) : null}
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
      <JsonDiff after={entriesToDocument(snapshot?.entries)} bare before={entriesToDocument(baseline?.entries)} className={cn('min-h-0 flex-1 overflow-auto', contentClassName)} mode={diffLayout} />
    </div>
  );
}
