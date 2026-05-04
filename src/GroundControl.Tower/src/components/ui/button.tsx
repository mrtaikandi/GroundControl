import { Slot } from '@radix-ui/react-slot';
import { cva, type VariantProps } from 'class-variance-authority';
import * as React from 'react';
import { cn } from '@/lib/utils';

const buttonSizeClasses = {
  default: 'h-9 px-4',
  icon: 'size-9 p-0',
  sm: 'h-8 px-3',
  lg: 'h-10 px-5',
} as const;

const buttonVariants = cva(
  "inline-flex shrink-0 items-center justify-center gap-2 whitespace-nowrap rounded-lg border text-[13px] font-semibold capitalize outline-none transition-[background-color,border-color,color,box-shadow,transform] duration-150 ease-out disabled:pointer-events-none disabled:translate-y-0 disabled:shadow-none disabled:opacity-45 focus-visible:ring-2 focus-visible:ring-ring/25 focus-visible:ring-offset-2 focus-visible:ring-offset-background active:translate-y-px [&_svg]:pointer-events-none [&_svg:not([class*='size-'])]:size-4 [&_svg]:shrink-0",
  {
    defaultVariants: {
      size: 'default',
      variant: 'default',
    },
    variants: {
      size: buttonSizeClasses,
      variant: {
        default: 'border-primary bg-primary text-primary-foreground shadow-[0_1px_2px_rgba(0,0,40,0.16)] hover:brightness-[1.04] active:brightness-[0.97]',
        destructive: 'border-destructive bg-destructive text-destructive-foreground shadow-[0_1px_2px_rgba(0,0,40,0.12)] hover:brightness-[1.03] active:brightness-[0.96]',
        ghost: 'border-transparent bg-transparent text-fg-caption shadow-none hover:bg-bg-container hover:text-fg-body',
        outline: 'border-stroke-field-initial bg-transparent text-fg-body shadow-none hover:border-stroke-divider hover:bg-bg-container',
        secondary: 'border-input bg-background text-fg-body shadow-[0_1px_2px_rgba(0,0,40,0.06)] hover:border-stroke-divider hover:bg-bg-container',
      },
    },
  },
);

function Button({ asChild = false, className, size, variant, ...props }: React.ComponentProps<'button'> & VariantProps<typeof buttonVariants> & { asChild?: boolean }) {
  const Comp = asChild ? Slot : 'button';

  return <Comp className={cn(buttonVariants({ className, size, variant }))} data-slot="button" {...props} />;
}

export { Button, buttonVariants };