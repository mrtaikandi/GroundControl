import * as TooltipPrimitive from '@radix-ui/react-tooltip';
import * as React from 'react';
import { cn } from '@/lib/utils';

function TooltipProvider({ delayDuration = 250, ...props }: React.ComponentProps<typeof TooltipPrimitive.Provider>) {
  return <TooltipPrimitive.Provider data-slot="tooltip-provider" delayDuration={delayDuration} {...props} />;
}

function Tooltip({ ...props }: React.ComponentProps<typeof TooltipPrimitive.Root>) {
  return <TooltipPrimitive.Root data-slot="tooltip" {...props} />;
}

function TooltipTrigger({ ...props }: React.ComponentProps<typeof TooltipPrimitive.Trigger>) {
  return <TooltipPrimitive.Trigger data-slot="tooltip-trigger" {...props} />;
}

function TooltipContent({ children, className, sideOffset = 6, ...props }: React.ComponentProps<typeof TooltipPrimitive.Content>) {
  return <TooltipPrimitive.Portal><TooltipPrimitive.Content className={cn('z-50 rounded-lg border border-border bg-popover px-2.5 py-1.5 text-[12px] text-popover-foreground shadow-[0_18px_40px_-16px_rgba(0,0,40,.25)]', className)} data-slot="tooltip-content" sideOffset={sideOffset} {...props}>{children}<TooltipPrimitive.Arrow className="fill-popover" /></TooltipPrimitive.Content></TooltipPrimitive.Portal>;
}

export { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger };