import type { ReactNode } from 'react';
import { cn } from '@/lib/utils';

interface ToolbarProps {
  className?: string;
  end?: ReactNode;
  endClassName?: string;
  start?: ReactNode;
  startClassName?: string;
}

export function Toolbar({ className, end, endClassName, start, startClassName }: ToolbarProps) {
  return (
    <div className={cn('flex flex-col gap-3 rounded-xl border border-stroke-subtle bg-bg-surface px-4 py-3 sm:flex-row sm:items-center sm:justify-between', className)}>
      {start ? <div className={cn('min-w-0', startClassName)}>{start}</div> : null}
      {end ? <div className={cn('flex shrink-0 items-center', endClassName)}>{end}</div> : null}
    </div>
  );
}