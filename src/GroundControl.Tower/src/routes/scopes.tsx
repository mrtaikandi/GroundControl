import { createFileRoute } from '@tanstack/react-router';
import { useEffect, useState } from 'react';
import { AlertDialog, AlertDialogAction, AlertDialogCancel, AlertDialogContent, AlertDialogDescription, AlertDialogFooter, AlertDialogHeader, AlertDialogTitle } from '@/components/ui/alert-dialog';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Skeleton } from '@/components/ui/skeleton';
import { Textarea } from '@/components/ui/textarea';
import { FilterChip } from '@/components/tower/data/FilterChip';
import { InlineCode } from '@/components/tower/data/InlineCode';
import { useCreateScope, useDeleteScope, useScopes, useUpdateScope, type Scope } from '@/queries/useScopes';

export const Route = createFileRoute('/scopes')({
  component: ScopesRoute,
});

function ScopesRoute() {
  const scopes = useScopes();
  const [creating, setCreating] = useState(false);
  const [editingScope, setEditingScope] = useState<Scope | undefined>();
  const [deletingScope, setDeletingScope] = useState<Scope | undefined>();
  const items = scopes.data?.data ?? [];

  return (
    <div className="grid gap-8">
      <div className="flex items-start justify-between gap-4">
        <div>
          <div className="text-[11px] font-medium uppercase text-fg-caption">GET /api/scopes</div>
          <h1 className="mt-2 text-[34px] font-bold leading-tight text-fg-heading">Scopes</h1>
          <p className="mt-2 text-[14.5px] text-fg-caption">Manage the closed dimensions clients can resolve against</p>
        </div>
        <Button onClick={() => setCreating(true)} type="button">New scope</Button>
      </div>

      {scopes.isLoading ? <Skeleton className="h-80" /> : null}
      {!scopes.isLoading && items.length === 0 ? <div className="rounded-xl border border-stroke-subtle bg-bg-surface p-8 text-center text-fg-caption">No scope dimensions yet.</div> : null}
      {items.length > 0 ? (
        <div className="grid gap-3">
          {items.map((scope) => (
            <div className="grid gap-4 rounded-xl border border-stroke-subtle bg-bg-surface p-5 md:grid-cols-[220px_1fr_auto]" key={scope.id}>
              <div>
                <InlineCode>{scope.dimension}</InlineCode>
                <p className="mt-2 text-[12.5px] text-fg-caption">{scope.description || 'No description provided.'}</p>
              </div>
              <div className="flex flex-wrap gap-2">
                {scope.allowedValues.map((value) => <FilterChip key={value} label={value} onToggle={() => undefined} />)}
              </div>
              <div className="flex items-start justify-end gap-2">
                <Button onClick={() => setEditingScope(scope)} type="button" variant="secondary">Edit</Button>
                <Button onClick={() => setDeletingScope(scope)} type="button" variant="ghost">Delete</Button>
              </div>
            </div>
          ))}
        </div>
      ) : null}

      <ScopeModal mode="create" onOpenChange={setCreating} open={creating} />
      <ScopeModal mode="edit" onOpenChange={(open) => !open && setEditingScope(undefined)} open={Boolean(editingScope)} scope={editingScope} />
      <DeleteScopeDialog onOpenChange={(open) => !open && setDeletingScope(undefined)} open={Boolean(deletingScope)} scope={deletingScope} />
    </div>
  );
}

function ScopeModal({ mode, onOpenChange, open, scope }: { mode: 'create' | 'edit'; onOpenChange: (open: boolean) => void; open: boolean; scope?: Scope }) {
  const createScope = useCreateScope();
  const updateScope = useUpdateScope();
  const [dimension, setDimension] = useState('');
  const [description, setDescription] = useState('');
  const [allowedValues, setAllowedValues] = useState<string[]>([]);
  const [newValue, setNewValue] = useState('');
  const pending = createScope.isPending || updateScope.isPending;

  useEffect(() => {
    if (!open) {
      return;
    }

    setDimension(scope?.dimension ?? '');
    setDescription(scope?.description ?? '');
    setAllowedValues(scope?.allowedValues ?? []);
    setNewValue('');
  }, [open, scope]);

  function addValue() {
    const value = newValue.trim();

    if (!value || allowedValues.includes(value)) {
      return;
    }

    setAllowedValues((current) => [...current, value]);
    setNewValue('');
  }

  async function save() {
    const body = { allowedValues, description: description.trim() || null, dimension: dimension.trim() };

    if (mode === 'create') {
      await createScope.mutateAsync(body);
    } else if (scope) {
      await updateScope.mutateAsync({ body, id: scope.id, version: scope.version.toString() });
    }

    onOpenChange(false);
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="w-[min(calc(100vw-32px),640px)]">
        <DialogHeader>
          <DialogTitle>{mode === 'create' ? 'New scope' : 'Edit scope'}</DialogTitle>
          <DialogDescription>Define a dimension and the allowed values clients can present for it.</DialogDescription>
        </DialogHeader>
        <div className="grid gap-4">
          <div className="grid gap-1.5">
            <label className="text-[12px] font-medium text-fg-body" htmlFor="scope-dimension">Name</label>
            <Input id="scope-dimension" onChange={(event) => setDimension(event.target.value)} placeholder="Environment" value={dimension} />
          </div>
          <div className="grid gap-1.5">
            <label className="text-[12px] font-medium text-fg-body" htmlFor="scope-description">Description</label>
            <Textarea id="scope-description" onChange={(event) => setDescription(event.target.value)} placeholder="Optional context for this dimension" value={description} />
          </div>
          <div className="grid gap-2 rounded-xl border border-stroke-subtle p-4">
            <div className="text-[13px] font-semibold text-fg-heading">Allowed values</div>
            <div className="flex gap-2">
              <Input onChange={(event) => setNewValue(event.target.value)} onKeyDown={(event) => { if (event.key === 'Enter') { event.preventDefault(); addValue(); } }} placeholder="prod" value={newValue} />
              <Button onClick={addValue} type="button" variant="secondary">Add</Button>
            </div>
            <div className="flex flex-wrap gap-2">
              {allowedValues.map((value) => <FilterChip key={value} label={value} onToggle={() => setAllowedValues((current) => current.filter((item) => item !== value))} selected />)}
            </div>
          </div>
        </div>
        <DialogFooter>
          <Button disabled={pending || !dimension.trim() || allowedValues.length === 0} onClick={() => void save()} type="button">{pending ? 'Saving…' : 'Save scope'}</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

function DeleteScopeDialog({ onOpenChange, open, scope }: { onOpenChange: (open: boolean) => void; open: boolean; scope?: Scope }) {
  const deleteScope = useDeleteScope();

  async function confirmDelete() {
    if (!scope) {
      return;
    }

    await deleteScope.mutateAsync({ id: scope.id, version: scope.version.toString() });
    onOpenChange(false);
  }

  return (
    <AlertDialog open={open} onOpenChange={onOpenChange}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>Delete scope dimension</AlertDialogTitle>
          <AlertDialogDescription>Deleting <InlineCode>{scope?.dimension ?? 'scope'}</InlineCode> can orphan scoped values that still reference this dimension.</AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel>Cancel</AlertDialogCancel>
          <AlertDialogAction disabled={deleteScope.isPending} onClick={(event) => { event.preventDefault(); void confirmDelete(); }}>Delete</AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}
