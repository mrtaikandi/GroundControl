import * as SheetPrimitive from '@radix-ui/react-dialog';
import { X } from 'lucide-react';
import * as React from 'react';
import { cn } from '@/lib/utils';

function Sheet({ ...props }: React.ComponentProps<typeof SheetPrimitive.Root>) {
  return <SheetPrimitive.Root data-slot="sheet" {...props} />;
}

function SheetTrigger({ ...props }: React.ComponentProps<typeof SheetPrimitive.Trigger>) {
  return <SheetPrimitive.Trigger data-slot="sheet-trigger" {...props} />;
}

function SheetClose({ ...props }: React.ComponentProps<typeof SheetPrimitive.Close>) {
  return <SheetPrimitive.Close data-slot="sheet-close" {...props} />;
}

function SheetPortal({ ...props }: React.ComponentProps<typeof SheetPrimitive.Portal>) {
  return <SheetPrimitive.Portal data-slot="sheet-portal" {...props} />;
}

function SheetOverlay({ className, ...props }: React.ComponentProps<typeof SheetPrimitive.Overlay>) {
  return <SheetPrimitive.Overlay className={cn('fixed inset-0 z-50 bg-fg-heading/35', className)} data-slot="sheet-overlay" {...props} />;
}

function SheetContent({ children, className, side = 'right', ...props }: React.ComponentProps<typeof SheetPrimitive.Content> & { side?: 'top' | 'right' | 'bottom' | 'left' }) {
  return <SheetPortal><SheetOverlay /><SheetPrimitive.Content className={cn('fixed z-50 gap-4 border-border bg-popover p-6 text-popover-foreground shadow-[0_30px_70px_-20px_rgba(0,0,40,.45)] outline-none', side === 'right' && 'inset-y-0 right-0 h-full w-[min(420px,calc(100vw-32px))] border-l', side === 'left' && 'inset-y-0 left-0 h-full w-[min(420px,calc(100vw-32px))] border-r', side === 'top' && 'inset-x-0 top-0 border-b', side === 'bottom' && 'inset-x-0 bottom-0 border-t', className)} data-slot="sheet-content" {...props}>{children}<SheetPrimitive.Close className="absolute right-4 top-4 grid size-8 place-items-center rounded-lg text-fg-icon-subtle hover:bg-muted hover:text-fg-body"><X aria-hidden="true" className="size-4" /><span className="sr-only">Close</span></SheetPrimitive.Close></SheetPrimitive.Content></SheetPortal>;
}

function SheetHeader({ className, ...props }: React.ComponentProps<'div'>) {
  return <div className={cn('flex flex-col gap-1.5 text-left', className)} data-slot="sheet-header" {...props} />;
}

function SheetFooter({ className, ...props }: React.ComponentProps<'div'>) {
  return <div className={cn('mt-auto flex flex-col-reverse gap-2 sm:flex-row sm:justify-end', className)} data-slot="sheet-footer" {...props} />;
}

function SheetTitle({ className, ...props }: React.ComponentProps<typeof SheetPrimitive.Title>) {
  return <SheetPrimitive.Title className={cn('text-[22px] font-medium leading-tight text-fg-heading', className)} data-slot="sheet-title" {...props} />;
}

function SheetDescription({ className, ...props }: React.ComponentProps<typeof SheetPrimitive.Description>) {
  return <SheetPrimitive.Description className={cn('text-[13px] text-fg-caption', className)} data-slot="sheet-description" {...props} />;
}

export { Sheet, SheetClose, SheetContent, SheetDescription, SheetFooter, SheetHeader, SheetOverlay, SheetPortal, SheetTitle, SheetTrigger };