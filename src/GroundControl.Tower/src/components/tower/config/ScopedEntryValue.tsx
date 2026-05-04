import { ScopeTag } from '@/components/tower/data/ScopeTag';
import { EntryValue } from './EntryValue';
import { type EntryReveal } from './use-entry-reveal';

interface ScopedEntryValueProps {
  reveal: EntryReveal;
  scopedValue: { scopes?: null | Record<string, string>; value: string };
}

export function ScopedEntryValue({ reveal, scopedValue }: ScopedEntryValueProps) {
  const scopes = Object.entries(scopedValue.scopes ?? {});

  return (
    <div className="ui-surface-panel px-4 py-3">
      <div className="flex flex-wrap gap-1.5">
        {scopes.map(([dimension, value]) => (
          <ScopeTag dimension={dimension} key={dimension} value={value} />
        ))}
      </div>
      <div className="mt-2">
        <EntryValue ariaLabel="Copy scoped value" bare reveal={reveal} scopedValue={scopedValue} />
      </div>
    </div>
  );
}
