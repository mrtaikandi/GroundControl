import { createColumnHelper, flexRender, getCoreRowModel, useReactTable } from '@tanstack/react-table';
import { createFileRoute } from '@tanstack/react-router';
import { useEffect, useMemo, useState } from 'react';
import { AlertDialog, AlertDialogAction, AlertDialogCancel, AlertDialogContent, AlertDialogDescription, AlertDialogFooter, AlertDialogHeader, AlertDialogTitle } from '@/components/ui/alert-dialog';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Skeleton } from '@/components/ui/skeleton';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Textarea } from '@/components/ui/textarea';
import { SensitiveValue } from '@/components/tower/code/SensitiveValue';
import { Badge } from '@/components/tower/data/Badge';
import { InlineCode } from '@/components/tower/data/InlineCode';
import { useGroups } from '@/queries/useGroups';
import { useProjects } from '@/queries/useProjects';
import { useCreateVariable, useDeleteVariable, useUpdateVariable, useVariables, type Variable } from '@/queries/useVariables';

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
    columnHelper.accessor('name', { cell: (info) => <InlineCode>{info.getValue()}</InlineCode>, header: 'Name' }),
    columnHelper.display({ cell: (info) => <SensitiveValue isSensitive={info.row.original.isSensitive} value={defaultValue(info.row.original)} />, header: 'Value', id: 'value' }),
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
          <div className="text-[11px] font-medium uppercase text-fg-caption">GET /api/variables</div>
          <h1 className="mt-2 text-[34px] font-bold leading-tight text-fg-heading">Variables</h1>
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
            <TableBody>
              {table.getRowModel().rows.map((row) => (
                <TableRow key={row.id}>{row.getVisibleCells().map((cell) => <TableCell key={cell.id}>{flexRender(cell.column.columnDef.cell, cell.getContext())}</TableCell>)}</TableRow>
              ))}
              {table.getRowModel().rows.length === 0 ? <TableRow><TableCell className="py-10 text-center text-fg-caption" colSpan={columns.length}>No variables found.</TableCell></TableRow> : null}
            </TableBody>
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
  const createVariable = useCreateVariable();
  const updateVariable = useUpdateVariable();
  const [name, setName] = useState('');
  const [value, setValue] = useState('');
  const [description, setDescription] = useState('');
  const [isSensitive, setIsSensitive] = useState(false);
  const pending = createVariable.isPending || updateVariable.isPending;

  useEffect(() => {
    if (!open) {
      return;
    }

    setName(variable?.name ?? '');
    setValue(defaultValue(variable));
    setDescription(variable?.description ?? '');
    setIsSensitive(variable?.isSensitive ?? false);
  }, [open, variable]);

  async function save() {
    const values = [{ scopes: {}, value }];

    if (mode === 'create') {
      await createVariable.mutateAsync({ description: description.trim() || null, isSensitive, name: name.trim(), scope: 0, values });
    } else if (variable) {
      await updateVariable.mutateAsync({ body: { description: description.trim() || null, isSensitive, values }, id: variable.id, version: variable.version.toString() });
    }

    onOpenChange(false);
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="w-[min(calc(100vw-32px),620px)]">
        <DialogHeader>
          <DialogTitle>{mode === 'create' ? 'New variable' : 'Edit variable'}</DialogTitle>
          <DialogDescription>Variables are resolved before snapshots are published.</DialogDescription>
        </DialogHeader>
        <div className="grid gap-4">
          <div className="grid gap-1.5">
            <label className="text-[12px] font-medium text-fg-body" htmlFor="variable-name">Name</label>
            <Input disabled={mode === 'edit'} id="variable-name" onChange={(event) => setName(event.target.value)} placeholder="connectionString" value={name} />
          </div>
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
          <Button disabled={pending || !name.trim()} onClick={() => void save()} type="button">{pending ? 'Saving…' : 'Save variable'}</Button>
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
