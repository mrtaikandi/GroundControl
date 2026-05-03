import { zodResolver } from '@hookform/resolvers/zod';
import { useEffect, useMemo } from 'react';
import { Controller, useForm } from 'react-hook-form';
import { z } from 'zod';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import { useGroups } from '@/queries/useGroups';
import { useUpdateProject } from '@/queries/useProjects';

const projectSchema = z.object({
  description: z.string().max(500, 'Use 500 characters or fewer').optional(),
  groupId: z.string().uuid('Select a group'),
  name: z.string().min(1, 'Project name is required').max(100, 'Use 100 characters or fewer').regex(/^[a-z0-9-]+$/, 'Lowercase, numbers, and hyphens only'),
});

type ProjectFormValues = z.infer<typeof projectSchema>;

interface EditProjectModalProps {
  onOpenChange: (open: boolean) => void;
  open: boolean;
  project: {
    description?: null | string;
    groupId?: null | string;
    id: string;
    name: string;
    templateIds?: readonly string[];
    version: number | string;
  };
}

export function EditProjectModal({ onOpenChange, open, project }: EditProjectModalProps) {
  const groups = useGroups();
  const updateProject = useUpdateProject(project.id);
  const defaults = useMemo<ProjectFormValues>(() => ({
    description: project.description ?? '',
    groupId: project.groupId ?? '',
    name: project.name,
  }), [project.description, project.groupId, project.name]);
  const form = useForm<ProjectFormValues>({
    defaultValues: defaults,
    resolver: zodResolver(projectSchema),
    values: defaults,
  });

  useEffect(() => {
    if (!open) {
      form.reset(defaults);
    }
  }, [defaults, form, open]);

  async function submit(values: ProjectFormValues) {
    await updateProject.mutateAsync({
      body: {
        description: values.description?.trim() ? values.description.trim() : null,
        groupId: values.groupId,
        name: values.name,
        templateIds: project.templateIds ? [...project.templateIds] : null,
      },
      version: project.version.toString(),
    });
    onOpenChange(false);
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Edit project</DialogTitle>
          <DialogDescription>Update the project's name, group, or description.</DialogDescription>
        </DialogHeader>

        <form className="grid gap-4" onSubmit={form.handleSubmit(submit)}>
          <div className="grid gap-1.5">
            <label className="text-[12px] font-medium text-fg-body" htmlFor="edit-project-name">Name</label>
            <Input id="edit-project-name" placeholder="checkout-api" {...form.register('name')} />
            {form.formState.errors.name ? <p className="text-[11.5px] text-badge-critical-fg">{form.formState.errors.name.message}</p> : null}
          </div>

          <div className="grid gap-1.5">
            <label className="text-[12px] font-medium text-fg-body" htmlFor="edit-project-group">Group</label>
            <Controller
              control={form.control}
              name="groupId"
              render={({ field }) => (
                <Select disabled={groups.isLoading} onValueChange={field.onChange} value={field.value}>
                  <SelectTrigger id="edit-project-group">
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
            <label className="text-[12px] font-medium text-fg-body" htmlFor="edit-project-description">Description</label>
            <Textarea id="edit-project-description" placeholder="Configuration for the checkout API service" {...form.register('description')} />
            {form.formState.errors.description ? <p className="text-[11.5px] text-badge-critical-fg">{form.formState.errors.description.message}</p> : null}
          </div>

          <DialogFooter>
            <Button onClick={() => onOpenChange(false)} type="button" variant="ghost">Cancel</Button>
            <Button disabled={updateProject.isPending} type="submit">{updateProject.isPending ? 'Saving…' : 'Save changes'}</Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
