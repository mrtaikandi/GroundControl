import { Slot } from '@radix-ui/react-slot';
import { cva, type VariantProps } from 'class-variance-authority';
import * as React from 'react';
import { cn } from '@/lib/utils';

const buttonVariants = cva(
  "inline-flex shrink-0 items-center justify-center gap-2 whitespace-nowrap text-[13px] font-medium outline-none transition-colors disabled:pointer-events-none disabled:opacity-40 [&_svg]:pointer-events-none [&_svg:not([class*='size-'])]:size-4 [&_svg]:shrink-0",
  {
    defaultVariants: {
      size: 'default',
      variant: 'default',
    },
    variants: {
      size: {
        default: 'h-9 px-4 py-2',
        icon: 'size-9',
        sm: 'h-8 px-3',
        lg: 'h-10 px-5',
      },
      variant: {
        default: 'rounded-full bg-primary text-primary-foreground hover:opacity-90 focus-visible:ring-2 focus-visible:ring-ring/50',
        destructive: 'rounded-full bg-destructive text-destructive-foreground hover:opacity-90 focus-visible:ring-2 focus-visible:ring-ring/50',
        ghost: 'rounded-lg text-fg-caption hover:bg-muted hover:text-fg-body focus-visible:ring-2 focus-visible:ring-ring/50',
        outline: 'rounded-full border border-input bg-transparent text-fg-body hover:bg-muted focus-visible:ring-2 focus-visible:ring-ring/50',
        secondary: 'rounded-full border border-input bg-transparent text-fg-body hover:bg-muted focus-visible:ring-2 focus-visible:ring-ring/50',
      },
    },
  },
);

function Button({ asChild = false, className, size, variant, ...props }: React.ComponentProps<'button'> & VariantProps<typeof buttonVariants> & { asChild?: boolean }) {
  const Comp = asChild ? Slot : 'button';

  return <Comp className={cn(buttonVariants({ className, size, variant }))} data-slot="button" {...props} />;
}

export { Button, buttonVariants };