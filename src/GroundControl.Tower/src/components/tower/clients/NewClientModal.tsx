import { zodResolver } from '@hookform/resolvers/zod';
import { Plus } from 'lucide-react';
import { useEffect, useRef, useState } from 'react';
import { Controller, useForm } from 'react-hook-form';
import { z } from 'zod';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle, DialogTrigger } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { SegmentedControl } from '@/components/tower/data/SegmentedControl';
import { useCreateClient } from '@/queries/useClients';
import { useProjects } from '@/queries/useProjects';
import { useScopes } from '@/queries/useScopes';
import { PATRevealModal } from './PATRevealModal';

const newClientSchema = z.object({
  name: z.string().min(1, 'Client name is required').max(100, 'Use 100 characters or fewer'),
  projectId: z.string().uuid('Select a project'),
  scopes: z.record(z.string(), z.string()),
});

type NewClientFormValues = z.infer<typeof newClientSchema>;

interface NewClientModalProps {
  projectId?: string;
}

export function NewClientModal({ projectId }: NewClientModalProps) {
  const projects = useProjects();
  const scopes = useScopes();
  const rawTokenRef = useRef<string | null>(null);
  const [open, setOpen] = useState(false);
  const [revealOpen, setRevealOpen] = useState(false);
  const createClient = useCreateClient((rawToken) => {
    rawTokenRef.current = rawToken;
    setRevealOpen(true);
  });
  const form = useForm<NewClientFormValues>({
    defaultValues: { name: '', projectId: projectId ?? '', scopes: {} },
    resolver: zodResolver(newClientSchema),
  });
  const selectedScopes = form.watch('scopes');
  const scopeDefinitions = scopes.data?.data.filter((scope) => scope.allowedValues.length > 0) ?? [];

  useEffect(() => {
    for (const scope of scopeDefinitions) {
      if (!form.getValues(`scopes.${scope.dimension}`)) {
        form.setValue(`scopes.${scope.dimension}`, scope.allowedValues[0]!);
      }
    }
  }, [form, scopeDefinitions]);

  useEffect(() => {
    if (projectId) {
      form.setValue('projectId', projectId);
    }
  }, [form, projectId]);

  async function submit(values: NewClientFormValues) {
    await createClient.mutateAsync({
      body: { name: values.name, scopes: values.scopes },
      projectId: values.projectId,
    });
    form.reset({ name: '', projectId: projectId ?? '', scopes: {} });
    setOpen(false);
  }

  function confirmReveal() {
    rawTokenRef.current = null;
    setRevealOpen(false);
  }

  const projectOptions = projects.data?.data ?? [];

  return (
    <>
      <Dialog open={open} onOpenChange={setOpen}>
        <DialogTrigger asChild>
          <Button type="button">
            <Plus aria-hidden="true" className="size-3.5" strokeWidth={2} />
            <span>New client</span>
          </Button>
        </DialogTrigger>
        <DialogContent className="max-h-[min(760px,calc(100vh-32px))] w-[min(calc(100vw-32px),680px)] overflow-y-auto">
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

            {projectId ? null : (
              <div className="grid gap-1.5">
                <label className="text-[12px] font-medium text-fg-body" htmlFor="client-project">Project</label>
                <Controller
                  control={form.control}
                  name="projectId"
                  render={({ field }) => (
                    <Select disabled={projects.isLoading} onValueChange={field.onChange} value={field.value}>
                      <SelectTrigger id="client-project">
                        <SelectValue placeholder={projects.isLoading ? 'Loading projects…' : 'Select a project'} />
                      </SelectTrigger>
                      <SelectContent>
                        {projectOptions.map((project) => <SelectItem key={project.id} value={project.id}>{project.name}</SelectItem>)}
                      </SelectContent>
                    </Select>
                  )}
                />
                {form.formState.errors.projectId ? <p className="text-[11.5px] text-badge-critical-fg">{form.formState.errors.projectId.message}</p> : null}
              </div>
            )}

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
