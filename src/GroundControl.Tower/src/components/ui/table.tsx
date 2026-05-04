import * as React from 'react';
import { cn } from '@/lib/utils';

function Table({ className, ...props }: React.ComponentProps<'table'>) {
  return <table className={cn('ui-text-body w-full caption-bottom', className)} data-slot="table" {...props} />;
}

function TableHeader({ className, ...props }: React.ComponentProps<'thead'>) {
  return <thead className={cn('[&_tr]:border-b', className)} data-slot="table-header" {...props} />;
}

function TableBody({ className, ...props }: React.ComponentProps<'tbody'>) {
  return <tbody className={cn('[&_tr:last-child]:border-0', className)} data-slot="table-body" {...props} />;
}

function TableFooter({ className, ...props }: React.ComponentProps<'tfoot'>) {
  return <tfoot className={cn('border-t bg-muted font-medium', className)} data-slot="table-footer" {...props} />;
}

function TableRow({ className, ...props }: React.ComponentProps<'tr'>) {
  return <tr className={cn('border-b border-border transition-colors hover:bg-muted/60 data-[state=selected]:bg-accent', className)} data-slot="table-row" {...props} />;
}

function TableHead({ className, ...props }: React.ComponentProps<'th'>) {
  return <th className={cn('ui-text-caption h-10 px-3 text-left align-middle font-medium text-muted-foreground', className)} data-slot="table-head" {...props} />;
}

function TableCell({ className, ...props }: React.ComponentProps<'td'>) {
  return <td className={cn('px-3 py-2 align-middle', className)} data-slot="table-cell" {...props} />;
}

function TableCaption({ className, ...props }: React.ComponentProps<'caption'>) {
  return <caption className={cn('ui-text-caption mt-4 text-muted-foreground', className)} data-slot="table-caption" {...props} />;
}

export { Table, TableBody, TableCaption, TableCell, TableFooter, TableHead, TableHeader, TableRow };