import { Lock } from 'lucide-react';
import { CopyButton } from '@/components/tower/data/CopyButton';
import { RevealButton } from '@/components/tower/data/RevealButton';
import { ScopeTag } from '@/components/tower/data/ScopeTag';
import { SensitiveValue } from '@/components/tower/code/SensitiveValue';
import { TooltipProvider } from '@/components/ui/tooltip';
import { scopedValueKey, type EntryReveal } from './use-entry-reveal';

export type ScopeKeyPlacement = 'inline' | 'top';

interface EntryValueProps {
  ariaLabel?: string;
  bare?: boolean;
  emptyMessage?: string;
  reveal: EntryReveal;
  scopedValue: { scopes?: null | Record<string, string>; value: string } | undefined;
  /** When set, render the scope chips inline alongside or above the value. */
  scopeKey?: ScopeKeyPlacement;
}

export function EntryValue({ ariaLabel = 'Copy value', bare = false, emptyMessage = 'No value.', reveal, scopedValue, scopeKey: scopeKeyPlacement }: EntryValueProps) {
  const isTopPlacement = scopeKeyPlacement === 'top';
  const wrapperClass = bare
    ? 'ui-text-body flex items-start justify-between gap-3 leading-snug'
    : isTopPlacement
      ? 'ui-surface-panel ui-text-body grid gap-2 px-4 py-3 leading-snug'
      : 'ui-surface-panel ui-text-body flex items-center justify-between gap-3 px-4 py-1 leading-snug';
  const scopeKey = scopedValue ? scopedValueKey(scopedValue.scopes ?? {}) : '';
  const revealed = scopedValue ? reveal.isRevealed(scopeKey) : false;
  const masked = scopedValue ? reveal.isSensitive && !revealed : false;
  const displayValue = scopedValue ? (revealed ? reveal.decryptedValue(scopeKey) ?? scopedValue.value : scopedValue.value) : '';
  const pending = scopedValue ? reveal.isPending(scopeKey) : false;
  const scopeEntries = scopeKeyPlacement && scopedValue?.scopes ? Object.entries(scopedValue.scopes) : [];

  if (!scopedValue || !displayValue) {
    return (
      <TooltipProvider>
        <div className={wrapperClass}>
          <span className="flex min-h-7 items-center text-fg-caption">{emptyMessage}</span>
        </div>
      </TooltipProvider>
    );
  }

  const valueContent = masked ? (
    <span className="inline-flex items-center gap-2">
      <span className="ui-text-code text-syntax-sensitive">••••••••</span>
      <span aria-label="Sensitive value" className="inline-flex" title="Sensitive value">
        <Lock aria-hidden="true" className="size-3.5" />
      </span>
    </span>
  ) : (
    <SensitiveValue className="bg-transparent px-0" isSensitive={false} value={displayValue} />
  );

  const actions = (
    <div className="flex shrink-0 items-center gap-1">
      {reveal.isSensitive ? <RevealButton onToggle={() => reveal.toggleReveal(scopeKey)} pending={pending} revealed={revealed} /> : null}
      <CopyButton
        ariaLabel={ariaLabel}
        disabled={masked || !displayValue}
        disabledReason={masked ? 'Reveal the value first to copy it' : 'Nothing to copy'}
        value={displayValue}
      />
    </div>
  );

  if (isTopPlacement) {
    return (
      <TooltipProvider>
        <div className={wrapperClass}>
          {scopeEntries.length > 0 ? (
            <div className="flex flex-wrap gap-1.5">
              {scopeEntries.map(([dimension, value]) => (
                <ScopeTag dimension={dimension} key={dimension} value={value} />
              ))}
            </div>
          ) : null}
          <div className="flex items-center justify-between gap-3">
            <div className="min-w-0 flex-1 break-all">{valueContent}</div>
            {actions}
          </div>
        </div>
      </TooltipProvider>
    );
  }

  return (
    <TooltipProvider>
      <div className={wrapperClass}>
        <div className="flex min-w-0 flex-1 items-center gap-2 break-all">
          {scopeEntries.length > 0 ? (
            <div className="flex shrink-0 flex-wrap items-center gap-1">
              {scopeEntries.map(([dimension, value]) => (
                <ScopeTag dimension={dimension} key={dimension} size="sm" value={value} />
              ))}
            </div>
          ) : null}
          <div className="min-w-0 flex-1 break-all">{valueContent}</div>
        </div>
        {actions}
      </div>
    </TooltipProvider>
  );
}
