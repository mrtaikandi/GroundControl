import * as React from 'react';
import { cn } from '@/lib/utils';

function Textarea({ className, ...props }: React.ComponentProps<'textarea'>) {
  return (
    <textarea
      className={cn(
        'min-h-24 w-full rounded-lg border border-input bg-background px-3 py-2 text-[13px] text-foreground outline-none transition-colors placeholder:text-muted-foreground focus:border-ring focus:ring-2 focus:ring-ring/25 disabled:cursor-not-allowed disabled:opacity-40',
        className,
      )}
      data-slot="textarea"
      {...props}
    />
  );
}

export { Textarea };