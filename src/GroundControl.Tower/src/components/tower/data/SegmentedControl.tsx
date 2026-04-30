import type { LucideIcon } from 'lucide-react';
import { cn } from '@/lib/utils';

interface SegmentedControlOption<TValue extends string> {
  icon?: LucideIcon;
  label: string;
  value: TValue;
}

interface SegmentedControlProps<TValue extends string> {
  onChange: (value: TValue) => void;
  options: SegmentedControlOption<TValue>[];
  size?: 'md' | 'sm';
  value: TValue;
}

const sizeClassNames = {
  md: 'h-7 px-3 text-[12.5px]',
  sm: 'h-6 px-2.5 text-[11.5px]',
} as const;

export function SegmentedControl<TValue extends string>({ onChange, options, size = 'md', value }: SegmentedControlProps<TValue>) {
  return (
    <div className="inline-flex rounded-full bg-bg-container p-[3px]" role="group">
      {options.map((option) => {
        const Icon = option.icon;
        const active = option.value === value;

        return (
          <button
            aria-pressed={active}
            className={cn(
              'inline-flex items-center gap-1.5 rounded-full font-medium transition-colors duration-150 ease-out',
              sizeClassNames[size],
              active ? 'bg-bg-surface font-semibold text-fg-heading shadow-[0_1px_2px_rgba(0,0,40,.12)]' : 'text-fg-caption hover:text-fg-body',
            )}
            key={option.value}
            onClick={() => onChange(option.value)}
            type="button"
          >
            {Icon ? <Icon aria-hidden="true" className="size-3.5" strokeWidth={1.8} /> : null}
            {option.label}
          </button>
        );
      })}
    </div>
  );
}