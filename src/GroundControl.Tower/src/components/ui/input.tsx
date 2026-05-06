import * as React from 'react';
import { cn } from '@/lib/utils';

function Input({ className, type, ...props }: React.ComponentProps<'input'>) {
  return (
    <input
      className={cn(
        'ui-surface-field ui-text-body h-9 w-full px-3 py-1 placeholder:text-muted-foreground focus:border-ring focus:ring-2 focus:ring-ring/25 disabled:cursor-not-allowed disabled:opacity-40',
        className,
      )}
      data-slot="input"
      type={type}
      {...props}
    />
  );
}

export { Input };