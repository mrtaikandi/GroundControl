import { cn } from '@/lib/utils';

interface ScopeTagProps {
  className?: string;
  dimension: string;
  size?: 'md' | 'sm';
  value: string;
}

const sizeClasses = {
  md: 'h-6 gap-1.5 px-2 text-[12px]',
  sm: 'h-5 gap-1 px-1.5 text-[11px]',
} as const;

export function ScopeTag({ className, dimension, size = 'md', value }: ScopeTagProps) {
  return (
    <span className={cn('inline-flex items-center rounded-md bg-bg-selected font-mono text-fg-on-selected', sizeClasses[size], className)}>
      <span className="font-semibold">{dimension}</span>
      <span aria-hidden="true" className="opacity-50">=</span>
      <span>{value}</span>
    </span>
  );
}
