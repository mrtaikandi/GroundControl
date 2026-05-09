import { Eye, EyeOff, Loader2 } from 'lucide-react';
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip';
import { cn } from '@/lib/utils';

interface RevealButtonProps {
  className?: string;
  onToggle: () => void;
  pending: boolean;
  revealed: boolean;
}

export function RevealButton({ className, onToggle, pending, revealed }: RevealButtonProps) {
  const label = revealed ? 'Hide value' : 'Reveal value';

  return (
    <Tooltip>
      <TooltipTrigger asChild>
        <button
          aria-label={label}
          className={cn(
            'inline-flex size-7 shrink-0 items-center justify-center rounded-md text-fg-icon-subtle transition-colors hover:bg-bg-selected hover:text-fg-body',
            'disabled:cursor-not-allowed disabled:opacity-40',
            className,
          )}
          disabled={pending}
          onClick={onToggle}
          type="button"
        >
          {pending ? (
            <Loader2 aria-hidden="true" className="size-3.5 animate-spin" />
          ) : revealed ? (
            <EyeOff aria-hidden="true" className="size-3.5" />
          ) : (
            <Eye aria-hidden="true" className="size-3.5" />
          )}
        </button>
      </TooltipTrigger>
      <TooltipContent>{label}</TooltipContent>
    </Tooltip>
  );
}
