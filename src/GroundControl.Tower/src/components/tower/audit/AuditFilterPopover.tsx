import { ChevronDown } from 'lucide-react';
import { useEffect, useState } from 'react';
import { Button } from '@/components/ui/button';
import { DropdownMenu, DropdownMenuCheckboxItem, DropdownMenuContent, DropdownMenuTrigger } from '@/components/ui/dropdown-menu';
import { Input } from '@/components/ui/input';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { FilterButton } from '@/components/tower/data/FilterButton';
import { cn } from '@/lib/utils';

export interface AuditFilters {
  entityTypes: string[];
  from: string;
  to: string;
}

interface AuditFilterPopoverProps {
  filters: AuditFilters;
  onApply: (filters: AuditFilters) => void;
  options: readonly { label: string; value: string }[];
}

export function AuditFilterPopover({ filters, onApply, options }: AuditFilterPopoverProps) {
  const [open, setOpen] = useState(false);
  const [draft, setDraft] = useState<AuditFilters>(filters);
  const activeCount = filters.entityTypes.length + (filters.from ? 1 : 0) + (filters.to ? 1 : 0);
  const dateRangeInvalid = !!draft.from && !!draft.to && draft.from > draft.to;

  useEffect(() => {
    if (open) {
      setDraft(filters);
    }
  }, [open, filters]);

  function toggleEntityType(value: string) {
    setDraft((current) => ({
      ...current,
      entityTypes: current.entityTypes.includes(value)
        ? current.entityTypes.filter((entry) => entry !== value)
        : [...current.entityTypes, value],
    }));
  }

  function applyAndClose() {
    if (dateRangeInvalid) {
      return;
    }

    onApply(draft);
    setOpen(false);
  }

  function clearAll() {
    const cleared: AuditFilters = { entityTypes: [], from: '', to: '' };
    setDraft(cleared);
    onApply(cleared);
    setOpen(false);
  }

  const triggerLabel = entityTriggerLabel(draft.entityTypes, options);

  return (
    <Popover onOpenChange={setOpen} open={open}>
      <PopoverTrigger asChild>
        <FilterButton activeCount={activeCount} aria-label="Filter audit records" />
      </PopoverTrigger>

      <PopoverContent align="end" className="w-96">
        <div className="grid gap-4">
          <div className="grid gap-1.5">
            <div className="text-[10.5px] font-semibold uppercase tracking-[0.08em] text-fg-caption">Entity</div>
            <DropdownMenu>
              <DropdownMenuTrigger asChild>
                <button
                  className="ui-surface-field ui-text-body flex h-9 w-full items-center justify-between gap-2 px-3 text-left focus:border-ring focus:outline-none focus:ring-2 focus:ring-ring/25"
                  type="button"
                >
                  <span className={cn('truncate', draft.entityTypes.length === 0 && 'text-fg-caption')}>{triggerLabel}</span>
                  <ChevronDown aria-hidden="true" className="size-4 shrink-0 text-fg-icon-subtle" />
                </button>
              </DropdownMenuTrigger>
              <DropdownMenuContent align="start" className="max-h-72 w-[var(--radix-dropdown-menu-trigger-width)] overflow-y-auto">
                {options.map((option) => (
                  <DropdownMenuCheckboxItem
                    checked={draft.entityTypes.includes(option.value)}
                    key={option.value}
                    onCheckedChange={() => toggleEntityType(option.value)}
                    onSelect={(event) => event.preventDefault()}
                  >
                    {option.label}
                  </DropdownMenuCheckboxItem>
                ))}
              </DropdownMenuContent>
            </DropdownMenu>
          </div>

          <div className="grid gap-2">
            <div className="text-[10.5px] font-semibold uppercase tracking-[0.08em] text-fg-caption">Date range</div>
            <div className="grid grid-cols-2 gap-2">
              <label className="grid gap-1 text-[12px] text-fg-body">
                <span>From</span>
                <Input onChange={(event) => setDraft((current) => ({ ...current, from: event.target.value }))} type="date" value={draft.from} />
              </label>
              <label className="grid gap-1 text-[12px] text-fg-body">
                <span>To</span>
                <Input onChange={(event) => setDraft((current) => ({ ...current, to: event.target.value }))} type="date" value={draft.to} />
              </label>
            </div>
            {dateRangeInvalid ? <div className="text-[11.5px] text-badge-critical-fg">From date must be before or equal to To date.</div> : null}
          </div>

          <div className="flex items-center justify-between border-t border-stroke-subtle pt-3">
            <button
              className={cn(
                'text-[12.5px] font-medium transition-colors',
                activeCount > 0 ? 'text-primary hover:underline' : 'pointer-events-none text-fg-caption',
              )}
              disabled={activeCount === 0}
              onClick={clearAll}
              type="button"
            >
              Clear all
            </button>
            <Button disabled={dateRangeInvalid} onClick={applyAndClose} size="sm" type="button">Done</Button>
          </div>
        </div>
      </PopoverContent>
    </Popover>
  );
}

function entityTriggerLabel(selected: string[], options: readonly { label: string; value: string }[]) {
  if (selected.length === 0) {
    return 'All entities';
  }

  if (selected.length === 1) {
    return options.find((option) => option.value === selected[0])?.label ?? selected[0];
  }

  return `${selected.length} selected`;
}
