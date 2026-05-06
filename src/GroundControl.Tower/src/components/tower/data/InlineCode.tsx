import { Copy } from 'lucide-react';
import { useMemo, useState, type ReactNode } from 'react';
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from '@/components/ui/tooltip';
import { cn } from '@/lib/utils';

interface InlineCodeProps {
  children: ReactNode;
  className?: string;
  copyable?: boolean;
}

export function InlineCode({ children, className, copyable = false }: InlineCodeProps) {
  const [copied, setCopied] = useState(false);
  const copyText = useMemo(() => getText(children), [children]);

  if (!copyable) {
    return <code className={cn('rounded-sm bg-bg-container px-1.5 py-0.5 font-mono text-[12.5px] text-fg-body', className)}>{children}</code>;
  }

  async function copy() {
    if (!copyText || !navigator.clipboard) {
      return;
    }

    await navigator.clipboard.writeText(copyText);
    setCopied(true);
    window.setTimeout(() => setCopied(false), 1500);
  }

  return (
    <TooltipProvider>
      <Tooltip open={copied}>
        <TooltipTrigger asChild>
          <button className={cn('group inline-flex items-center gap-1 rounded-sm bg-bg-container px-1.5 py-0.5 font-mono text-[12.5px] text-fg-body hover:bg-bg-selected', className)} onClick={copy} type="button">
            <code>{children}</code>
            <Copy aria-hidden="true" className="size-3 opacity-0 transition-opacity group-hover:opacity-100" strokeWidth={1.8} />
          </button>
        </TooltipTrigger>
        <TooltipContent>Copied!</TooltipContent>
      </Tooltip>
    </TooltipProvider>
  );
}

function getText(value: ReactNode): string {
  if (typeof value === 'string' || typeof value === 'number') {
    return value.toString();
  }

  if (Array.isArray(value)) {
    return value.map(getText).join('');
  }

  return '';
}