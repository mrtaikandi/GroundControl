import * as React from 'react';
import { cn } from '@/lib/utils';

function Textarea({ className, ...props }: React.ComponentProps<'textarea'>) {
  return (
    <textarea
      className={cn(
        'ui-surface-field ui-text-body min-h-24 w-full px-3 py-2 placeholder:text-muted-foreground focus:border-ring focus:ring-2 focus:ring-ring/25 disabled:cursor-not-allowed disabled:opacity-40',
        className,
      )}
      data-slot="textarea"
      {...props}
    />
  );
}

export { Textarea };