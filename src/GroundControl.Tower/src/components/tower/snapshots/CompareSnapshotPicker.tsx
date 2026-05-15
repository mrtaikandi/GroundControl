import { Check, ChevronDown, GitCompareArrows, Search } from 'lucide-react';
import { useMemo, useRef, useState } from 'react';
import { Badge } from '@/components/tower/data/Badge';
import { Input } from '@/components/ui/input';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { cn } from '@/lib/utils';
import type { SnapshotSummary } from '@/queries/useSnapshots';

interface CompareSnapshotPickerProps {
  activeSnapshotId?: string;
  active: boolean;
  compareSnapshotId?: string;
  disabled?: boolean;
  onSelect: (snapshotId: string) => void;
  selectedSnapshotId?: string;
  size?: 'md' | 'sm';
  snapshots: SnapshotSummary[];
}

const sizeClassNames = {
  md: 'ui-text-body-sm h-7 px-3',
  sm: 'ui-text-caption h-6 px-2.5',
} as const;

export function CompareSnapshotPicker({
  active,
  activeSnapshotId,
  compareSnapshotId,
  disabled = false,
  onSelect,
  selectedSnapshotId,
  size = 'md',
  snapshots,
}: CompareSnapshotPickerProps) {
  const [open, setOpen] = useState(false);
  const [search, setSearch] = useState('');
  const searchRef = useRef<HTMLInputElement>(null);
  const compareSummary = useMemo(
    () => snapshots.find((snapshot) => snapshot.id === compareSnapshotId),
    [compareSnapshotId, snapshots],
  );

  const filtered = useMemo(() => {
    const term = search.trim().toLowerCase();
    if (!term) {
      return snapshots;
    }

    return snapshots.filter((snapshot) => {
      const version = `v${snapshot.snapshotVersion}`.toLowerCase();
      const description = snapshot.description?.toLowerCase() ?? '';
      return version.includes(term) || description.includes(term);
    });
  }, [search, snapshots]);

  const label = active && compareSummary
    ? `Compare with v${compareSummary.snapshotVersion}${compareSummary.id === activeSnapshotId ? ' (active)' : ''}`
    : 'Compare';

  return (
    <Popover open={open} onOpenChange={(next) => { if (next) { setSearch(''); } setOpen(next); }}>
      <PopoverTrigger
        aria-pressed={active}
        className={cn(
          'inline-flex items-center gap-1.5 rounded-full font-medium transition-colors duration-150 ease-out disabled:cursor-not-allowed disabled:opacity-40',
          sizeClassNames[size],
          active ? 'bg-bg-surface font-semibold text-fg-heading shadow-ui-button-subtle' : 'text-fg-caption hover:text-fg-body',
        )}
        disabled={disabled}
        type="button"
      >
        <GitCompareArrows aria-hidden="true" className="size-3.5" strokeWidth={1.8} />
        {label}
        <ChevronDown aria-hidden="true" className="size-3.5" strokeWidth={1.8} />
      </PopoverTrigger>
      <PopoverContent
        align="start"
        className="w-80 p-0"
        onOpenAutoFocus={(event) => {
          event.preventDefault();
          searchRef.current?.focus();
        }}
      >
        <div className="flex items-center gap-2 border-b border-stroke-subtle px-3 py-2">
          <Search aria-hidden="true" className="size-3.5 text-fg-icon-subtle" />
          <Input
            aria-label="Search snapshots"
            className="h-7 border-0 bg-transparent px-0 text-[13px] focus-visible:ring-0"
            onChange={(event) => setSearch(event.target.value)}
            placeholder="Search by version or description…"
            ref={searchRef}
            value={search}
          />
        </div>
        <div className="max-h-72 overflow-y-auto p-1">
          {filtered.length === 0 ? (
            <div className="px-3 py-6 text-center text-[12.5px] text-fg-caption">No snapshots match your search.</div>
          ) : (
            filtered.map((snapshot) => {
              const isCompareTarget = snapshot.id === compareSnapshotId && active;
              const isActive = snapshot.id === activeSnapshotId;
              const isSelected = snapshot.id === selectedSnapshotId;

              return (
                <button
                  className={cn(
                    'flex w-full items-start gap-2 rounded-md px-2 py-1.5 text-left transition-colors hover:bg-muted focus:bg-muted focus:outline-none',
                    isCompareTarget && 'bg-bg-selected',
                  )}
                  disabled={isSelected}
                  key={snapshot.id}
                  onClick={() => {
                    onSelect(snapshot.id);
                    setOpen(false);
                  }}
                  type="button"
                >
                  <span className="mt-0.5 flex size-4 shrink-0 items-center justify-center">
                    {isCompareTarget ? <Check aria-hidden="true" className="size-3.5" /> : null}
                  </span>
                  <span className="min-w-0 flex-1">
                    <span className="flex flex-wrap items-center gap-1.5">
                      <span className="font-mono text-[13px] font-semibold text-fg-heading">v{snapshot.snapshotVersion}</span>
                      {isActive ? <Badge variant="success">active</Badge> : null}
                      {isSelected ? <Badge variant="neutral">viewing</Badge> : null}
                    </span>
                    {snapshot.description?.trim() ? (
                      <span className="mt-0.5 block truncate text-[12px] text-fg-body">{snapshot.description.trim()}</span>
                    ) : null}
                  </span>
                </button>
              );
            })
          )}
        </div>
      </PopoverContent>
    </Popover>
  );
}
