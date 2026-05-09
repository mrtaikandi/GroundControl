import { Lock } from 'lucide-react';
import { CopyButton } from '@/components/tower/data/CopyButton';
import { RevealButton } from '@/components/tower/data/RevealButton';
import { SensitiveValue } from '@/components/tower/code/SensitiveValue';
import { TooltipProvider } from '@/components/ui/tooltip';
import { scopedValueKey, type EntryReveal } from './use-entry-reveal';

interface EntryValueProps {
  ariaLabel?: string;
  bare?: boolean;
  emptyMessage?: string;
  reveal: EntryReveal;
  scopedValue: { scopes?: null | Record<string, string>; value: string } | undefined;
}

export function EntryValue({ ariaLabel = 'Copy value', bare = false, emptyMessage = 'No value.', reveal, scopedValue }: EntryValueProps) {
  const wrapperClass = bare
    ? 'ui-text-body flex items-start justify-between gap-3'
    : 'ui-surface-panel ui-text-body mt-2 flex items-center justify-between gap-3 px-4 py-2.5';
  const scopeKey = scopedValue ? scopedValueKey(scopedValue.scopes ?? {}) : '';
  const revealed = scopedValue ? reveal.isRevealed(scopeKey) : false;
  const masked = scopedValue ? reveal.isSensitive && !revealed : false;
  const displayValue = scopedValue ? (revealed ? reveal.decryptedValue(scopeKey) ?? scopedValue.value : scopedValue.value) : '';
  const pending = scopedValue ? reveal.isPending(scopeKey) : false;

  return (
    <TooltipProvider>
      <div className={wrapperClass}>
        {!scopedValue || !displayValue ? (
          <span className="flex min-h-7 items-center text-fg-caption">{emptyMessage}</span>
        ) : (
          <>
            <div className="min-w-0 flex-1 break-all">
              {masked ? (
                <span className="inline-flex items-center gap-2">
                  <span className="ui-text-code text-syntax-sensitive">••••••••</span>
                  <span aria-label="Sensitive value" className="inline-flex" title="Sensitive value">
                    <Lock aria-hidden="true" className="size-3.5" />
                  </span>
                </span>
              ) : (
                <SensitiveValue className="bg-transparent px-0" isSensitive={false} value={displayValue} />
              )}
            </div>
            <div className="flex shrink-0 items-center gap-1">
              {reveal.isSensitive ? <RevealButton onToggle={() => reveal.toggleReveal(scopeKey)} pending={pending} revealed={revealed} /> : null}
              <CopyButton
                ariaLabel={ariaLabel}
                disabled={masked || !displayValue}
                disabledReason={masked ? 'Reveal the value first to copy it' : 'Nothing to copy'}
                value={displayValue}
              />
            </div>
          </>
        )}
      </div>
    </TooltipProvider>
  );
}

