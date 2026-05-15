import { createFileRoute } from '@tanstack/react-router';
import { Plus, X } from 'lucide-react';
import { useEffect, useId, useMemo, useState } from 'react';
import { AlertDialog, AlertDialogAction, AlertDialogCancel, AlertDialogContent, AlertDialogDescription, AlertDialogFooter, AlertDialogHeader, AlertDialogTitle } from '@/components/ui/alert-dialog';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Skeleton } from '@/components/ui/skeleton';
import { Textarea } from '@/components/ui/textarea';
import { SearchFilterPopover } from '@/components/tower/data/SearchFilterPopover';
import { PageHeader } from '@/components/tower/shell/PageHeader';
import { PageContent } from '@/components/tower/shell/PageContent';
import { useCreateScope, useDeleteScope, useScopes, useUpdateScope, type Scope } from '@/queries/useScopes';

export const Route = createFileRoute('/scopes')({
  component: ScopesRoute,
});

function ScopesRoute() {
  const scopes = useScopes();
  const [creating, setCreating] = useState(false);
  const [editingScope, setEditingScope] = useState<Scope | undefined>();
  const [search, setSearch] = useState<string | undefined>(undefined);
  const items = scopes.data?.data ?? [];

  const filtered = useMemo(() => {
    const needle = search?.trim().toLowerCase();
    if (!needle) {
      return items;
    }

    return items.filter((scope) => scope.dimension.toLowerCase().includes(needle) || (scope.description ?? '').toLowerCase().includes(needle));
  }, [items, search]);

  return (
    <>
      <PageHeader
        actions={(
          <div className="flex items-center gap-2">
            <SearchFilterPopover
              appliedSearch={search}
              ariaLabel="Filter scopes"
              onApply={setSearch}
              placeholder="Scope name or description"
            />
            <Button onClick={() => setCreating(true)} type="button">
              <Plus aria-hidden="true" className="size-3.5" strokeWidth={2} />
              <span>New scope</span>
            </Button>
          </div>
        )}
        description="Decide which settings each app sees based on where it's running."
        title="Scopes"
      />

      <PageContent>
        <div className="grid gap-8 pt-8">
          {scopes.isLoading ? <Skeleton className="h-80" /> : null}
          {!scopes.isLoading && items.length === 0 ? <div className="rounded-xl border border-stroke-subtle bg-bg-surface p-8 text-center text-fg-caption">No scope dimensions yet.</div> : null}
          {!scopes.isLoading && items.length > 0 && filtered.length === 0 ? <div className="rounded-xl border border-stroke-subtle bg-bg-surface p-8 text-center text-fg-caption">No scopes match the current filter.</div> : null}
          {filtered.length > 0 ? (
            <div className="overflow-hidden rounded-xl border border-stroke-subtle bg-bg-surface">
              <ul className="grid divide-y divide-stroke-subtle">
                {filtered.map((scope) => (
                  <li key={scope.id}>
                    <div
                      className="cursor-pointer px-[18px] py-[14px] transition-colors hover:bg-bg-container focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-stroke-field-focus"
                      onClick={() => setEditingScope(scope)}
                      onKeyDown={(event) => { if (event.key === 'Enter' || event.key === ' ') { event.preventDefault(); setEditingScope(scope); } }}
                      role="button"
                      tabIndex={0}
                    >
                      <h2 className="font-mono text-[13.5px] font-semibold text-fg-heading [overflow-wrap:anywhere]">{scope.dimension}</h2>
                      {scope.allowedValues.length > 0 ? (
                        <div className="mt-2 flex flex-wrap gap-1.5">
                          {scope.allowedValues.map((value) => (
                            <code className="inline-flex items-center rounded-md bg-bg-selected px-2 py-0.5 font-mono text-[11.5px] text-fg-on-selected" key={value}>{value}</code>
                          ))}
                        </div>
                      ) : null}
                      {scope.description ? <p className="mt-2 text-[12.5px] text-fg-body [overflow-wrap:anywhere]">{scope.description}</p> : null}
                    </div>
                  </li>
                ))}
              </ul>
            </div>
          ) : null}
        </div>
      </PageContent>

      <ScopeModal mode="create" onOpenChange={setCreating} open={creating} />
      <ScopeModal mode="edit" onOpenChange={(open) => !open && setEditingScope(undefined)} open={Boolean(editingScope)} scope={editingScope} />
    </>
  );
}

function ScopeModal({ mode, onOpenChange, open, scope }: { mode: 'create' | 'edit'; onOpenChange: (open: boolean) => void; open: boolean; scope?: Scope }) {
  const createScope = useCreateScope();
  const updateScope = useUpdateScope();
  const deleteScope = useDeleteScope();
  const [dimension, setDimension] = useState('');
  const [description, setDescription] = useState('');
  const [allowedValues, setAllowedValues] = useState<string[]>([]);
  const [newValue, setNewValue] = useState('');
  const [confirmingDelete, setConfirmingDelete] = useState(false);
  const [deleteConfirmText, setDeleteConfirmText] = useState('');
  const deleteConfirmInputId = useId();
  const pending = createScope.isPending || updateScope.isPending;
  const isDeleteConfirmed = !!scope && deleteConfirmText === scope.dimension;

  useEffect(() => {
    if (!open) {
      return;
    }

    setDimension(scope?.dimension ?? '');
    setDescription(scope?.description ?? '');
    setAllowedValues(scope?.allowedValues ?? []);
    setNewValue('');
  }, [open, scope]);

  useEffect(() => {
    if (!confirmingDelete) {
      setDeleteConfirmText('');
    }
  }, [confirmingDelete]);

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

  async function confirmDelete() {
    if (!scope) {
      return;
    }

    await deleteScope.mutateAsync({ id: scope.id, version: scope.version.toString() });
    setConfirmingDelete(false);
    onOpenChange(false);
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="w-[min(calc(100vw-32px),640px)]">
        <DialogHeader>
          <DialogTitle>{mode === 'create' ? 'New Scope' : 'Edit Scope'}</DialogTitle>
          <DialogDescription>Define a dimension and the allowed values clients can present for it.</DialogDescription>
        </DialogHeader>
        <div className="grid gap-4">
          <div className="grid gap-1.5">
            <label className="text-[12px] font-medium text-fg-body" htmlFor="scope-dimension">Name</label>
            <Input id="scope-dimension" onChange={(event) => setDimension(event.target.value)} placeholder="Environment" value={dimension} />
          </div>
          <div className="grid gap-1.5">
            <label className="text-[12px] font-medium text-fg-body" htmlFor="scope-description">Description</label>
            <Textarea id="scope-description" onChange={(event) => setDescription(event.target.value)} value={description} />
          </div>
          <div className="grid gap-2 rounded-xl border border-stroke-subtle p-4">
            <div className="text-[13px] font-semibold text-fg-heading">Allowed Values</div>
            <div className="flex flex-col gap-2 sm:flex-row">
              <Input onChange={(event) => setNewValue(event.target.value)} onKeyDown={(event) => { if (event.key === 'Enter') { event.preventDefault(); addValue(); } }} placeholder="prod" value={newValue} />
              <Button onClick={addValue} type="button" variant="secondary">Add</Button>
            </div>
            <div className="flex flex-wrap gap-2">
              {allowedValues.map((value) => (
                <button
                  aria-label={`Remove ${value}`}
                  className="group inline-flex items-center gap-1.5 rounded-md bg-bg-selected px-2 py-0.5 font-mono text-[11.5px] text-fg-on-selected transition-opacity hover:opacity-80"
                  key={value}
                  onClick={() => setAllowedValues((current) => current.filter((item) => item !== value))}
                  type="button"
                >
                  <span>{value}</span>
                  <X aria-hidden="true" className="size-3 opacity-70 transition-opacity group-hover:opacity-100" />
                </button>
              ))}
            </div>
            {allowedValues.length === 0 ? <div className="text-[12.5px] text-fg-caption">Add at least one allowed value before saving this scope.</div> : null}
          </div>
        </div>
        <DialogFooter className={mode === 'edit' ? 'sm:justify-between' : undefined}>
          {mode === 'edit' && scope ? (
            <Button disabled={pending || deleteScope.isPending} onClick={() => setConfirmingDelete(true)} type="button" variant="destructive">Delete scope</Button>
          ) : null}
          <div className="flex flex-col-reverse gap-2 sm:flex-row sm:gap-2">
            <Button onClick={() => onOpenChange(false)} type="button" variant="secondary">Cancel</Button>
            <Button disabled={pending || deleteScope.isPending || !dimension.trim() || allowedValues.length === 0} onClick={() => void save()} type="button">{pending ? 'Saving…' : 'Save scope'}</Button>
          </div>
        </DialogFooter>
      </DialogContent>

      <AlertDialog open={confirmingDelete} onOpenChange={setConfirmingDelete}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Delete {scope?.dimension ?? 'scope'}?</AlertDialogTitle>
            <AlertDialogDescription>Deleting this dimension can orphan scoped values that still reference it. This cannot be undone.</AlertDialogDescription>
          </AlertDialogHeader>
          {scope ? (
            <div className="grid gap-1.5">
              <label className="text-[12px] text-fg-body" htmlFor={deleteConfirmInputId}>
                Type <span className="font-mono font-semibold text-fg-heading">{scope.dimension}</span> to confirm.
              </label>
              <Input
                autoComplete="off"
                disabled={deleteScope.isPending}
                id={deleteConfirmInputId}
                onChange={(event) => setDeleteConfirmText(event.target.value)}
                placeholder={scope.dimension}
                value={deleteConfirmText}
              />
            </div>
          ) : null}
          <AlertDialogFooter>
            <AlertDialogCancel disabled={deleteScope.isPending}>Cancel</AlertDialogCancel>
            <AlertDialogAction disabled={!isDeleteConfirmed || deleteScope.isPending} onClick={(event) => { event.preventDefault(); void confirmDelete(); }}>{deleteScope.isPending ? 'Deleting…' : 'Delete'}</AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </Dialog>
  );
}
