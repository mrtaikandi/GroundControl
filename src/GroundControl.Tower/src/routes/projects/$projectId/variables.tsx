import { useMutation } from '@tanstack/react-query';
import { createFileRoute, Link } from '@tanstack/react-router';
import { ChevronDown, ChevronRight, ExternalLink, Globe, Lock, Pencil, Plus, Users, Variable as VariableIcon } from 'lucide-react';
import { useEffect, useMemo, useState } from 'react';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Skeleton } from '@/components/ui/skeleton';
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from '@/components/ui/tooltip';
import { EntryValue } from '@/components/tower/config/EntryValue';
import { scopedValueKey, type EntryReveal } from '@/components/tower/config/use-entry-reveal';
import { Badge } from '@/components/tower/data/Badge';
import { InlineCode } from '@/components/tower/data/InlineCode';
import { Toolbar } from '@/components/tower/data/Toolbar';
import { VariableEditorModal } from '@/components/tower/variables/VariableEditorModal';
import { getVariable } from '@/api/endpoints/variables';
import { cn } from '@/lib/utils';
import { useGroups } from '@/queries/useGroups';
import { useProjects } from '@/queries/useProjects';
import { useVariables, type Variable } from '@/queries/useVariables';

const SENSITIVE_MASK = '***';

type VariableTier = 'project' | 'inherited';

interface DisplayVariable {
  shadowed: boolean;
  shadowsProject: boolean;
  tier: VariableTier;
  variable: Variable;
}

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
  const [filter, setFilter] = useState('');
  const [selectedId, setSelectedId] = useState<null | string>(null);
  const [projectSectionCollapsed, setProjectSectionCollapsed] = useState(false);
  const [inheritedSectionCollapsed, setInheritedSectionCollapsed] = useState(false);
  const groupNames = useMemo(() => new Map((groups.data?.data ?? []).map((group) => [group.id, group.name])), [groups.data?.data]);

  const projectItems = projectVariables.data?.data ?? [];
  const visibleGlobals = useMemo(() => {
    const globals = allGlobals.data?.data ?? [];
    return globals.filter((variable) => !variable.groupId || variable.groupId === project?.groupId);
  }, [allGlobals.data?.data, project?.groupId]);

  const projectNameSet = useMemo(() => new Set(projectItems.map((variable) => variable.name.toLowerCase())), [projectItems]);
  const globalNameSet = useMemo(() => new Set(visibleGlobals.map((variable) => variable.name.toLowerCase())), [visibleGlobals]);

  const projectDisplay: DisplayVariable[] = useMemo(() => projectItems.map<DisplayVariable>((variable) => ({
    shadowed: false,
    shadowsProject: globalNameSet.has(variable.name.toLowerCase()),
    tier: 'project',
    variable,
  })), [globalNameSet, projectItems]);

  const inheritedDisplay: DisplayVariable[] = useMemo(() => visibleGlobals.map<DisplayVariable>((variable) => ({
    shadowed: projectNameSet.has(variable.name.toLowerCase()),
    shadowsProject: false,
    tier: 'inherited',
    variable,
  })), [projectNameSet, visibleGlobals]);

  const filteredProject = useMemo(() => filterByText(projectDisplay, filter, groupNames), [filter, groupNames, projectDisplay]);
  const filteredInherited = useMemo(() => filterByText(inheritedDisplay, filter, groupNames), [filter, groupNames, inheritedDisplay]);

  const isLoading = projectVariables.isLoading || allGlobals.isLoading;
  const isEmpty = !isLoading && projectItems.length === 0 && visibleGlobals.length === 0;

  const allDisplay = useMemo(() => [...projectDisplay, ...inheritedDisplay], [inheritedDisplay, projectDisplay]);
  const selected = useMemo(() => allDisplay.find((item) => item.variable.id === selectedId) ?? null, [allDisplay, selectedId]);

  useEffect(() => {
    setSelectedId(null);
  }, [projectId]);

  useEffect(() => {
    if (selectedId !== null || allDisplay.length === 0) {
      return;
    }

    const firstVisible = filteredProject[0]?.variable.id ?? filteredInherited[0]?.variable.id;

    if (firstVisible) {
      setSelectedId(firstVisible);
    }
  }, [allDisplay.length, filteredInherited, filteredProject, selectedId]);

  return (
    <TooltipProvider>
      <div className="grid gap-4">
        <Toolbar
          startClassName="flex-1"
          start={
            <div className="flex flex-wrap items-center gap-3">
              <Input className="w-full sm:max-w-sm" onChange={(event) => setFilter(event.target.value)} placeholder="Filter variables…" value={filter} />
              <Button onClick={() => setCreating(true)} size="sm" type="button">
                <Plus aria-hidden="true" className="size-3.5" strokeWidth={2} />
                <span>New variable</span>
              </Button>
            </div>
          }
        />

        <div className="grid gap-6 xl:grid-cols-[minmax(0,1fr)_minmax(320px,600px)]">
          <div className="flex flex-col gap-3">
            {isLoading ? <Skeleton className="min-h-96 flex-1" /> : isEmpty ? (
              <div className="rounded-xl border border-dashed border-stroke-subtle bg-bg-surface p-10 text-center text-[12.5px] text-fg-caption">
                No variables yet. Use the button above to add one.
              </div>
            ) : (
              <div className="flex flex-1 flex-col overflow-hidden rounded-xl border border-stroke-subtle bg-bg-surface">
                <SectionHeader
                  collapsed={projectSectionCollapsed}
                  count={filteredProject.length}
                  description=""
                  onToggle={() => setProjectSectionCollapsed((current) => !current)}
                  title="Project variables"
                  totalCount={projectDisplay.length}
                />
                {!projectSectionCollapsed ? (
                  filteredProject.length === 0 ? (
                    <div className="px-4 py-3 text-[12px] text-fg-caption">
                      {projectDisplay.length === 0 ? 'No project variables defined.' : 'No project variables match the filter.'}
                    </div>
                  ) : (
                    filteredProject.map((item) => (
                      <VariableRow
                        item={item}
                        key={item.variable.id}
                        onEdit={setEditingVariable}
                        onSelect={setSelectedId}
                        ownerLabelFor={ownerLabelFor(groupNames)}
                        selected={selectedId === item.variable.id}
                      />
                    ))
                  )
                ) : null}

                {inheritedDisplay.length > 0 ? (
                  <>
                    <SectionHeader
                      collapsed={inheritedSectionCollapsed}
                      count={filteredInherited.length}
                      description="read-only · resolved from group and global variables"
                      onToggle={() => setInheritedSectionCollapsed((current) => !current)}
                      title="Inherited variables"
                      totalCount={inheritedDisplay.length}
                    />
                    {!inheritedSectionCollapsed ? (
                      filteredInherited.length === 0 ? (
                        <div className="px-4 py-3 text-[12px] text-fg-caption">No globals match the filter.</div>
                      ) : (
                        filteredInherited.map((item) => (
                          <VariableRow
                            item={item}
                            key={item.variable.id}
                            onEdit={setEditingVariable}
                            onSelect={setSelectedId}
                            ownerLabelFor={ownerLabelFor(groupNames)}
                            selected={selectedId === item.variable.id}
                          />
                        ))
                      )
                    ) : null}
                  </>
                ) : null}
              </div>
            )}
          </div>

          <div className="self-start">
            <VariableDetailPanel
              groupNames={groupNames}
              item={selected}
              onAddOverride={() => selected && selected.tier === 'project' && setEditingVariable(selected.variable)}
              onEdit={() => selected && selected.tier === 'project' && setEditingVariable(selected.variable)}
            />
          </div>
        </div>

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
    </TooltipProvider>
  );
}

interface SectionHeaderProps {
  collapsed: boolean;
  count: number;
  description: string;
  onToggle: () => void;
  title: string;
  totalCount: number;
}

function SectionHeader({ collapsed, count, description, onToggle, title, totalCount }: SectionHeaderProps) {
  return (
    <button
      className="flex items-center justify-between gap-3 border-b border-stroke-subtle bg-bg-container px-4 py-2 text-left transition-colors hover:bg-bg-selected/40"
      onClick={onToggle}
      type="button"
    >
      <div className="flex items-center gap-2">
        {collapsed ? <ChevronRight aria-hidden="true" className="size-3.5 text-fg-icon-subtle" /> : <ChevronDown aria-hidden="true" className="size-3.5 text-fg-icon-subtle" />}
        <span className="font-mono text-[11px] uppercase tracking-wide text-fg-caption">{title}</span>
        <span className="rounded-md bg-bg-surface px-1.5 py-0.5 font-mono text-[11px] text-fg-caption">{count === totalCount ? totalCount : `${count}/${totalCount}`}</span>
      </div>
      <span className="text-[11.5px] text-fg-caption">{description}</span>
    </button>
  );
}

interface VariableRowProps {
  item: DisplayVariable;
  onEdit: (variable: Variable) => void;
  onSelect: (id: string) => void;
  ownerLabelFor: (variable: Variable) => string;
  selected: boolean;
}

function VariableRow({ item, onEdit, onSelect, ownerLabelFor, selected }: VariableRowProps) {
  const { tier, variable, shadowed, shadowsProject } = item;
  const overrides = variable.values.filter((value) => value.scopes && Object.keys(value.scopes).length > 0).length;
  const summaryParts: string[] = [];

  if (tier === 'project') {
    if (shadowsProject) {
      summaryParts.push('shadows global');
    }

    if (overrides > 0) {
      summaryParts.push(`${overrides} override${overrides === 1 ? '' : 's'}`);
    }
  } else {
    summaryParts.push(ownerLabelFor(variable));

    if (variable.isSensitive) {
      summaryParts.push('sensitive');
    }

    if (shadowed) {
      summaryParts.push('shadowed here');
    }
  }

  const Icon = tier === 'project'
    ? variable.isSensitive ? Lock : VariableIcon
    : variable.groupId ? Users : Globe;

  return (
    <div
      className={cn(
        'group relative grid w-full cursor-pointer items-center gap-3 border-b border-stroke-subtle px-4 py-2.5 text-left text-[13px] last:border-b-0 hover:bg-bg-container',
        'grid-cols-[16px_minmax(0,1fr)] sm:grid-cols-[16px_minmax(0,1fr)_auto]',
        selected && 'bg-bg-selected before:absolute before:inset-y-0 before:left-0 before:w-[2px] before:bg-primary',
        shadowed && 'opacity-70',
      )}
      onClick={() => onSelect(variable.id)}
      onKeyDown={(event) => { if (event.key === 'Enter' || event.key === ' ') { event.preventDefault(); onSelect(variable.id); } }}
      role="button"
      tabIndex={0}
    >
      <Icon aria-hidden="true" className="size-3.5 text-fg-icon-subtle" />
      <span className="flex min-w-0 flex-wrap items-baseline gap-x-2 gap-y-0.5">
        <span className="font-mono text-[13px] font-semibold text-fg-heading [overflow-wrap:anywhere]">{variable.name}</span>
        {summaryParts.length > 0 ? (
          <span className="text-[12px] text-fg-caption">{summaryParts.join(' · ')}</span>
        ) : null}
      </span>
      <div className="col-start-2 flex flex-wrap justify-start gap-1 opacity-100 transition-opacity sm:col-start-auto sm:justify-end sm:opacity-0 sm:group-hover:opacity-100 sm:focus-within:opacity-100">
        {tier === 'project' ? (
          <Button onClick={(event) => { event.stopPropagation(); onEdit(variable); }} size="sm" type="button" variant="ghost">Edit</Button>
        ) : (
          <Tooltip>
            <TooltipTrigger asChild>
              <Button asChild onClick={(event) => event.stopPropagation()} size="sm" type="button" variant="ghost">
                <Link to="/variables">
                  <VariableIcon aria-hidden="true" className="size-3.5" />
                  Variables
                </Link>
              </Button>
            </TooltipTrigger>
            <TooltipContent>Open {variable.name} in Globals</TooltipContent>
          </Tooltip>
        )}
      </div>
    </div>
  );
}

interface VariableDetailPanelProps {
  groupNames: Map<string, string>;
  item: DisplayVariable | null;
  onAddOverride: () => void;
  onEdit: () => void;
}

function VariableDetailPanel({ groupNames, item, onAddOverride, onEdit }: VariableDetailPanelProps) {
  if (!item) {
    return (
      <div className="rounded-xl border border-dashed border-stroke-subtle bg-bg-container p-10 text-center text-[12.5px] text-fg-caption">
        Select a variable to see its details.
      </div>
    );
  }

  return <VariableDetailPanelBody groupNames={groupNames} item={item} key={item.variable.id} onAddOverride={onAddOverride} onEdit={onEdit} />;
}

function VariableDetailPanelBody({ groupNames, item, onAddOverride, onEdit }: { groupNames: Map<string, string>; item: DisplayVariable; onAddOverride: () => void; onEdit: () => void }) {
  const { tier, variable, shadowed, shadowsProject } = item;
  const reveal = useVariableReveal(variable);
  const defaultRow = variable.values.find((value) => !value.scopes || Object.keys(value.scopes).length === 0);
  const overrides = variable.values.filter((value) => value.scopes && Object.keys(value.scopes).length > 0);
  const isProject = tier === 'project';

  return (
    <div className="rounded-xl border border-stroke-subtle bg-bg-container p-6">
      <div className="flex items-start justify-between gap-3">
        <div className="min-w-0">
          <div className="text-[11px] font-medium uppercase text-fg-caption">{isProject ? 'Project variable' : 'Inherited variable'}</div>
          <h2 className="mt-2"><InlineCode className="text-[20px] font-semibold [overflow-wrap:anywhere]">{variable.name}</InlineCode></h2>
        </div>
        {isProject ? (
          <Button onClick={onEdit} size="sm" type="button" variant="secondary">
            <Pencil aria-hidden="true" className="size-3.5" />Edit
          </Button>
        ) : null}
      </div>

      <div className="mt-3 flex flex-wrap items-center gap-2">
        {variable.isSensitive ? <Badge icon={Lock} tone="mono" variant="selected">sensitive</Badge> : null}
        {isProject && shadowsProject ? (
          <Tooltip>
            <TooltipTrigger asChild>
              <span><Badge tone="mono" variant="warning">shadows global</Badge></span>
            </TooltipTrigger>
            <TooltipContent>A global with the same name is overridden by this project variable.</TooltipContent>
          </Tooltip>
        ) : null}
        {!isProject && shadowed ? (
          <Tooltip>
            <TooltipTrigger asChild>
              <span><Badge tone="mono" variant="warning">shadowed here</Badge></span>
            </TooltipTrigger>
            <TooltipContent>A project variable with the same name overrides this global for this project.</TooltipContent>
          </Tooltip>
        ) : null}
        <TierPill groupNames={groupNames} variable={variable} />
      </div>

      {variable.description ? (
        <p className="mt-4 text-[13.5px] text-fg-body [overflow-wrap:anywhere]">{variable.description}</p>
      ) : (
        <p className="mt-4 text-[13px] italic text-fg-caption">No description set.</p>
      )}

      <div className="mt-6">
        <div className="text-[11px] font-medium uppercase text-fg-caption">Default value</div>
        <div className="mt-2">
          <EntryValue ariaLabel="Copy default value" emptyMessage="No default value." reveal={reveal} scopedValue={defaultRow} />
        </div>
      </div>

      <div className="mt-6">
        <div className="flex items-center justify-between gap-3">
          <div className="text-[11px] font-medium uppercase text-fg-caption">Scoped values</div>
          {isProject ? (
            <button
              className="inline-flex items-center gap-1 text-[12px] font-medium text-fg-link transition-colors hover:underline"
              onClick={onAddOverride}
              type="button"
            >
              <Plus aria-hidden="true" className="size-3.5" strokeWidth={2} />
              Add override
            </button>
          ) : null}
        </div>
        <div className="mt-2 grid gap-2">
          {overrides.length === 0 ? (
            <div className="rounded-lg border border-dashed border-stroke-subtle p-6 text-center text-[13px] text-fg-caption">No scoped overrides defined.</div>
          ) : overrides.map((value, index) => (
            <EntryValue ariaLabel="Copy scoped value" key={`${scopedValueKey(value.scopes ?? {})}-${index}`} reveal={reveal} scopedValue={value} scopeKey="top" />
          ))}
        </div>
      </div>

      <div className="mt-6 flex flex-wrap items-center justify-between gap-2 border-t border-stroke-subtle pt-4 text-[11.5px] text-fg-caption">
        <span>Updated {formatDateTime(variable.updatedAt)}</span>
        {!isProject ? (
          <Link className="inline-flex items-center gap-1 text-fg-link transition-colors hover:underline" to="/variables">
            Open in Globals
            <ExternalLink aria-hidden="true" className="size-3" strokeWidth={1.8} />
          </Link>
        ) : null}
      </div>
    </div>
  );
}

function TierPill({ groupNames, variable }: { groupNames: Map<string, string>; variable: Variable }) {
  if (variable.projectId) {
    return <Badge tone="mono" variant="selected">Project · this scope</Badge>;
  }

  if (variable.groupId) {
    return <Badge icon={Users} tone="mono" variant="info">Group · {groupNames.get(variable.groupId) ?? variable.groupId}</Badge>;
  }

  return <Badge icon={Globe} tone="mono" variant="success">Global</Badge>;
}

function ownerLabelFor(groupNames: Map<string, string>) {
  return (variable: Variable) => {
    if (variable.groupId) {
      return groupNames.get(variable.groupId) ?? 'group';
    }

    return 'global';
  };
}

function useVariableReveal(variable: Variable): EntryReveal {
  const [revealedKeys, setRevealedKeys] = useState<Set<string>>(new Set());
  const [decryptedByKey, setDecryptedByKey] = useState<Map<string, string>>(new Map());
  const [pendingKey, setPendingKey] = useState<null | string>(null);

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

function filterByText(items: DisplayVariable[], search: string, groupNames: Map<string, string>) {
  const needle = search.trim().toLowerCase();

  if (!needle) {
    return items;
  }

  return items.filter(({ variable }) => {
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
