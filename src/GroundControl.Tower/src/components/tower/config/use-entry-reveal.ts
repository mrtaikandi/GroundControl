import { useMutation } from '@tanstack/react-query';
import { useCallback, useEffect, useMemo, useState } from 'react';
import { toast } from 'sonner';
import { getConfigEntry } from '@/api/endpoints/config-entries';
import type { ConfigEntry } from '@/queries/useConfigEntries';

const SENSITIVE_MASK = '***';

export interface EntryReveal {
  decryptedValue: (scopeKey: string) => string | undefined;
  isPending: (scopeKey: string) => boolean;
  isRevealed: (scopeKey: string) => boolean;
  isSensitive: boolean;
  toggleReveal: (scopeKey: string) => void;
}

export function scopedValueKey(scopes: Record<string, string>): string {
  const entries = Object.entries(scopes).sort(([left], [right]) => left.localeCompare(right));

  return JSON.stringify(entries);
}

export function useEntryReveal(entry: ConfigEntry): EntryReveal {
  const [revealedKeys, setRevealedKeys] = useState<Set<string>>(new Set());
  const [pendingKey, setPendingKey] = useState<null | string>(null);
  const revealMutation = useMutation({
    mutationFn: ({ id }: { id: string; key: string }) => getConfigEntry(id, { decrypt: true }),
    onSuccess: (data, variables) => {
      const stillMasked = data.values.some((value) => value.value === SENSITIVE_MASK);

      if (stillMasked) {
        toast.error('You don’t have permission to reveal sensitive values.');
        setPendingKey(null);

        return;
      }

      setRevealedKeys((previous) => {
        const next = new Set(previous);
        next.add(variables.key);

        return next;
      });
      setPendingKey(null);
    },
    onError: () => {
      toast.error('Couldn’t reveal sensitive value.');
      setPendingKey(null);
    },
  });

  const entryId = entry.id;

  useEffect(() => {
    setRevealedKeys(new Set());
    setPendingKey(null);
    revealMutation.reset();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [entryId]);

  const decryptedByKey = useMemo(() => {
    if (!revealMutation.data) {
      return new Map<string, string>();
    }

    return new Map(revealMutation.data.values.map((value) => [scopedValueKey(value.scopes ?? {}), value.value]));
  }, [revealMutation.data]);

  const toggleReveal = useCallback((scopeKey: string) => {
    setRevealedKeys((previous) => {
      if (previous.has(scopeKey)) {
        const next = new Set(previous);
        next.delete(scopeKey);

        return next;
      }

      if (decryptedByKey.has(scopeKey)) {
        const next = new Set(previous);
        next.add(scopeKey);

        return next;
      }

      return previous;
    });

    if (revealedKeys.has(scopeKey) || decryptedByKey.has(scopeKey)) {
      return;
    }

    setPendingKey(scopeKey);
    revealMutation.mutate({ id: entryId, key: scopeKey });
  }, [decryptedByKey, entryId, revealMutation, revealedKeys]);

  return useMemo(() => ({
    decryptedValue: (scopeKey: string) => decryptedByKey.get(scopeKey),
    isPending: (scopeKey: string) => pendingKey === scopeKey,
    isRevealed: (scopeKey: string) => revealedKeys.has(scopeKey),
    isSensitive: entry.isSensitive,
    toggleReveal,
  }), [decryptedByKey, entry.isSensitive, pendingKey, revealedKeys, toggleReveal]);
}
