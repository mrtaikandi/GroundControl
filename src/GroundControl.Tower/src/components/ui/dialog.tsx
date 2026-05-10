import * as DialogPrimitive from '@radix-ui/react-dialog';
import { Maximize2, Minimize2, X } from 'lucide-react';
import * as React from 'react';
import { cn } from '@/lib/utils';

function Dialog({ ...props }: React.ComponentProps<typeof DialogPrimitive.Root>) {
  return <DialogPrimitive.Root data-slot="dialog" {...props} />;
}

function DialogTrigger({ ...props }: React.ComponentProps<typeof DialogPrimitive.Trigger>) {
  return <DialogPrimitive.Trigger data-slot="dialog-trigger" {...props} />;
}

function DialogPortal({ ...props }: React.ComponentProps<typeof DialogPrimitive.Portal>) {
  return <DialogPrimitive.Portal data-slot="dialog-portal" {...props} />;
}

function DialogClose({ ...props }: React.ComponentProps<typeof DialogPrimitive.Close>) {
  return <DialogPrimitive.Close data-slot="dialog-close" {...props} />;
}

function DialogOverlay({ className, ...props }: React.ComponentProps<typeof DialogPrimitive.Overlay>) {
  return <DialogPrimitive.Overlay className={cn('ui-overlay-scrim fixed inset-0 z-50', className)} data-slot="dialog-overlay" {...props} />;
}

function DialogContent({ children, className, onMaximizeChange, showCloseButton = true, showMaximizeButton = false, ...props }: React.ComponentProps<typeof DialogPrimitive.Content> & { onMaximizeChange?: (isMaximized: boolean) => void; showCloseButton?: boolean; showMaximizeButton?: boolean }) {
  const [isMaximized, setIsMaximized] = React.useState(false);

  React.useEffect(() => {
    onMaximizeChange?.(isMaximized);
  }, [isMaximized, onMaximizeChange]);

  return (
    <DialogPortal>
      <DialogOverlay />
      <DialogPrimitive.Content className={cn('ui-surface-modal ui-text-body fixed left-1/2 top-1/2 z-50 grid w-[min(calc(100vw-32px),520px)] -translate-x-1/2 -translate-y-1/2 gap-4 rounded-2xl p-6 outline-none', className, isMaximized && 'h-[calc(100vh-32px)] w-[calc(100vw-32px)] max-w-none max-h-[calc(100vh-32px)] overflow-auto')} data-slot="dialog-content" {...props}>
        {children}
        {showCloseButton || showMaximizeButton ? (
          <div className="absolute right-4 top-4 flex items-center gap-1">
            {showMaximizeButton ? (
              <button
                aria-label={isMaximized ? 'Restore dialog size' : 'Maximize dialog'}
                className="grid size-8 place-items-center rounded-lg text-fg-icon-subtle hover:bg-muted hover:text-fg-body"
                onClick={() => setIsMaximized((value) => !value)}
                type="button"
              >
                {isMaximized ? <Minimize2 aria-hidden="true" className="size-4" /> : <Maximize2 aria-hidden="true" className="size-4" />}
                <span className="sr-only">{isMaximized ? 'Restore dialog size' : 'Maximize dialog'}</span>
              </button>
            ) : null}
            {showCloseButton ? (
              <DialogPrimitive.Close className="grid size-8 place-items-center rounded-lg text-fg-icon-subtle hover:bg-muted hover:text-fg-body">
                <X aria-hidden="true" className="size-4" />
                <span className="sr-only">Close</span>
              </DialogPrimitive.Close>
            ) : null}
          </div>
        ) : null}
      </DialogPrimitive.Content>
    </DialogPortal>
  );
}

function DialogHeader({ className, ...props }: React.ComponentProps<'div'>) {
  return <div className={cn('flex flex-col gap-1.5 text-left', className)} data-slot="dialog-header" {...props} />;
}

function DialogFooter({ className, ...props }: React.ComponentProps<'div'>) {
  return <div className={cn('flex flex-col-reverse gap-2 sm:flex-row sm:justify-end', className)} data-slot="dialog-footer" {...props} />;
}

function DialogTitle({ className, ...props }: React.ComponentProps<typeof DialogPrimitive.Title>) {
  return <DialogPrimitive.Title className={cn('ui-text-modal-title text-fg-heading', className)} data-slot="dialog-title" {...props} />;
}

function DialogDescription({ className, ...props }: React.ComponentProps<typeof DialogPrimitive.Description>) {
  return <DialogPrimitive.Description className={cn('ui-text-body text-fg-caption', className)} data-slot="dialog-description" {...props} />;
}

export { Dialog, DialogClose, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogOverlay, DialogPortal, DialogTitle, DialogTrigger };