import { zodResolver } from '@hookform/resolvers/zod';
import { useEffect, useRef, useState } from 'react';
import { useForm } from 'react-hook-form';
import { z } from 'zod';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle, DialogTrigger } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { SegmentedControl } from '@/components/tower/data/SegmentedControl';
import { useCreateClient } from '@/queries/useClients';
import { useScopes } from '@/queries/useScopes';
import { PATRevealModal } from './PATRevealModal';

const newClientSchema = z.object({
  name: z.string().min(1, 'Client name is required').max(100, 'Use 100 characters or fewer'),
  scopes: z.record(z.string(), z.string()),
});

type NewClientFormValues = z.infer<typeof newClientSchema>;

export function NewClientModal({ projectId }: { projectId: string }) {
  const scopes = useScopes();
  const rawTokenRef = useRef<string | null>(null);
  const [open, setOpen] = useState(false);
  const [revealOpen, setRevealOpen] = useState(false);
  const createClient = useCreateClient(projectId, (rawToken) => {
    rawTokenRef.current = rawToken;
    setRevealOpen(true);
  });
  const form = useForm<NewClientFormValues>({ defaultValues: { name: '', scopes: {} }, resolver: zodResolver(newClientSchema) });
  const selectedScopes = form.watch('scopes');
  const scopeDefinitions = scopes.data?.data.filter((scope) => scope.allowedValues.length > 0) ?? [];

  useEffect(() => {
    for (const scope of scopeDefinitions) {
      if (!form.getValues(`scopes.${scope.dimension}`)) {
        form.setValue(`scopes.${scope.dimension}`, scope.allowedValues[0]!);
      }
    }
  }, [form, scopeDefinitions]);

  async function submit(values: NewClientFormValues) {
    await createClient.mutateAsync({ name: values.name, scopes: values.scopes });
    form.reset({ name: '', scopes: {} });
    setOpen(false);
  }

  function confirmReveal() {
    rawTokenRef.current = null;
    setRevealOpen(false);
  }

  return (
    <>
      <Dialog open={open} onOpenChange={setOpen}>
        <DialogTrigger asChild><Button type="button">New client</Button></DialogTrigger>
        <DialogContent className="max-h-[min(760px,calc(100vh-32px))] overflow-y-auto w-[min(calc(100vw-32px),680px)]">
          <DialogHeader>
            <DialogTitle>New client credential</DialogTitle>
            <DialogDescription>Choose the fixed scope context this credential will use when fetching config.</DialogDescription>
          </DialogHeader>
          <form className="grid gap-4" onSubmit={form.handleSubmit(submit)}>
            <div className="grid gap-1.5">
              <label className="text-[12px] font-medium text-fg-body" htmlFor="client-name">Name</label>
              <Input id="client-name" placeholder="checkout-api-prod" {...form.register('name')} />
              {form.formState.errors.name ? <p className="text-[11.5px] text-badge-critical-fg">{form.formState.errors.name.message}</p> : null}
            </div>
            <div className="grid gap-4 rounded-xl border border-stroke-subtle p-4">
              {scopeDefinitions.length === 0 ? <div className="text-[12px] text-fg-caption">No scope dimensions are configured.</div> : null}
              {scopeDefinitions.map((scope) => (
                <div className="grid gap-1.5 min-w-0" key={scope.id}>
                  <div className="font-mono text-[11px] uppercase text-fg-caption [overflow-wrap:anywhere]">{scope.dimension}</div>
                  <div className="overflow-x-auto pb-1">
                    <SegmentedControl onChange={(value) => form.setValue(`scopes.${scope.dimension}`, value)} options={scope.allowedValues.map((value) => ({ label: value, value }))} size="sm" value={selectedScopes[scope.dimension] ?? scope.allowedValues[0]!} />
                  </div>
                </div>
              ))}
            </div>
            <DialogFooter>
              <Button disabled={createClient.isPending} type="submit">{createClient.isPending ? 'Creating…' : 'Create client'}</Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>
      <PATRevealModal onConfirm={confirmReveal} open={revealOpen} rawToken={rawTokenRef.current ?? ''} />
    </>
  );
}