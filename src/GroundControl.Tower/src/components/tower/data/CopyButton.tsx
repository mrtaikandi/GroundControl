import { Check, Copy } from 'lucide-react';
import { useState } from 'react';
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip';
import { cn } from '@/lib/utils';

interface CopyButtonProps {
  ariaLabel?: string;
  className?: string;
  disabled?: boolean;
  disabledReason?: string;
  value: string;
}

export function CopyButton({ ariaLabel = 'Copy value', className, disabled = false, disabledReason, value }: CopyButtonProps) {
  const [copied, setCopied] = useState(false);

  async function copy() {
    if (disabled || !value || !navigator.clipboard) {
      return;
    }

    await navigator.clipboard.writeText(value);
    setCopied(true);
    window.setTimeout(() => setCopied(false), 1500);
  }

  return (
    <Tooltip open={copied || undefined}>
      <TooltipTrigger asChild>
        <button
          aria-label={ariaLabel}
          className={cn(
            'inline-flex size-7 shrink-0 items-center justify-center rounded-md text-fg-icon-subtle transition-colors',
            disabled ? 'cursor-not-allowed opacity-40' : 'hover:bg-bg-selected hover:text-fg-body',
            className,
          )}
          disabled={disabled}
          onClick={copy}
          type="button"
        >
          {copied ? <Check aria-hidden="true" className="size-3.5" /> : <Copy aria-hidden="true" className="size-3.5" />}
        </button>
      </TooltipTrigger>
      <TooltipContent>{copied ? 'Copied!' : disabled ? disabledReason ?? ariaLabel : ariaLabel}</TooltipContent>
    </Tooltip>
  );
}
