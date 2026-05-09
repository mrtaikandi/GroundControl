import { useMutation } from '@tanstack/react-query';
import { createFileRoute } from '@tanstack/react-router';
import { Globe, Lock, Plus, Users } from 'lucide-react';
import { useEffect, useMemo, useState } from 'react';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { TooltipProvider } from '@/components/ui/tooltip';
import { EntryValue } from '@/components/tower/config/EntryValue';
import { scopedValueKey, type EntryReveal } from '@/components/tower/config/use-entry-reveal';
import { Badge } from '@/components/tower/data/Badge';
import { ScopeTag } from '@/components/tower/data/ScopeTag';
import { SearchFilterPopover } from '@/components/tower/data/SearchFilterPopover';
import { SegmentedControl } from '@/components/tower/data/SegmentedControl';
import { VariableEditorModal } from '@/components/tower/variables/VariableEditorModal';
import { PageHeader } from '@/components/tower/shell/PageHeader';
import { PageContent } from '@/components/tower/shell/PageContent';
import { getVariable } from '@/api/endpoints/variables';
import { useGroups } from '@/queries/useGroups';
import { useVariables, type Variable } from '@/queries/useVariables';

const SENSITIVE_MASK = '***';

type TierFilter = 'all' | 'global' | 'group';

export const Route = createFileRoute('/variables')({
  component: VariablesRoute,
});

function VariablesRoute() {
  const variables = useVariables({ Scope: 0 });
  const groups = useGroups();
  const [creating, setCreating] = useState(false);
  const [editingVariable, setEditingVariable] = useState<Variable | undefined>();
  const [search, setSearch] = useState<string | undefined>(undefined);
  const [tierFilter, setTierFilter] = useState<TierFilter>('all');
  const groupNames = useMemo(() => new Map((groups.data?.data ?? []).map((group) => [group.id, group.name])), [groups.data?.data]);
  const items = variables.data?.data ?? [];

  const filtered = useMemo(() => {
    const needle = search?.trim().toLowerCase();

    return items.filter((variable) => {
      if (tierFilter === 'global' && variable.groupId) {
        return false;
      }

      if (tierFilter === 'group' && !variable.groupId) {
        return false;
      }

      if (!needle) {
        return true;
      }

      const ownerText = ownerLabel(variable, groupNames).toLowerCase();
      return variable.name.toLowerCase().includes(needle)
        || (variable.description ?? '').toLowerCase().includes(needle)
        || ownerText.includes(needle);
    });
  }, [groupNames, items, search, tierFilter]);

  return (
    <>
      <PageHeader
        actions={(
          <div className="flex items-center gap-2">
            <SearchFilterPopover
              appliedSearch={search}
              ariaLabel="Filter variables"
              onApply={setSearch}
              placeholder="Variable name, owner, or description"
            />
            <Button onClick={() => setCreating(true)} type="button">
              <Plus aria-hidden="true" className="size-3.5" strokeWidth={2} />
              <span>New Variable</span>
            </Button>
          </div>
        )}
        description="Reusable values for interpolation during snapshot publishing. Project-scoped variables live on each project's Variables tab."
        title="Variables"
      />

      <PageContent>
        <div className="grid gap-5 pt-8">
          <div className="flex items-center justify-between gap-3">
            <SegmentedControl
              onChange={(next) => setTierFilter(next as TierFilter)}
              options={[
                { label: 'All', value: 'all' },
                { label: 'Global', value: 'global' },
                { label: 'Group-owned', value: 'group' },
              ]}
              value={tierFilter}
            />
            <span className="text-[11.5px] text-fg-caption">
              {filtered.length} of {items.length}
            </span>
          </div>

          {variables.isLoading ? <Skeleton className="h-80" /> : null}
          {!variables.isLoading && items.length === 0 ? (
            <div className="rounded-xl border border-stroke-subtle bg-bg-surface p-8 text-center text-fg-caption">
              No global variables yet.
            </div>
          ) : null}
          {!variables.isLoading && items.length > 0 && filtered.length === 0 ? (
            <div className="rounded-xl border border-stroke-subtle bg-bg-surface p-8 text-center text-fg-caption">
              No variables match the current filter.
            </div>
          ) : null}
          {filtered.length > 0 ? (
            <div className="overflow-hidden rounded-xl border border-stroke-subtle bg-bg-surface">
              <ul className="grid divide-y divide-stroke-subtle">
                {filtered.map((variable) => (
                  <VariableRow
                    groupNames={groupNames}
                    key={variable.id}
                    onEdit={() => setEditingVariable(variable)}
                    variable={variable}
                  />
                ))}
              </ul>
            </div>
          ) : null}
        </div>
      </PageContent>

      <VariableEditorModal
        mode="create"
        onOpenChange={setCreating}
        open={creating}
        tier={{ kind: 'global' }}
      />
      <VariableEditorModal
        mode="edit"
        onOpenChange={(open) => !open && setEditingVariable(undefined)}
        open={Boolean(editingVariable)}
        tier={{ kind: 'global' }}
        variable={editingVariable}
      />
    </>
  );
}

interface VariableRowProps {
  groupNames: Map<string, string>;
  onEdit: () => void;
  variable: Variable;
}

function VariableRow({ groupNames, onEdit, variable }: VariableRowProps) {
  const reveal = useVariableReveal(variable);
  const defaultRow = useMemo(() => variable.values.find((value) => !value.scopes || Object.keys(value.scopes).length === 0), [variable.values]);
  const overrides = useMemo(() => variable.values.filter((value) => value.scopes && Object.keys(value.scopes).length > 0), [variable.values]);
  const isSystemWide = !variable.groupId;
  const groupName = variable.groupId ? groupNames.get(variable.groupId) ?? variable.groupId : null;

  return (
    <li>
      <div
        className="grid cursor-pointer grid-cols-[minmax(0,1fr)_auto] items-start gap-x-4 px-[18px] py-[14px] transition-colors hover:bg-bg-container focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-stroke-field-focus"
        onClick={onEdit}
        onKeyDown={(event) => { if (event.key === 'Enter' || event.key === ' ') { event.preventDefault(); onEdit(); } }}
        role="button"
        tabIndex={0}
      >
        <div className="min-w-0">
          <div className="flex min-w-0 flex-wrap items-center gap-x-3 gap-y-1">
            <h2 className="font-mono text-[13.5px] font-semibold text-fg-heading [overflow-wrap:anywhere]">{variable.name}</h2>
            {variable.isSensitive ? <Badge icon={Lock} tone="mono" variant="selected">sensitive</Badge> : null}
            {isSystemWide ? (
              <Badge icon={Globe} tone="mono" variant="success">global</Badge>
            ) : (
              <Badge icon={Users} tone="mono" variant="info">group · {groupName}</Badge>
            )}
            {overrides.length > 0 ? (
              <Badge tone="mono" variant="selected">{overrides.length} override{overrides.length === 1 ? '' : 's'}</Badge>
            ) : null}
          </div>
          {variable.description ? <p className="mt-2 text-[12.5px] text-fg-body [overflow-wrap:anywhere]">{variable.description}</p> : null}
          <div className="mt-2 grid gap-1.5" onClick={(event) => event.stopPropagation()} onKeyDown={(event) => event.stopPropagation()} role="presentation">
            <ValueRow label="default" reveal={reveal} scopedValue={defaultRow ?? { scopes: null, value: '' }} />
            {overrides.map((override, index) => (
              <ValueRow
                key={`${variable.id}-${index}`}
                scopeChips={Object.entries(override.scopes ?? {})}
                reveal={reveal}
                scopedValue={override}
              />
            ))}
          </div>
        </div>
        <div className="shrink-0 text-right text-[11.5px] text-fg-caption">
          Updated at: {formatDateTime(variable.updatedAt)}
        </div>
      </div>
    </li>
  );
}

interface ValueRowProps {
  label?: string;
  reveal: EntryReveal;
  scopeChips?: [string, string][];
  scopedValue: { scopes?: null | Record<string, string>; value: string };
}

function ValueRow({ label, reveal, scopeChips, scopedValue }: ValueRowProps) {
  return (
    <TooltipProvider>
      <div className="grid max-w-3xl grid-cols-[auto_minmax(0,1fr)] items-center gap-3">
        <div className="flex flex-wrap items-center gap-1.5">
          {label ? (
            <span className="font-mono text-[10.5px] uppercase tracking-wide text-fg-caption">{label}</span>
          ) : null}
          {scopeChips?.map(([dimension, value]) => (
            <ScopeTag dimension={dimension} key={dimension} size="sm" value={value} />
          ))}
        </div>
        <EntryValue ariaLabel="Copy variable value" emptyMessage="—" reveal={reveal} scopedValue={scopedValue} />
      </div>
    </TooltipProvider>
  );
}

function useVariableReveal(variable: Variable): EntryReveal {
  const [revealedKeys, setRevealedKeys] = useState<Set<string>>(new Set());
  const [decryptedByKey, setDecryptedByKey] = useState<Map<string, string>>(new Map());
  const [pendingKey, setPendingKey] = useState<string | null>(null);

  const fetchDecrypted = useMutation({
    mutationFn: () => getVariable(variable.id, { decrypt: true }),
    onError: () => {
      toast.error("Couldn't reveal sensitive value.");
      setPendingKey(null);
    },
    onSuccess: (data) => {
      if (!data) {
        setPendingKey(null);

        return;
      }

      const stillMasked = data.values.some((value) => value.value === SENSITIVE_MASK);

      if (stillMasked) {
        toast.error("You don't have permission to reveal sensitive values.");
        setPendingKey(null);

        return;
      }

      const next = new Map<string, string>();
      for (const value of data.values) {
        next.set(scopedValueKey(value.scopes ?? {}), value.value);
      }

      setDecryptedByKey(next);

      if (pendingKey !== null) {
        setRevealedKeys((current) => {
          const updated = new Set(current);
          updated.add(pendingKey);

          return updated;
        });
      }

      setPendingKey(null);
    },
  });

  useEffect(() => {
    setRevealedKeys(new Set());
    setDecryptedByKey(new Map());
    setPendingKey(null);
    fetchDecrypted.reset();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [variable.id, variable.version]);

  return useMemo<EntryReveal>(() => ({
    decryptedValue: (key: string) => decryptedByKey.get(key),
    isPending: (key: string) => pendingKey === key,
    isRevealed: (key: string) => revealedKeys.has(key),
    isSensitive: variable.isSensitive,
    toggleReveal: (key: string) => {
      if (revealedKeys.has(key)) {
        setRevealedKeys((current) => {
          const next = new Set(current);
          next.delete(key);

          return next;
        });

        return;
      }

      if (decryptedByKey.has(key)) {
        setRevealedKeys((current) => {
          const next = new Set(current);
          next.add(key);

          return next;
        });

        return;
      }

      setPendingKey(key);
      fetchDecrypted.mutate();
    },
  }), [decryptedByKey, fetchDecrypted, pendingKey, revealedKeys, variable.isSensitive]);
}

function ownerLabel(variable: Variable, groupNames: Map<string, string>) {
  if (variable.groupId) {
    return `group · ${groupNames.get(variable.groupId) ?? variable.groupId}`;
  }

  return 'global';
}

function formatDateTime(value: string) {
  return new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value));
}
