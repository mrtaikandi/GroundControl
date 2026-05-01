import * as SelectPrimitive from '@radix-ui/react-select';
import { Check, ChevronDown, ChevronUp } from 'lucide-react';
import * as React from 'react';
import { cn } from '@/lib/utils';

function Select({ ...props }: React.ComponentProps<typeof SelectPrimitive.Root>) {
  return <SelectPrimitive.Root data-slot="select" {...props} />;
}

function SelectTrigger({ children, className, size = 'default', ...props }: React.ComponentProps<typeof SelectPrimitive.Trigger> & { size?: 'sm' | 'default' }) {
  return <SelectPrimitive.Trigger className={cn('flex w-full items-center justify-between gap-2 rounded-lg border border-input bg-background px-3 text-[13px] text-foreground outline-none transition-colors placeholder:text-muted-foreground focus:border-ring focus:ring-2 focus:ring-ring/25 disabled:cursor-not-allowed disabled:opacity-40', size === 'default' ? 'h-9' : 'h-8', className)} data-size={size} data-slot="select-trigger" {...props}>{children}<SelectPrimitive.Icon asChild><ChevronDown className="size-4 text-fg-icon-subtle" /></SelectPrimitive.Icon></SelectPrimitive.Trigger>;
}

function SelectValue({ ...props }: React.ComponentProps<typeof SelectPrimitive.Value>) {
  return <SelectPrimitive.Value data-slot="select-value" {...props} />;
}

function SelectContent({ children, className, position = 'popper', ...props }: React.ComponentProps<typeof SelectPrimitive.Content>) {
  return <SelectPrimitive.Portal><SelectPrimitive.Content className={cn('relative z-50 max-h-80 min-w-32 overflow-hidden rounded-xl border border-border bg-popover text-[13px] text-popover-foreground shadow-[0_18px_40px_-16px_rgba(0,0,40,.25)]', position === 'popper' && 'data-[side=bottom]:translate-y-1 data-[side=left]:-translate-x-1 data-[side=right]:translate-x-1 data-[side=top]:-translate-y-1', className)} data-slot="select-content" position={position} {...props}><SelectScrollUpButton /><SelectPrimitive.Viewport className={cn('p-1', position === 'popper' && 'h-[var(--radix-select-trigger-height)] w-full min-w-[var(--radix-select-trigger-width)]')}>{children}</SelectPrimitive.Viewport><SelectScrollDownButton /></SelectPrimitive.Content></SelectPrimitive.Portal>;
}

function SelectItem({ children, className, ...props }: React.ComponentProps<typeof SelectPrimitive.Item>) {
  return <SelectPrimitive.Item className={cn('relative flex w-full cursor-default select-none items-center gap-2 rounded-lg py-1.5 pl-8 pr-2 text-[13px] outline-none hover:bg-muted focus:bg-muted data-[disabled]:pointer-events-none data-[disabled]:opacity-40', className)} data-slot="select-item" {...props}><span className="absolute left-2 flex size-4 items-center justify-center"><SelectPrimitive.ItemIndicator><Check className="size-4" /></SelectPrimitive.ItemIndicator></span><SelectPrimitive.ItemText>{children}</SelectPrimitive.ItemText></SelectPrimitive.Item>;
}

function SelectLabel({ className, ...props }: React.ComponentProps<typeof SelectPrimitive.Label>) {
  return <SelectPrimitive.Label className={cn('px-2 py-1.5 text-[11.5px] font-medium text-fg-caption', className)} data-slot="select-label" {...props} />;
}

function SelectSeparator({ className, ...props }: React.ComponentProps<typeof SelectPrimitive.Separator>) {
  return <SelectPrimitive.Separator className={cn('-mx-1 my-1 h-px bg-border', className)} data-slot="select-separator" {...props} />;
}

function SelectScrollUpButton({ className, ...props }: React.ComponentProps<typeof SelectPrimitive.ScrollUpButton>) {
  return <SelectPrimitive.ScrollUpButton className={cn('flex cursor-default items-center justify-center py-1', className)} data-slot="select-scroll-up-button" {...props}><ChevronUp className="size-4" /></SelectPrimitive.ScrollUpButton>;
}

function SelectScrollDownButton({ className, ...props }: React.ComponentProps<typeof SelectPrimitive.ScrollDownButton>) {
  return <SelectPrimitive.ScrollDownButton className={cn('flex cursor-default items-center justify-center py-1', className)} data-slot="select-scroll-down-button" {...props}><ChevronDown className="size-4" /></SelectPrimitive.ScrollDownButton>;
}

function SelectGroup({ ...props }: React.ComponentProps<typeof SelectPrimitive.Group>) {
  return <SelectPrimitive.Group data-slot="select-group" {...props} />;
}

export { Select, SelectContent, SelectGroup, SelectItem, SelectLabel, SelectScrollDownButton, SelectScrollUpButton, SelectSeparator, SelectTrigger, SelectValue };