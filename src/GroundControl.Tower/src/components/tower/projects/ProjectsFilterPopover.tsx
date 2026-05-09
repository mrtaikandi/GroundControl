import { Filter, Search } from 'lucide-react';
import { useEffect, useId, useRef, useState } from 'react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { cn } from '@/lib/utils';

interface ProjectsFilterPopoverProps {
  appliedSearch: string | undefined;
  onApply: (search: string | undefined) => void;
}

export function ProjectsFilterPopover({ appliedSearch, onApply }: ProjectsFilterPopoverProps) {
  const [open, setOpen] = useState(false);
  const [draft, setDraft] = useState(appliedSearch ?? '');
  const labelId = useId();
  const inputRef = useRef<HTMLInputElement>(null);
  const activeCount = appliedSearch ? 1 : 0;

  useEffect(() => {
    if (open) {
      setDraft(appliedSearch ?? '');
    }
  }, [open, appliedSearch]);

  function applyAndClose() {
    const trimmed = draft.trim();
    onApply(trimmed.length > 0 ? trimmed : undefined);
    setOpen(false);
  }

  function clearAll() {
    setDraft('');
    onApply(undefined);
    setOpen(false);
  }

  return (
    <Popover onOpenChange={setOpen} open={open}>
      <PopoverTrigger asChild>
        <button
          aria-expanded={open}
          aria-label="Filter projects"
          className={cn(
            'inline-flex h-9 items-center gap-2 rounded-full border px-4 text-[13px] font-semibold capitalize transition-colors',
            activeCount > 0
              ? 'border-primary bg-primary-50 text-primary-text hover:bg-primary-100'
              : 'border-input bg-background text-fg-body hover:border-stroke-divider hover:bg-bg-container',
          )}
          type="button"
        >
          <Filter aria-hidden="true" className="size-3.5" />
          <span>Filter</span>
          {activeCount > 0 ? (
            <span className="inline-flex h-5 min-w-5 items-center justify-center rounded-full bg-primary px-1.5 text-[11px] font-semibold text-primary-foreground">{activeCount}</span>
          ) : null}
        </button>
      </PopoverTrigger>

      <PopoverContent align="end" className="w-80" onOpenAutoFocus={(event) => {
        event.preventDefault();
        inputRef.current?.focus();
        inputRef.current?.select();
      }}>
        <div className="grid gap-3">
          <div className="grid gap-1.5">
            <label className="text-[10.5px] font-semibold uppercase tracking-[0.08em] text-fg-caption" htmlFor={labelId}>Search</label>
            <div className="relative">
              <Search aria-hidden="true" className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-fg-icon-subtle" />
              <Input
                className="px-9"
                id={labelId}
                onChange={(event) => setDraft(event.target.value)}
                onKeyDown={(event) => {
                  if (event.key === 'Enter') {
                    event.preventDefault();
                    applyAndClose();
                  }
                }}
                placeholder="Project name or description"
                ref={inputRef}
                value={draft}
              />
            </div>
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
            <Button onClick={applyAndClose} size="sm" type="button">Done</Button>
          </div>
        </div>
      </PopoverContent>
    </Popover>
  );
}