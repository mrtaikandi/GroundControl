import { createColumnHelper, flexRender, getCoreRowModel, useReactTable } from '@tanstack/react-table';
import { createFileRoute } from '@tanstack/react-router';
import { useEffect, useMemo, useState } from 'react';
import { AlertDialog, AlertDialogAction, AlertDialogCancel, AlertDialogContent, AlertDialogDescription, AlertDialogFooter, AlertDialogHeader, AlertDialogTitle } from '@/components/ui/alert-dialog';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Skeleton } from '@/components/ui/skeleton';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Textarea } from '@/components/ui/textarea';
import { SensitiveValue } from '@/components/tower/code/SensitiveValue';
import { Badge } from '@/components/tower/data/Badge';
import { InlineCode } from '@/components/tower/data/InlineCode';
import { SegmentedControl } from '@/components/tower/data/SegmentedControl';
import { cn } from '@/lib/utils';
import { useGroups } from '@/queries/useGroups';
import { useProjects } from '@/queries/useProjects';
import { useCreateVariable, useDeleteVariable, useUpdateVariable, useVariables, type Variable } from '@/queries/useVariables';

type VariableTier = 'group' | 'project';

const columnHelper = createColumnHelper<Variable>();

export const Route = createFileRoute('/variables')({
  component: VariablesRoute,
});

function VariablesRoute() {
  const variables = useVariables();
  const projects = useProjects();
  const groups = useGroups();
  const [creating, setCreating] = useState(false);
  const [editingVariable, setEditingVariable] = useState<Variable | undefined>();
  const [deletingVariable, setDeletingVariable] = useState<Variable | undefined>();
  const projectNames = useMemo(() => new Map((projects.data?.data ?? []).map((project) => [project.id, project.name])), [projects.data?.data]);
  const groupNames = useMemo(() => new Map((groups.data?.data ?? []).map((group) => [group.id, group.name])), [groups.data?.data]);
  const data = variables.data?.data ?? [];
  const columns = useMemo(() => [
    columnHelper.accessor('name', { cell: (info) => <InlineCode className="bg-transparent px-0">{info.getValue()}</InlineCode>, header: 'Name' }),
    columnHelper.display({ cell: (info) => <SensitiveValue className="bg-transparent px-0" isSensitive={info.row.original.isSensitive} value={defaultValue(info.row.original)} />, header: 'Value', id: 'value' }),
    columnHelper.accessor('isSensitive', { cell: (info) => <Badge variant={info.getValue() ? 'critical' : 'neutral'}>{info.getValue() ? 'sensitive' : 'plain'}</Badge>, header: 'Mode' }),
    columnHelper.display({ cell: (info) => <Badge variant="info">{ownerLabel(info.row.original, projectNames, groupNames)}</Badge>, header: 'Owner', id: 'owner' }),
    columnHelper.accessor('updatedAt', { cell: (info) => formatDate(info.getValue()), header: 'Updated' }),
    columnHelper.display({ cell: (info) => <div className="flex justify-end gap-1"><Button onClick={() => setEditingVariable(info.row.original)} size="sm" type="button" variant="ghost">Edit</Button><Button onClick={() => setDeletingVariable(info.row.original)} size="sm" type="button" variant="ghost">Delete</Button></div>, header: '', id: 'actions' }),
  ], [groupNames, projectNames]);
  const table = useReactTable({ columns, data, getCoreRowModel: getCoreRowModel() });

  return (
    <div className="grid gap-8">
      <div className="flex items-start justify-between gap-4">
        <div>
          <h1 className="text-[34px] font-bold leading-tight text-fg-heading">Variables</h1>
          <p className="mt-2 text-[14.5px] text-fg-caption">Reusable values for interpolation during snapshot publishing</p>
        </div>
        <Button onClick={() => setCreating(true)} type="button">New variable</Button>
      </div>

      {variables.isLoading ? <Skeleton className="h-96" /> : (
        <div className="overflow-hidden rounded-xl border border-stroke-subtle bg-bg-surface">
          <Table>
            <TableHeader>
              {table.getHeaderGroups().map((headerGroup) => (
                <TableRow key={headerGroup.id}>{headerGroup.headers.map((header) => <TableHead key={header.id}>{header.isPlaceholder ? null : flexRender(header.column.columnDef.header, header.getContext())}</TableHead>)}</TableRow>
              ))}
            </TableHeader>
            {table.getRowModel().rows.map((row, index, all) => {
              const description = row.original.description?.trim();
              const cells = row.getVisibleCells();
              const mainCells = cells.slice(0, -1);
              const actionsCell = cells[cells.length - 1];
              const isLast = index === all.length - 1;

              return (
                <tbody className={cn('group', isLast && '[&>tr:last-child]:border-b-0')} key={row.id}>
                  <TableRow className={cn('hover:bg-transparent group-hover:bg-muted/60 [&>td]:pt-3', description ? 'border-b-0 [&>td]:pb-2' : '[&>td]:pb-4')}>
                    {mainCells.map((cell) => <TableCell key={cell.id}>{flexRender(cell.column.columnDef.cell, cell.getContext())}</TableCell>)}
                    {actionsCell ? (
                      <TableCell className="align-middle" rowSpan={description ? 2 : 1}>
                        {flexRender(actionsCell.column.columnDef.cell, actionsCell.getContext())}
                      </TableCell>
                    ) : null}
                  </TableRow>
                  {description ? (
                    <TableRow className="hover:bg-transparent group-hover:bg-muted/60">
                      <TableCell className="px-3 pb-4 pt-0 text-[12.5px] leading-snug text-fg-caption" colSpan={mainCells.length}>{description}</TableCell>
                    </TableRow>
                  ) : null}
                </tbody>
              );
            })}
            {table.getRowModel().rows.length === 0 ? (
              <TableBody>
                <TableRow><TableCell className="py-10 text-center text-fg-caption" colSpan={columns.length}>No variables found.</TableCell></TableRow>
              </TableBody>
            ) : null}
          </Table>
        </div>
      )}

      <VariableModal mode="create" onOpenChange={setCreating} open={creating} />
      <VariableModal mode="edit" onOpenChange={(open) => !open && setEditingVariable(undefined)} open={Boolean(editingVariable)} variable={editingVariable} />
      <DeleteVariableDialog onOpenChange={(open) => !open && setDeletingVariable(undefined)} open={Boolean(deletingVariable)} variable={deletingVariable} />
    </div>
  );
}

function VariableModal({ mode, onOpenChange, open, variable }: { mode: 'create' | 'edit'; onOpenChange: (open: boolean) => void; open: boolean; variable?: Variable }) {
  const projects = useProjects();
  const groups = useGroups();
  const createVariable = useCreateVariable();
  const updateVariable = useUpdateVariable();
  const [name, setName] = useState('');
  const [value, setValue] = useState('');
  const [description, setDescription] = useState('');
  const [isSensitive, setIsSensitive] = useState(false);
  const [tier, setTier] = useState<VariableTier>('group');
  const [groupId, setGroupId] = useState<string | null>(null);
  const [projectId, setProjectId] = useState<string | null>(null);
  const pending = createVariable.isPending || updateVariable.isPending;
  const isEdit = mode === 'edit';

  useEffect(() => {
    if (!open) {
      return;
    }

    setName(variable?.name ?? '');
    setValue(defaultValue(variable));
    setDescription(variable?.description ?? '');
    setIsSensitive(variable?.isSensitive ?? false);

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
  }, [open, variable]);

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

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="w-[min(calc(100vw-32px),620px)]">
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
                <div className="rounded-lg border border-stroke-subtle bg-bg-container px-3 py-2 text-[12.5px] text-fg-caption">
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
                <div className="rounded-lg border border-stroke-subtle bg-bg-container px-3 py-2 text-[12.5px] text-fg-caption">
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
            <Input id="variable-value" onChange={(event) => setValue(event.target.value)} type={isSensitive ? 'password' : 'text'} value={value} />
          </div>
          <label className="flex h-9 items-center gap-2 rounded-lg border border-stroke-subtle bg-bg-container px-3 text-[13px] text-fg-body">
            <input checked={isSensitive} className="size-4 accent-[var(--tower-stroke-field-focus)]" onChange={(event) => setIsSensitive(event.target.checked)} type="checkbox" />
            Sensitive value
          </label>
          <div className="grid gap-1.5">
            <label className="text-[12px] font-medium text-fg-body" htmlFor="variable-description">Description</label>
            <Textarea id="variable-description" onChange={(event) => setDescription(event.target.value)} placeholder="Optional context" value={description} />
          </div>
        </div>
        <DialogFooter>
          <Button disabled={pending || !canSave} onClick={() => void save()} type="button">{pending ? 'Saving…' : 'Save variable'}</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

function DeleteVariableDialog({ onOpenChange, open, variable }: { onOpenChange: (open: boolean) => void; open: boolean; variable?: Variable }) {
  const deleteVariable = useDeleteVariable();

  async function confirmDelete() {
    if (!variable) {
      return;
    }

    await deleteVariable.mutateAsync({ id: variable.id, version: variable.version.toString() });
    onOpenChange(false);
  }

  return (
    <AlertDialog open={open} onOpenChange={onOpenChange}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>Delete variable</AlertDialogTitle>
          <AlertDialogDescription>Delete <InlineCode>{variable?.name ?? 'variable'}</InlineCode>. Existing config values that reference it may fail to publish.</AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel>Cancel</AlertDialogCancel>
          <AlertDialogAction disabled={deleteVariable.isPending} onClick={(event) => { event.preventDefault(); void confirmDelete(); }}>Delete</AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
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

function formatDate(value: string) {
  return new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value));
}
