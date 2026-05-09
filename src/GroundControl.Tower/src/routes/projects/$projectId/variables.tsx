import { useMutation } from '@tanstack/react-query';
import { createFileRoute, Link } from '@tanstack/react-router';
import { ExternalLink, Globe, Lock, Plus, Users } from 'lucide-react';
import { useEffect, useMemo, useState } from 'react';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { EntryValue } from '@/components/tower/config/EntryValue';
import { scopedValueKey, type EntryReveal } from '@/components/tower/config/use-entry-reveal';
import { Badge } from '@/components/tower/data/Badge';
import { SearchFilterPopover } from '@/components/tower/data/SearchFilterPopover';
import { VariableEditorModal } from '@/components/tower/variables/VariableEditorModal';
import { getVariable } from '@/api/endpoints/variables';
import { useGroups } from '@/queries/useGroups';
import { useProjects } from '@/queries/useProjects';
import { useVariables, type Variable } from '@/queries/useVariables';

const SENSITIVE_MASK = '***';

export const Route = createFileRoute('/projects/$projectId/variables')({
  component: ProjectVariablesRoute,
});

function ProjectVariablesRoute() {
  const { projectId } = Route.useParams();
  const projects = useProjects();
  const project = projects.data?.data.find((candidate) => candidate.id === projectId);
  const projectVariables = useVariables({ ProjectId: projectId, Scope: 1 });
  const allGlobals = useVariables({ Scope: 0 });
  const groups = useGroups();
  const [creating, setCreating] = useState(false);
  const [editingVariable, setEditingVariable] = useState<Variable | undefined>();
  const [search, setSearch] = useState<string | undefined>(undefined);
  const groupNames = useMemo(() => new Map((groups.data?.data ?? []).map((group) => [group.id, group.name])), [groups.data?.data]);

  const projectItems = projectVariables.data?.data ?? [];
  const visibleGlobals = useMemo(() => {
    const globals = allGlobals.data?.data ?? [];

    return globals.filter((variable) => !variable.groupId || variable.groupId === project?.groupId);
  }, [allGlobals.data?.data, project?.groupId]);

  const projectVariableNames = useMemo(() => new Set(projectItems.map((variable) => variable.name.toLowerCase())), [projectItems]);

  const filteredProject = useMemo(() => filterByText(projectItems, search, groupNames), [groupNames, projectItems, search]);
  const filteredGlobals = useMemo(() => filterByText(visibleGlobals, search, groupNames), [groupNames, search, visibleGlobals]);

  const isLoading = projectVariables.isLoading || allGlobals.isLoading;

  return (
    <div className="grid gap-6">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <p className="text-[12.5px] text-fg-caption">
            Project variables shadow globals with the same name when this project resolves a snapshot.
          </p>
        </div>
        <div className="flex items-center gap-2">
          <SearchFilterPopover
            appliedSearch={search}
            ariaLabel="Filter variables"
            onApply={setSearch}
            placeholder="Variable name or description"
          />
          <Button onClick={() => setCreating(true)} type="button">
            <Plus aria-hidden="true" className="size-3.5" strokeWidth={2} />
            <span>New Project Variable</span>
          </Button>
        </div>
      </div>

      <section className="grid gap-3">
        <SectionHeader count={projectItems.length} description="" title="Project Variables" />
        {isLoading ? <Skeleton className="h-40" /> : null}
        {!isLoading && projectItems.length === 0 ? (
          <div className="rounded-xl border border-dashed border-stroke-subtle bg-bg-surface p-6 text-center text-[12.5px] text-fg-caption">
            No project variables. Use the button above to add one.
          </div>
        ) : null}
        {!isLoading && projectItems.length > 0 && filteredProject.length === 0 ? (
          <div className="rounded-xl border border-stroke-subtle bg-bg-surface p-6 text-center text-[12.5px] text-fg-caption">
            No project variables match the current filter.
          </div>
        ) : null}
        {filteredProject.length > 0 ? (
          <div className="overflow-hidden rounded-xl border border-stroke-subtle bg-bg-surface">
            <ul className="grid divide-y divide-stroke-subtle">
              {filteredProject.map((variable) => (
                <ProjectVariableRow
                  key={variable.id}
                  onEdit={() => setEditingVariable(variable)}
                  shadowsGlobal={visibleGlobals.some((global) => global.name.toLowerCase() === variable.name.toLowerCase())}
                  variable={variable}
                />
              ))}
            </ul>
          </div>
        ) : null}
      </section>

      <section className="grid gap-3">
        <SectionHeader count={visibleGlobals.length} description="" title="Inherited Variables" />
        {isLoading ? <Skeleton className="h-40" /> : null}
        {!isLoading && visibleGlobals.length === 0 ? (
          <div className="rounded-xl border border-dashed border-stroke-subtle bg-bg-surface p-6 text-center text-[12.5px] text-fg-caption">
            No globals are visible to this project.
          </div>
        ) : null}
        {!isLoading && visibleGlobals.length > 0 && filteredGlobals.length === 0 ? (
          <div className="rounded-xl border border-stroke-subtle bg-bg-surface p-6 text-center text-[12.5px] text-fg-caption">
            No globals match the current filter.
          </div>
        ) : null}
        {filteredGlobals.length > 0 ? (
          <div className="overflow-hidden rounded-xl border border-stroke-subtle bg-bg-surface">
            <ul className="grid divide-y divide-stroke-subtle">
              {filteredGlobals.map((variable) => (
                <GlobalVariableRow
                  groupNames={groupNames}
                  key={variable.id}
                  shadowedByProject={projectVariableNames.has(variable.name.toLowerCase())}
                  variable={variable}
                />
              ))}
            </ul>
          </div>
        ) : null}
      </section>

      <VariableEditorModal
        mode="create"
        onOpenChange={setCreating}
        open={creating}
        tier={{ kind: 'project', projectId }}
      />
      <VariableEditorModal
        mode="edit"
        onOpenChange={(open) => !open && setEditingVariable(undefined)}
        open={Boolean(editingVariable)}
        tier={{ kind: 'project', projectId }}
        variable={editingVariable}
      />
    </div>
  );
}

interface SectionHeaderProps {
  count: number;
  description: string;
  title: string;
}

function SectionHeader({ count, description, title }: SectionHeaderProps) {
  return (
    <div className="flex items-baseline gap-3">
      <h2 className="text-[14px] font-semibold text-fg-heading">{title}</h2>
      <span className="rounded-md bg-bg-container px-2 py-0.5 font-mono text-[11px] text-fg-caption">{count}</span>
      <p className="text-[12px] text-fg-caption">{description}</p>
    </div>
  );
}

interface ProjectVariableRowProps {
  onEdit: () => void;
  shadowsGlobal: boolean;
  variable: Variable;
}

function ProjectVariableRow({ onEdit, shadowsGlobal, variable }: ProjectVariableRowProps) {
  const reveal = useVariableReveal(variable);
  const defaultRow = useMemo(() => variable.values.find((value) => !value.scopes || Object.keys(value.scopes).length === 0), [variable.values]);
  const overrides = useMemo(() => variable.values.filter((value) => value.scopes && Object.keys(value.scopes).length > 0), [variable.values]);

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
            <h3 className="font-mono text-[13.5px] font-semibold text-fg-heading [overflow-wrap:anywhere]">{variable.name}</h3>
            {variable.isSensitive ? <Badge icon={Lock} tone="mono" variant="selected">sensitive</Badge> : null}
            {shadowsGlobal ? <Badge tone="mono" variant="warning">shadows global</Badge> : null}
            {overrides.length > 0 ? (
              <Badge tone="mono" variant="selected">{overrides.length} override{overrides.length === 1 ? '' : 's'}</Badge>
            ) : null}
          </div>
          {variable.description ? <p className="mt-2 text-[12.5px] text-fg-body [overflow-wrap:anywhere]">{variable.description}</p> : null}
          <div className="mt-2 grid gap-1.5" onClick={(event) => event.stopPropagation()} onKeyDown={(event) => event.stopPropagation()} role="presentation">
            <EntryValue
              ariaLabel="Copy variable value"
              emptyMessage="—"
              reveal={reveal}
              scopedValue={defaultRow ?? { scopes: null, value: '' }}
            />
            {overrides.map((override, index) => (
              <EntryValue
                ariaLabel="Copy variable value"
                emptyMessage="—"
                key={`${variable.id}-${index}`}
                reveal={reveal}
                scopeKey="inline"
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

interface GlobalVariableRowProps {
  groupNames: Map<string, string>;
  shadowedByProject: boolean;
  variable: Variable;
}

function GlobalVariableRow({ groupNames, shadowedByProject, variable }: GlobalVariableRowProps) {
  const reveal = useVariableReveal(variable);
  const defaultRow = useMemo(() => variable.values.find((value) => !value.scopes || Object.keys(value.scopes).length === 0), [variable.values]);
  const overrides = useMemo(() => variable.values.filter((value) => value.scopes && Object.keys(value.scopes).length > 0), [variable.values]);
  const isSystemWide = !variable.groupId;

  return (
    <li>
      <div className={`grid grid-cols-[minmax(0,1fr)_auto] items-start gap-x-4 px-[18px] py-[14px] ${shadowedByProject ? 'opacity-70' : ''}`}>
        <div className="min-w-0">
          <div className="flex min-w-0 flex-wrap items-center gap-x-3 gap-y-1">
            <h3 className="font-mono text-[13.5px] font-semibold text-fg-heading [overflow-wrap:anywhere]">{variable.name}</h3>
            {variable.isSensitive ? <Badge icon={Lock} tone="mono" variant="selected">sensitive</Badge> : null}
            {isSystemWide ? (
              <Badge icon={Globe} tone="mono" variant="success">global</Badge>
            ) : (
              <Badge icon={Users} tone="mono" variant="info">group · {groupNames.get(variable.groupId!) ?? variable.groupId}</Badge>
            )}
            {shadowedByProject ? <Badge tone="mono" variant="warning">shadowed here</Badge> : null}
            {overrides.length > 0 ? (
              <Badge tone="mono" variant="selected">{overrides.length} override{overrides.length === 1 ? '' : 's'}</Badge>
            ) : null}
          </div>
          {variable.description ? <p className="mt-2 text-[12.5px] text-fg-body [overflow-wrap:anywhere]">{variable.description}</p> : null}
          <div className="mt-2 grid gap-1.5">
            <EntryValue
              ariaLabel="Copy variable value"
              emptyMessage="—"
              reveal={reveal}
              scopedValue={defaultRow ?? { scopes: null, value: '' }}
            />
            {overrides.map((override, index) => (
              <EntryValue
                ariaLabel="Copy variable value"
                emptyMessage="—"
                key={`${variable.id}-${index}`}
                reveal={reveal}
                scopeKey="inline"
                scopedValue={override}
              />
            ))}
          </div>
        </div>
        <div className="flex shrink-0 flex-col items-end gap-2 text-right text-[11.5px] text-fg-caption">
          <Link
            className="inline-flex items-center gap-1 text-fg-link hover:underline"
            to="/variables"
          >
            Open globals
            <ExternalLink aria-hidden="true" className="size-3" strokeWidth={1.8} />
          </Link>
          <span>Updated at: {formatDateTime(variable.updatedAt)}</span>
        </div>
      </div>
    </li>
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

function filterByText(items: Variable[], search: string | undefined, groupNames: Map<string, string>) {
  const needle = search?.trim().toLowerCase();

  if (!needle) {
    return items;
  }

  return items.filter((variable) => {
    const ownerText = variable.groupId
      ? `group ${groupNames.get(variable.groupId) ?? variable.groupId}`
      : variable.projectId
        ? 'project'
        : 'global';

    return variable.name.toLowerCase().includes(needle)
      || (variable.description ?? '').toLowerCase().includes(needle)
      || ownerText.toLowerCase().includes(needle);
  });
}

function formatDateTime(value: string) {
  return new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value));
}
