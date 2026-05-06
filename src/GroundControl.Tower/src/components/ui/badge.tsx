import { cva, type VariantProps } from 'class-variance-authority';
import * as React from 'react';
import { cn } from '@/lib/utils';

const badgeVariants = cva('ui-text-caption inline-flex items-center rounded-md px-2 py-0.5 font-medium whitespace-nowrap', {
  defaultVariants: {
    variant: 'default',
  },
  variants: {
    variant: {
      critical: 'bg-badge-critical-bg text-badge-critical-fg',
      default: 'bg-badge-neutral-bg text-badge-neutral-fg',
      info: 'bg-badge-info-bg text-badge-info-fg',
      secondary: 'bg-badge-neutral-bg text-badge-neutral-fg',
      success: 'bg-badge-success-bg text-badge-success-fg',
      warning: 'bg-badge-warning-bg text-badge-warning-fg',
    },
  },
});

function Badge({ className, variant, ...props }: React.ComponentProps<'span'> & VariantProps<typeof badgeVariants>) {
  return <span className={cn(badgeVariants({ className, variant }))} data-slot="badge" {...props} />;
}

export { Badge, badgeVariants };