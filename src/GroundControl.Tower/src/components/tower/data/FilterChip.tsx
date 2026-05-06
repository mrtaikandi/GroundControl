import { cn } from '@/lib/utils';

interface FilterChipProps {
  count?: number;
  label: string;
  onToggle: () => void;
  selected?: boolean;
}

export function FilterChip({ count, label, onToggle, selected = false }: FilterChipProps) {
  return (
    <button
      aria-pressed={selected}
      className={cn(
        'inline-flex h-7 items-center gap-1.5 rounded-md px-2.5 font-mono text-[12.5px] transition-colors',
        selected ? 'bg-bg-chip-selected text-fg-chip-selected' : 'bg-bg-container text-fg-body hover:bg-bg-selected',
      )}
      onClick={onToggle}
      type="button"
    >
      <span>{label}</span>
      {typeof count === 'number' ? <span className={cn('rounded-sm px-1 text-[11px]', selected ? 'bg-bg-surface/20' : 'bg-bg-surface text-fg-caption')}>{count}</span> : null}
    </button>
  );
}