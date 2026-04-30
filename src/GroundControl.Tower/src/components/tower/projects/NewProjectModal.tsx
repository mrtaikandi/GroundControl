import { zodResolver } from '@hookform/resolvers/zod';
import { Controller, useForm } from 'react-hook-form';
import { z } from 'zod';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle, DialogTrigger } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import { InlineCode } from '@/components/tower/data/InlineCode';
import { useGroups } from '@/queries/useGroups';
import { useCreateProject } from '@/queries/useProjects';
import { useState } from 'react';

const projectSchema = z.object({
  description: z.string().max(500, 'Use 500 characters or fewer').optional(),
  groupId: z.string().uuid('Select a group'),
  name: z.string().min(1, 'Project name is required').max(100, 'Use 100 characters or fewer').regex(/^[a-z0-9-]+$/, 'Lowercase, numbers, and hyphens only'),
});

type ProjectFormValues = z.infer<typeof projectSchema>;

export function NewProjectModal() {
  const [open, setOpen] = useState(false);
  const groups = useGroups();
  const createProject = useCreateProject();
  const form = useForm<ProjectFormValues>({
    defaultValues: {
      description: '',
      groupId: '',
      name: '',
    },
    resolver: zodResolver(projectSchema),
  });
  const name = form.watch('name');
  const slugPreview = name.trim().toLowerCase().replace(/\s+/g, '-');

  async function submit(values: ProjectFormValues) {
    await createProject.mutateAsync({
      description: values.description?.trim() ? values.description.trim() : null,
      groupId: values.groupId,
      name: values.name,
      templateIds: [],
    });
    form.reset();
    setOpen(false);
  }

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <Button type="button">New project</Button>
      </DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>New project</DialogTitle>
          <DialogDescription>Create a configuration boundary owned by a group.</DialogDescription>
        </DialogHeader>

        <form className="grid gap-4" onSubmit={form.handleSubmit(submit)}>
          <div className="grid gap-1.5">
            <label className="text-[12px] font-medium text-fg-body" htmlFor="project-name">Name</label>
            <Input id="project-name" placeholder="checkout-api" {...form.register('name')} />
            <div className="text-[11.5px] text-fg-caption">Slug preview: <InlineCode>{slugPreview || 'project-name'}</InlineCode></div>
            {form.formState.errors.name ? <p className="text-[11.5px] text-badge-critical-fg">{form.formState.errors.name.message}</p> : null}
          </div>

          <div className="grid gap-1.5">
            <label className="text-[12px] font-medium text-fg-body" htmlFor="project-group">Group</label>
            <Controller
              control={form.control}
              name="groupId"
              render={({ field }) => (
                <Select disabled={groups.isLoading} onValueChange={field.onChange} value={field.value}>
                  <SelectTrigger id="project-group">
                    <SelectValue placeholder={groups.isLoading ? 'Loading groups…' : 'Select a group'} />
                  </SelectTrigger>
                  <SelectContent>
                    {(groups.data?.data ?? []).map((group) => <SelectItem key={group.id} value={group.id}>{group.name}</SelectItem>)}
                  </SelectContent>
                </Select>
              )}
            />
            {form.formState.errors.groupId ? <p className="text-[11.5px] text-badge-critical-fg">{form.formState.errors.groupId.message}</p> : null}
          </div>

          <div className="grid gap-1.5">
            <label className="text-[12px] font-medium text-fg-body" htmlFor="project-description">Description</label>
            <Textarea id="project-description" placeholder="Configuration for the checkout API service" {...form.register('description')} />
            {form.formState.errors.description ? <p className="text-[11.5px] text-badge-critical-fg">{form.formState.errors.description.message}</p> : null}
          </div>

          <DialogFooter>
            <Button disabled={createProject.isPending} type="submit">{createProject.isPending ? 'Creating…' : 'Create project'}</Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}