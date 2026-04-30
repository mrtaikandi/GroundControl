import { Badge as PrimitiveBadge } from '@/components/ui/badge';
import { cn } from '@/lib/utils';

type BadgeVariant = 'neutral' | 'info' | 'success' | 'warning' | 'critical';

interface BadgeProps extends Omit<React.ComponentProps<typeof PrimitiveBadge>, 'variant'> {
  variant?: BadgeVariant;
}

const variantClassNames: Record<BadgeVariant, string> = {
  critical: 'bg-badge-critical-bg text-badge-critical-fg',
  info: 'bg-badge-info-bg text-badge-info-fg',
  neutral: 'bg-badge-neutral-bg text-badge-neutral-fg',
  success: 'bg-badge-success-bg text-badge-success-fg',
  warning: 'bg-badge-warning-bg text-badge-warning-fg',
};

export function Badge({ className, variant = 'neutral', ...props }: BadgeProps) {
  return <PrimitiveBadge className={cn(variantClassNames[variant], className)} {...props} />;
}