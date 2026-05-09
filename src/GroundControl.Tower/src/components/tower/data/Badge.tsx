import type { LucideIcon } from 'lucide-react';
import { Badge as PrimitiveBadge } from '@/components/ui/badge';
import { cn } from '@/lib/utils';

type BadgeVariant = 'neutral' | 'info' | 'success' | 'warning' | 'critical' | 'selected';
type BadgeTone = 'default' | 'mono';

interface BadgeProps extends Omit<React.ComponentProps<typeof PrimitiveBadge>, 'variant'> {
  icon?: LucideIcon;
  tone?: BadgeTone;
  variant?: BadgeVariant;
}

const variantClassNames: Record<BadgeVariant, string> = {
  critical: 'bg-badge-critical-bg text-badge-critical-fg',
  info: 'bg-badge-info-bg text-badge-info-fg',
  neutral: 'bg-badge-neutral-bg text-badge-neutral-fg',
  selected: 'bg-bg-selected text-fg-on-selected',
  success: 'bg-badge-success-bg text-badge-success-fg',
  warning: 'bg-badge-warning-bg text-badge-warning-fg',
};

const toneClassNames: Record<BadgeTone, string> = {
  default: '',
  mono: 'gap-1 font-mono text-[11px] uppercase tracking-wide leading-none',
};

export function Badge({ children, className, icon: Icon, tone = 'default', variant = 'neutral', ...props }: BadgeProps) {
  return (
    <PrimitiveBadge className={cn(variantClassNames[variant], toneClassNames[tone], className)} {...props}>
      {Icon ? <Icon aria-hidden="true" className="size-3" strokeWidth={1.8} /> : null}
      {children}
    </PrimitiveBadge>
  );
}
