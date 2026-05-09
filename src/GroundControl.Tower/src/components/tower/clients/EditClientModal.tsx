import { zodResolver } from '@hookform/resolvers/zod';
import { useEffect, useId, useState } from 'react';
import { useForm } from 'react-hook-form';
import { z } from 'zod';
import { AlertDialog, AlertDialogAction, AlertDialogCancel, AlertDialogContent, AlertDialogDescription, AlertDialogFooter, AlertDialogHeader, AlertDialogTitle } from '@/components/ui/alert-dialog';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { SegmentedControl } from '@/components/tower/data/SegmentedControl';
import { useProjects } from '@/queries/useProjects';
import { useScopes } from '@/queries/useScopes';
import { useRevokeClient, useUpdateClient, type Client } from '@/queries/useClients';

const editClientSchema = z.object({
  name: z.string().min(1, 'Client name is required').max(100, 'Use 100 characters or fewer'),
  scopes: z.record(z.string(), z.string()),
});

type EditClientFormValues = z.infer<typeof editClientSchema>;

interface EditClientModalProps {
  client: Client | null;
  onOpenChange: (open: boolean) => void;
  open: boolean;
  projectId: string;
}

export function EditClientModal({ client, onOpenChange, open, projectId }: EditClientModalProps) {
  const projects = useProjects();
  const scopes = useScopes();
  const updateClient = useUpdateClient();
  const revokeClient = useRevokeClient(projectId);
  const [confirmingDelete, setConfirmingDelete] = useState(false);
  const [deleteConfirmText, setDeleteConfirmText] = useState('');
  const deleteConfirmInputId = useId();
  const isDeleteConfirmed = !!client && deleteConfirmText === client.name;
  const form = useForm<EditClientFormValues>({
    defaultValues: { name: client?.name ?? '', scopes: client?.scopes ?? {} },
    resolver: zodResolver(editClientSchema),
  });
  const selectedScopes = form.watch('scopes');
  const projectName = projects.data?.data.find((project) => project.id === projectId)?.name ?? projectId;
  const scopeDefinitions = scopes.data?.data.filter((scope) => scope.allowedValues.length > 0) ?? [];

  useEffect(() => {
    if (open && client) {
      form.reset({ name: client.name, scopes: { ...client.scopes } });
    }
  }, [client, form, open]);

  useEffect(() => {
    if (!confirmingDelete) {
      setDeleteConfirmText('');
    }
  }, [confirmingDelete]);

  useEffect(() => {
    if (!open || !client) {
      return;
    }

    for (const scope of scopeDefinitions) {
      if (!form.getValues(`scopes.${scope.dimension}`)) {
        form.setValue(`scopes.${scope.dimension}`, scope.allowedValues[0]!);
      }
    }
  }, [client, form, open, scopeDefinitions]);

  async function submit(values: EditClientFormValues) {
    if (!client) {
      return;
    }

    await updateClient.mutateAsync({
      body: {
        expiresAt: client.expiresAt ?? null,
        isActive: client.isActive,
        name: values.name,
        scopes: values.scopes,
      },
      id: client.id,
      projectId,
      version: client.version.toString(),
    });
    onOpenChange(false);
  }

  async function confirmDelete() {
    if (!client) {
      return;
    }

    await revokeClient.mutateAsync({ id: client.id, version: client.version.toString() });
    setConfirmingDelete(false);
    onOpenChange(false);
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-h-[min(760px,calc(100vh-32px))] w-[min(calc(100vw-32px),680px)] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>Edit Client Credential</DialogTitle>
          <DialogDescription>Update the name and scope context. The owning project cannot be changed.</DialogDescription>
        </DialogHeader>
        <form className="grid gap-4" onSubmit={form.handleSubmit(submit)}>
          <div className="grid gap-1.5">
            <label className="text-[12px] font-medium text-fg-body" htmlFor="edit-client-name">Name</label>
            <Input id="edit-client-name" placeholder="checkout-api-prod" {...form.register('name')} />
            {form.formState.errors.name ? <p className="text-[11.5px] text-badge-critical-fg">{form.formState.errors.name.message}</p> : null}
          </div>

          <div className="grid gap-1.5">
            <span className="text-[12px] font-medium text-fg-body">Project</span>
            <div className="rounded-md border border-stroke-subtle bg-bg-container px-3 py-2 text-[13px] text-fg-body [overflow-wrap:anywhere]">{projectName}</div>
          </div>

          <div className="grid gap-4 rounded-xl border border-stroke-subtle p-4">
            {scopeDefinitions.length === 0 ? <div className="text-[12px] text-fg-caption">No scope dimensions are configured.</div> : null}
            {scopeDefinitions.map((scope) => (
              <div className="grid min-w-0 gap-1.5" key={scope.id}>
                <div className="font-mono text-[11px] uppercase text-fg-caption [overflow-wrap:anywhere]">{scope.dimension}</div>
                <div className="overflow-x-auto pb-1">
                  <SegmentedControl onChange={(value) => form.setValue(`scopes.${scope.dimension}`, value)} options={scope.allowedValues.map((value) => ({ label: value, value }))} size="sm" value={selectedScopes[scope.dimension] ?? scope.allowedValues[0]!} />
                </div>
              </div>
            ))}
          </div>

          <DialogFooter className="sm:justify-between">
            <Button disabled={!client || updateClient.isPending || revokeClient.isPending} onClick={() => setConfirmingDelete(true)} type="button" variant="destructive">Delete client</Button>
            <div className="flex flex-col-reverse gap-2 sm:flex-row sm:gap-2">
              <Button onClick={() => onOpenChange(false)} type="button" variant="secondary">Cancel</Button>
              <Button disabled={!client || updateClient.isPending || revokeClient.isPending} type="submit">{updateClient.isPending ? 'Saving…' : 'Save changes'}</Button>
            </div>
          </DialogFooter>
        </form>
      </DialogContent>

      <AlertDialog open={confirmingDelete} onOpenChange={setConfirmingDelete}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Delete {client?.name ?? 'client'}?</AlertDialogTitle>
            <AlertDialogDescription>This permanently removes the credential. Anyone using it will immediately lose access. This cannot be undone.</AlertDialogDescription>
          </AlertDialogHeader>
          {client ? (
            <div className="grid gap-1.5">
              <label className="text-[12px] text-fg-body" htmlFor={deleteConfirmInputId}>
                Type <span className="font-mono font-semibold text-fg-heading">{client.name}</span> to confirm.
              </label>
              <Input
                autoComplete="off"
                disabled={revokeClient.isPending}
                id={deleteConfirmInputId}
                onChange={(event) => setDeleteConfirmText(event.target.value)}
                placeholder={client.name}
                value={deleteConfirmText}
              />
            </div>
          ) : null}
          <AlertDialogFooter>
            <AlertDialogCancel disabled={revokeClient.isPending}>Cancel</AlertDialogCancel>
            <AlertDialogAction disabled={!isDeleteConfirmed || revokeClient.isPending} onClick={(event) => { event.preventDefault(); void confirmDelete(); }}>{revokeClient.isPending ? 'Deleting…' : 'Delete'}</AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </Dialog>
  );
}
