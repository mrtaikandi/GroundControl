import { Slot } from '@radix-ui/react-slot';
import { cva, type VariantProps } from 'class-variance-authority';
import * as React from 'react';
import { cn } from '@/lib/utils';

const buttonSizeClasses = {
  default: 'h-9 rounded-lg px-4',
  icon: 'size-9 rounded-lg p-0',
  sm: 'h-8 rounded-lg px-3',
  lg: 'h-10 rounded-lg px-5',
} as const;

const buttonVariants = cva(
  "ui-text-body inline-flex shrink-0 items-center justify-center gap-2 whitespace-nowrap border font-semibold capitalize outline-none transition-[background-color,border-color,color,box-shadow,filter,transform] duration-150 ease-out disabled:pointer-events-none disabled:translate-y-0 disabled:shadow-none disabled:opacity-45 focus-visible:ring-2 focus-visible:ring-ring/25 focus-visible:ring-offset-2 focus-visible:ring-offset-background active:translate-y-px [&_svg]:pointer-events-none [&_svg:not([class*='size-'])]:size-4 [&_svg]:shrink-0",
  {
    defaultVariants: {
      size: 'default',
      variant: 'default',
    },
    variants: {
      size: buttonSizeClasses,
      variant: {
        default: 'border-primary bg-primary text-primary-foreground shadow-ui-button hover:shadow-ui-button-hover hover:brightness-[var(--tower-interaction-button-hover-brightness)] active:brightness-[var(--tower-interaction-button-active-brightness)]',
        destructive: 'border-[var(--tower-interaction-button-danger-outline)] bg-transparent text-[var(--tower-interaction-button-danger-outline)] shadow-none hover:border-destructive hover:bg-destructive hover:text-destructive-foreground hover:shadow-ui-button-danger-hover focus-visible:border-destructive focus-visible:bg-destructive focus-visible:text-destructive-foreground focus-visible:shadow-ui-button-danger-hover active:brightness-[var(--tower-interaction-button-danger-active-brightness)]',
        ghost: 'border-transparent bg-transparent text-fg-caption shadow-none hover:bg-[var(--tower-interaction-button-subtle-hover-background)] hover:text-fg-heading',
        outline: 'border-stroke-field-initial bg-transparent text-fg-body shadow-none hover:border-[var(--tower-interaction-button-subtle-hover-border)] hover:bg-[var(--tower-interaction-button-subtle-hover-background)] hover:text-fg-heading',
        secondary: 'border-input bg-background text-fg-body shadow-ui-button-subtle hover:border-[var(--tower-interaction-button-subtle-hover-border)] hover:bg-[var(--tower-interaction-button-subtle-hover-background)] hover:text-fg-heading hover:shadow-ui-button-subtle-hover',
      },
    },
  },
);

function Button({ asChild = false, className, size, variant, ...props }: React.ComponentProps<'button'> & VariantProps<typeof buttonVariants> & { asChild?: boolean }) {
  const Comp = asChild ? Slot : 'button';

  return <Comp className={cn(buttonVariants({ className, size, variant }))} data-slot="button" {...props} />;
}

export { Button, buttonVariants };