import * as DropdownMenuPrimitive from '@radix-ui/react-dropdown-menu';
import { Check, ChevronRight, Circle } from 'lucide-react';
import * as React from 'react';
import { cn } from '@/lib/utils';

function DropdownMenu({ ...props }: React.ComponentProps<typeof DropdownMenuPrimitive.Root>) {
  return <DropdownMenuPrimitive.Root data-slot="dropdown-menu" {...props} />;
}

function DropdownMenuTrigger({ ...props }: React.ComponentProps<typeof DropdownMenuPrimitive.Trigger>) {
  return <DropdownMenuPrimitive.Trigger data-slot="dropdown-menu-trigger" {...props} />;
}

function DropdownMenuContent({ align = 'start', className, sideOffset = 6, ...props }: React.ComponentProps<typeof DropdownMenuPrimitive.Content>) {
  return <DropdownMenuPrimitive.Portal><DropdownMenuPrimitive.Content align={align} className={cn('z-50 min-w-40 overflow-hidden rounded-xl border border-border bg-popover p-1 text-popover-foreground shadow-[0_18px_40px_-16px_rgba(0,0,40,.25)]', className)} data-slot="dropdown-menu-content" sideOffset={sideOffset} {...props} /></DropdownMenuPrimitive.Portal>;
}

function DropdownMenuItem({ className, inset, variant = 'default', ...props }: React.ComponentProps<typeof DropdownMenuPrimitive.Item> & { inset?: boolean; variant?: 'default' | 'destructive' }) {
  return <DropdownMenuPrimitive.Item className={cn('relative flex cursor-default select-none items-center gap-2 rounded-lg px-2 py-1.5 text-[13px] outline-none hover:bg-muted focus:bg-muted data-[disabled]:pointer-events-none data-[disabled]:opacity-40', inset && 'pl-8', variant === 'destructive' && 'text-destructive', className)} data-slot="dropdown-menu-item" {...props} />;
}

function DropdownMenuCheckboxItem({ checked, children, className, ...props }: React.ComponentProps<typeof DropdownMenuPrimitive.CheckboxItem>) {
  return <DropdownMenuPrimitive.CheckboxItem checked={checked} className={cn('relative flex cursor-default select-none items-center gap-2 rounded-lg py-1.5 pl-8 pr-2 text-[13px] outline-none hover:bg-muted focus:bg-muted data-[disabled]:pointer-events-none data-[disabled]:opacity-40', className)} data-slot="dropdown-menu-checkbox-item" {...props}><span className="absolute left-2 flex size-4 items-center justify-center"><DropdownMenuPrimitive.ItemIndicator><Check className="size-4" /></DropdownMenuPrimitive.ItemIndicator></span>{children}</DropdownMenuPrimitive.CheckboxItem>;
}

function DropdownMenuRadioItem({ children, className, ...props }: React.ComponentProps<typeof DropdownMenuPrimitive.RadioItem>) {
  return <DropdownMenuPrimitive.RadioItem className={cn('relative flex cursor-default select-none items-center gap-2 rounded-lg py-1.5 pl-8 pr-2 text-[13px] outline-none hover:bg-muted focus:bg-muted data-[disabled]:pointer-events-none data-[disabled]:opacity-40', className)} data-slot="dropdown-menu-radio-item" {...props}><span className="absolute left-2 flex size-4 items-center justify-center"><DropdownMenuPrimitive.ItemIndicator><Circle className="size-2 fill-current" /></DropdownMenuPrimitive.ItemIndicator></span>{children}</DropdownMenuPrimitive.RadioItem>;
}

function DropdownMenuLabel({ className, inset, ...props }: React.ComponentProps<typeof DropdownMenuPrimitive.Label> & { inset?: boolean }) {
  return <DropdownMenuPrimitive.Label className={cn('px-2 py-1.5 text-[11.5px] font-medium text-fg-caption', inset && 'pl-8', className)} data-slot="dropdown-menu-label" {...props} />;
}

function DropdownMenuSeparator({ className, ...props }: React.ComponentProps<typeof DropdownMenuPrimitive.Separator>) {
  return <DropdownMenuPrimitive.Separator className={cn('-mx-1 my-1 h-px bg-border', className)} data-slot="dropdown-menu-separator" {...props} />;
}

function DropdownMenuShortcut({ className, ...props }: React.ComponentProps<'span'>) {
  return <span className={cn('ml-auto text-[11px] text-fg-caption', className)} data-slot="dropdown-menu-shortcut" {...props} />;
}

function DropdownMenuSub({ ...props }: React.ComponentProps<typeof DropdownMenuPrimitive.Sub>) {
  return <DropdownMenuPrimitive.Sub data-slot="dropdown-menu-sub" {...props} />;
}

function DropdownMenuSubTrigger({ children, className, inset, ...props }: React.ComponentProps<typeof DropdownMenuPrimitive.SubTrigger> & { inset?: boolean }) {
  return <DropdownMenuPrimitive.SubTrigger className={cn('flex cursor-default select-none items-center gap-2 rounded-lg px-2 py-1.5 text-[13px] outline-none hover:bg-muted focus:bg-muted', inset && 'pl-8', className)} data-slot="dropdown-menu-sub-trigger" {...props}>{children}<ChevronRight className="ml-auto size-4" /></DropdownMenuPrimitive.SubTrigger>;
}

function DropdownMenuSubContent({ className, ...props }: React.ComponentProps<typeof DropdownMenuPrimitive.SubContent>) {
  return <DropdownMenuPrimitive.SubContent className={cn('z-50 min-w-40 overflow-hidden rounded-xl border border-border bg-popover p-1 text-popover-foreground shadow-[0_18px_40px_-16px_rgba(0,0,40,.25)]', className)} data-slot="dropdown-menu-sub-content" {...props} />;
}

const DropdownMenuGroup = DropdownMenuPrimitive.Group;
const DropdownMenuPortal = DropdownMenuPrimitive.Portal;
const DropdownMenuRadioGroup = DropdownMenuPrimitive.RadioGroup;

export { DropdownMenu, DropdownMenuCheckboxItem, DropdownMenuContent, DropdownMenuGroup, DropdownMenuItem, DropdownMenuLabel, DropdownMenuPortal, DropdownMenuRadioGroup, DropdownMenuRadioItem, DropdownMenuSeparator, DropdownMenuShortcut, DropdownMenuSub, DropdownMenuSubContent, DropdownMenuSubTrigger, DropdownMenuTrigger };