import { Filter } from 'lucide-react';
import { type ComponentProps } from 'react';
import { cn } from '@/lib/utils';

interface FilterButtonProps extends ComponentProps<'button'> {
  activeCount?: number;
}

export function FilterButton({ activeCount = 0, className, ...props }: FilterButtonProps) {
  return (
    <button
      className={cn(
        'relative grid size-9 place-items-center rounded-full border transition-colors',
        activeCount > 0
          ? 'border-primary bg-primary-50 text-primary-text hover:bg-primary-100'
          : 'border-input bg-background text-fg-body hover:border-stroke-divider hover:bg-bg-container',
        className,
      )}
      type="button"
      {...props}
    >
      <Filter aria-hidden="true" className="size-4" />
      {activeCount > 0 ? (
        <span className="absolute -right-0.5 -top-0.5 grid h-4 min-w-4 place-items-center rounded-full bg-primary px-1 text-[10px] font-semibold leading-none text-primary-foreground">{activeCount}</span>
      ) : null}
    </button>
  );
}
