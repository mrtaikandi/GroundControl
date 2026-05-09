import { useMutation } from '@tanstack/react-query';
import { createFileRoute } from '@tanstack/react-router';
import { Lock, Plus } from 'lucide-react';
import { useEffect, useId, useMemo, useState } from 'react';
import { toast } from 'sonner';
import { AlertDialog, AlertDialogAction, AlertDialogCancel, AlertDialogContent, AlertDialogDescription, AlertDialogFooter, AlertDialogHeader, AlertDialogTitle } from '@/components/ui/alert-dialog';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Skeleton } from '@/components/ui/skeleton';
import { Textarea } from '@/components/ui/textarea';
import { TooltipProvider } from '@/components/ui/tooltip';
import { EntryValue } from '@/components/tower/config/EntryValue';
import type { EntryReveal } from '@/components/tower/config/use-entry-reveal';
import { RevealButton } from '@/components/tower/data/RevealButton';
import { SearchFilterPopover } from '@/components/tower/data/SearchFilterPopover';
import { SegmentedControl } from '@/components/tower/data/SegmentedControl';
import { PageHeader } from '@/components/tower/shell/PageHeader';
import { PageContent } from '@/components/tower/shell/PageContent';
import { getVariable } from '@/api/endpoints/variables';
import { useGroups } from '@/queries/useGroups';
import { useProjects } from '@/queries/useProjects';
import { useCreateVariable, useDeleteVariable, useUpdateVariable, useVariables, type Variable } from '@/queries/useVariables';

const SENSITIVE_MASK = '***';

type VariableTier = 'group' | 'project';

export const Route = createFileRoute('/variables')({
  component: VariablesRoute,
});

function VariablesRoute() {
  const variables = useVariables();
  const projects = useProjects();
  const groups = useGroups();
  const [creating, setCreating] = useState(false);
  const [editingVariable, setEditingVariable] = useState<Variable | undefined>();
  const [search, setSearch] = useState<string | undefined>(undefined);
  const projectNames = useMemo(() => new Map((projects.data?.data ?? []).map((project) => [project.id, project.name])), [projects.data?.data]);
  const groupNames = useMemo(() => new Map((groups.data?.data ?? []).map((group) => [group.id, group.name])), [groups.data?.data]);
  const items = variables.data?.data ?? [];

  const filtered = useMemo(() => {
    const needle = search?.trim().toLowerCase();
    if (!needle) {
      return items;
    }

    return items.filter((variable) => {
      const ownerText = ownerLabel(variable, projectNames, groupNames).toLowerCase();
      return variable.name.toLowerCase().includes(needle) || (variable.description ?? '').toLowerCase().includes(needle) || ownerText.includes(needle);
    });
  }, [groupNames, items, projectNames, search]);

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
              <span>New variable</span>
            </Button>
          </div>
        )}
        description="Reusable values for interpolation during snapshot publishing."
        title="Variables"
      />

      <PageContent>
        <div className="grid gap-8 pt-8">
          {variables.isLoading ? <Skeleton className="h-80" /> : null}
          {!variables.isLoading && items.length === 0 ? <div className="rounded-xl border border-stroke-subtle bg-bg-surface p-8 text-center text-fg-caption">No variables yet.</div> : null}
          {!variables.isLoading && items.length > 0 && filtered.length === 0 ? <div className="rounded-xl border border-stroke-subtle bg-bg-surface p-8 text-center text-fg-caption">No variables match the current filter.</div> : null}
          {filtered.length > 0 ? (
            <div className="overflow-hidden rounded-xl border border-stroke-subtle bg-bg-surface">
              <ul className="grid divide-y divide-stroke-subtle">
                {filtered.map((variable) => (
                  <VariableRow
                    key={variable.id}
                    onEdit={() => setEditingVariable(variable)}
                    ownerText={ownerLabel(variable, projectNames, groupNames)}
                    variable={variable}
                  />
                ))}
              </ul>
            </div>
          ) : null}
        </div>
      </PageContent>

      <VariableModal mode="create" onOpenChange={setCreating} open={creating} />
      <VariableModal mode="edit" onOpenChange={(open) => !open && setEditingVariable(undefined)} open={Boolean(editingVariable)} variable={editingVariable} />
    </>
  );
}

function VariableModal({ mode, onOpenChange, open, variable }: { mode: 'create' | 'edit'; onOpenChange: (open: boolean) => void; open: boolean; variable?: Variable }) {
  const projects = useProjects();
  const groups = useGroups();
  const createVariable = useCreateVariable();
  const updateVariable = useUpdateVariable();
  const deleteVariable = useDeleteVariable();
  const [name, setName] = useState('');
  const [value, setValue] = useState('');
  const [description, setDescription] = useState('');
  const [isSensitive, setIsSensitive] = useState(false);
  const [tier, setTier] = useState<VariableTier>('group');
  const [groupId, setGroupId] = useState<string | null>(null);
  const [projectId, setProjectId] = useState<string | null>(null);
  const [confirmingDelete, setConfirmingDelete] = useState(false);
  const [deleteConfirmText, setDeleteConfirmText] = useState('');
  const [valueRevealed, setValueRevealed] = useState(false);
  const deleteConfirmInputId = useId();
  const pending = createVariable.isPending || updateVariable.isPending;
  const isEdit = mode === 'edit';
  const isDeleteConfirmed = !!variable && deleteConfirmText === variable.name;
  const revealValue = useMutation({
    mutationFn: async () => {
      if (!variable) {
        throw new Error('NO_VARIABLE');
      }

      const data = await getVariable(variable.id, { decrypt: true });
      const next = data?.values.find((entry) => !entry.scopes || Object.keys(entry.scopes).length === 0)?.value ?? '';
      if (next === SENSITIVE_MASK || next === defaultValue(variable)) {
        throw new Error('NO_PERMISSION');
      }

      return next;
    },
    onError: (error) => {
      const message = (error as Error).message;
      toast.error(message === 'NO_PERMISSION' ? "You don't have permission to reveal sensitive values." : "Couldn't reveal sensitive value.");
    },
    onSuccess: (next) => {
      setValue(next);
      setValueRevealed(true);
    },
  });
  const canRevealValue = isEdit && !!variable && variable.isSensitive && value.length > 0;

  useEffect(() => {
    if (!open) {
      return;
    }

    setName(variable?.name ?? '');
    setValue(defaultValue(variable));
    setDescription(variable?.description ?? '');
    setIsSensitive(variable?.isSensitive ?? false);
    setValueRevealed(false);
    revealValue.reset();

    if (variable) {
      if (variable.projectId) {
        setTier('project');
        setProjectId(variable.projectId);
        setGroupId(null);
      } else {
        setTier('group');
        setGroupId(variable.groupId ?? null);
        setProjectId(null);
      }
    } else {
      setTier('group');
      setGroupId(null);
      setProjectId(null);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, variable]);

  function toggleValueReveal() {
    if (valueRevealed) {
      setValueRevealed(false);

      return;
    }

    if (revealValue.isSuccess) {
      setValueRevealed(true);

      return;
    }

    revealValue.mutate();
  }

  function handleSensitiveChange(checked: boolean) {
    if (!checked && variable?.isSensitive && !revealValue.isSuccess && value.length > 0) {
      revealValue.mutate(undefined, {
        onSuccess: () => setIsSensitive(false),
      });

      return;
    }

    setIsSensitive(checked);
  }

  useEffect(() => {
    if (!confirmingDelete) {
      setDeleteConfirmText('');
    }
  }, [confirmingDelete]);

  const canSave = useMemo(() => {
    if (!name.trim()) {
      return false;
    }

    if (tier === 'project' && !projectId) {
      return false;
    }

    return true;
  }, [name, projectId, tier]);

  async function save() {
    const values = [{ scopes: {}, value }];
    const trimmedDescription = description.trim() || null;

    if (mode === 'create') {
      const body = tier === 'project'
        ? { description: trimmedDescription, groupId: null, isSensitive, name: name.trim(), projectId, scope: 1 as const, values }
        : { description: trimmedDescription, groupId, isSensitive, name: name.trim(), projectId: null, scope: 0 as const, values };

      await createVariable.mutateAsync(body);
    } else if (variable) {
      await updateVariable.mutateAsync({
        body: { description: trimmedDescription, isSensitive, values },
        id: variable.id,
        version: variable.version.toString(),
      });
    }

    onOpenChange(false);
  }

  async function confirmDelete() {
    if (!variable) {
      return;
    }

    await deleteVariable.mutateAsync({ id: variable.id, version: variable.version.toString() });
    setConfirmingDelete(false);
    onOpenChange(false);
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-h-[min(760px,calc(100vh-32px))] overflow-y-auto w-[min(calc(100vw-32px),620px)]">
        <DialogHeader>
          <DialogTitle>{mode === 'create' ? 'New variable' : 'Edit variable'}</DialogTitle>
          <DialogDescription>Variables are resolved before snapshots are published. Project-tier variables override group-tier variables on key collision.</DialogDescription>
        </DialogHeader>
        <div className="grid gap-4">
          <div className="grid gap-1.5">
            <label className="text-[12px] font-medium text-fg-body" htmlFor="variable-name">Name</label>
            <Input disabled={isEdit} id="variable-name" onChange={(event) => setName(event.target.value)} placeholder="connectionString" value={name} />
          </div>

          <div className="grid gap-1.5">
            <label className="text-[12px] font-medium text-fg-body">Tier</label>
            {isEdit ? (
              <div className="rounded-lg border border-stroke-subtle bg-bg-container px-3 py-2 text-[12.5px] text-fg-caption">
                {tier === 'project' ? 'Project tier — overrides group-tier variables for one project' : 'Group tier — global by default, or scoped to a group'}
                <span className="ml-2 text-fg-icon-subtle">(locked after creation)</span>
              </div>
            ) : (
              <SegmentedControl
                onChange={(next) => setTier(next as VariableTier)}
                options={[{ label: 'Group', value: 'group' }, { label: 'Project', value: 'project' }]}
                value={tier}
              />
            )}
          </div>

          {tier === 'group' ? (
            <div className="grid gap-1.5">
              <label className="text-[12px] font-medium text-fg-body" htmlFor="variable-group">Group</label>
              {isEdit ? (
                <div className="rounded-lg border border-stroke-subtle bg-bg-container px-3 py-2 text-[12.5px] text-fg-caption [overflow-wrap:anywhere]">
                  {groupId ? groups.data?.data.find((g) => g.id === groupId)?.name ?? groupId : 'Global (no group)'}
                </div>
              ) : (
                <Select onValueChange={(next) => setGroupId(next === '__global__' ? null : next)} value={groupId ?? '__global__'}>
                  <SelectTrigger id="variable-group"><SelectValue placeholder="Pick a group" /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value="__global__">Global (no group)</SelectItem>
                    {(groups.data?.data ?? []).map((group) => <SelectItem key={group.id} value={group.id}>{group.name}</SelectItem>)}
                  </SelectContent>
                </Select>
              )}
            </div>
          ) : (
            <div className="grid gap-1.5">
              <label className="text-[12px] font-medium text-fg-body" htmlFor="variable-project">Project</label>
              {isEdit ? (
                <div className="rounded-lg border border-stroke-subtle bg-bg-container px-3 py-2 text-[12.5px] text-fg-caption [overflow-wrap:anywhere]">
                  {projectId ? projects.data?.data.find((p) => p.id === projectId)?.name ?? projectId : '—'}
                </div>
              ) : (
                <Select onValueChange={(next) => setProjectId(next)} value={projectId ?? ''}>
                  <SelectTrigger id="variable-project"><SelectValue placeholder="Pick a project" /></SelectTrigger>
                  <SelectContent>
                    {(projects.data?.data ?? []).map((project) => <SelectItem key={project.id} value={project.id}>{project.name}</SelectItem>)}
                  </SelectContent>
                </Select>
              )}
            </div>
          )}

          <div className="grid gap-1.5">
            <label className="text-[12px] font-medium text-fg-body" htmlFor="variable-value">Value</label>
            <TooltipProvider>
              <div className="relative">
                <Input
                  className={canRevealValue ? 'pr-10' : undefined}
                  id="variable-value"
                  onChange={(event) => setValue(event.target.value)}
                  type={isSensitive && !valueRevealed ? 'password' : 'text'}
                  value={value}
                />
                {canRevealValue ? (
                  <div className="absolute right-1 top-1/2 -translate-y-1/2">
                    <RevealButton onToggle={toggleValueReveal} pending={revealValue.isPending} revealed={valueRevealed} />
                  </div>
                ) : null}
              </div>
            </TooltipProvider>
          </div>
          <label className="flex items-center gap-2 text-[13px] text-fg-body">
            <input checked={isSensitive} className="size-4 accent-[var(--tower-stroke-field-focus)]" disabled={revealValue.isPending} onChange={(event) => handleSensitiveChange(event.target.checked)} type="checkbox" />
            Sensitive value
          </label>
          <div className="grid gap-1.5">
            <label className="text-[12px] font-medium text-fg-body" htmlFor="variable-description">Description</label>
            <Textarea id="variable-description" onChange={(event) => setDescription(event.target.value)} placeholder="Optional context" value={description} />
          </div>
        </div>
        <DialogFooter className={isEdit ? 'sm:justify-between' : undefined}>
          {isEdit && variable ? (
            <Button disabled={pending || deleteVariable.isPending} onClick={() => setConfirmingDelete(true)} type="button" variant="destructive">Delete variable</Button>
          ) : null}
          <div className="flex flex-col-reverse gap-2 sm:flex-row sm:gap-2">
            <Button onClick={() => onOpenChange(false)} type="button" variant="secondary">Cancel</Button>
            <Button disabled={pending || deleteVariable.isPending || !canSave} onClick={() => void save()} type="button">{pending ? 'Saving…' : 'Save variable'}</Button>
          </div>
        </DialogFooter>
      </DialogContent>

      <AlertDialog open={confirmingDelete} onOpenChange={setConfirmingDelete}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Delete {variable?.name ?? 'variable'}?</AlertDialogTitle>
            <AlertDialogDescription>Existing config values that reference this variable may fail to publish. This cannot be undone.</AlertDialogDescription>
          </AlertDialogHeader>
          {variable ? (
            <div className="grid gap-1.5">
              <label className="text-[12px] text-fg-body" htmlFor={deleteConfirmInputId}>
                Type <span className="font-mono font-semibold text-fg-heading">{variable.name}</span> to confirm.
              </label>
              <Input
                autoComplete="off"
                disabled={deleteVariable.isPending}
                id={deleteConfirmInputId}
                onChange={(event) => setDeleteConfirmText(event.target.value)}
                placeholder={variable.name}
                value={deleteConfirmText}
              />
            </div>
          ) : null}
          <AlertDialogFooter>
            <AlertDialogCancel disabled={deleteVariable.isPending}>Cancel</AlertDialogCancel>
            <AlertDialogAction disabled={!isDeleteConfirmed || deleteVariable.isPending} onClick={(event) => { event.preventDefault(); void confirmDelete(); }}>{deleteVariable.isPending ? 'Deleting…' : 'Delete'}</AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </Dialog>
  );
}

interface VariableRowProps {
  onEdit: () => void;
  ownerText: string;
  variable: Variable;
}

function VariableRow({ onEdit, ownerText, variable }: VariableRowProps) {
  const reveal = useVariableReveal(variable);
  const scopedValue = useMemo(() => ({ scopes: null, value: defaultValue(variable) }), [variable]);
  const isGlobal = !variable.projectId && !variable.groupId;

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
            {variable.isSensitive ? (
              <span className="inline-flex items-center gap-1 rounded-md bg-bg-selected px-2 py-0.5 font-mono text-[11px] uppercase tracking-wide text-fg-on-selected">
                <Lock aria-hidden="true" className="size-3" />
                <span>sensitive</span>
              </span>
            ) : null}
            {isGlobal ? (
              <span className="inline-flex items-center rounded-md bg-badge-success-bg px-2 py-0.5 font-mono text-[11px] uppercase tracking-wide text-badge-success-fg">global</span>
            ) : (
              <code className="inline-flex items-center rounded-md bg-bg-selected px-2 py-0.5 font-mono text-[11.5px] text-fg-on-selected">{ownerText}</code>
            )}
          </div>
          {variable.description ? <p className="mt-2 text-[12.5px] text-fg-body [overflow-wrap:anywhere]">{variable.description}</p> : null}
          <div onClick={(event) => event.stopPropagation()} onKeyDown={(event) => event.stopPropagation()} role="presentation">
            <EntryValue ariaLabel="Copy variable value" emptyMessage="No value." reveal={reveal} scopedValue={scopedValue} />
          </div>
        </div>
        <div className="shrink-0 text-right text-[11.5px] text-fg-caption">
          Updated at: {formatDateTime(variable.updatedAt)}
        </div>
      </div>
    </li>
  );
}

function useVariableReveal(variable: Variable): EntryReveal {
  const [revealed, setRevealed] = useState(false);
  const [decrypted, setDecrypted] = useState<string | null>(null);
  const rawValue = defaultValue(variable);
  const reveal = useMutation({
    mutationFn: () => getVariable(variable.id, { decrypt: true }),
    onError: () => toast.error("Couldn't reveal sensitive value."),
    onSuccess: (data) => {
      const next = data?.values.find((value) => !value.scopes || Object.keys(value.scopes).length === 0)?.value ?? '';
      if (next === SENSITIVE_MASK || (variable.isSensitive && next === rawValue)) {
        toast.error("You don't have permission to reveal sensitive values.");

        return;
      }

      setDecrypted(next);
      setRevealed(true);
    },
  });

  useEffect(() => {
    setRevealed(false);
    setDecrypted(null);
    reveal.reset();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [variable.id, variable.version]);

  const isPending = reveal.isPending;

  return useMemo<EntryReveal>(() => ({
    decryptedValue: () => decrypted ?? undefined,
    isPending: () => isPending,
    isRevealed: () => revealed,
    isSensitive: variable.isSensitive,
    toggleReveal: () => {
      if (revealed) {
        setRevealed(false);

        return;
      }

      if (decrypted !== null) {
        setRevealed(true);

        return;
      }

      reveal.mutate();
    },
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }), [decrypted, isPending, revealed, variable.isSensitive]);
}

function defaultValue(variable?: Variable): string {
  return variable?.values.find((value) => !value.scopes || Object.keys(value.scopes).length === 0)?.value ?? '';
}

function ownerLabel(variable: Variable, projectNames: Map<string, string>, groupNames: Map<string, string>) {
  if (variable.projectId) {
    return `project · ${projectNames.get(variable.projectId) ?? variable.projectId}`;
  }

  if (variable.groupId) {
    return `group · ${groupNames.get(variable.groupId) ?? variable.groupId}`;
  }

  return 'global';
}

function formatDateTime(value: string) {
  return new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value));
}
