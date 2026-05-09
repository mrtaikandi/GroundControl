import { createFileRoute, Link, useNavigate } from '@tanstack/react-router';
import { Braces, FolderTree, Layers3, List, Pencil, Trash2 } from 'lucide-react';
import { useEffect, useId, useMemo, useState } from 'react';
import { AlertDialog, AlertDialogAction, AlertDialogCancel, AlertDialogContent, AlertDialogDescription, AlertDialogFooter, AlertDialogHeader, AlertDialogTitle } from '@/components/ui/alert-dialog';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Skeleton } from '@/components/ui/skeleton';
import { Textarea } from '@/components/ui/textarea';
import { ConfigFlatView } from '@/components/tower/config/ConfigFlatView';
import { ConfigJsonView } from '@/components/tower/config/ConfigJsonView';
import { ConfigTreeView } from '@/components/tower/config/ConfigTreeView';
import { SegmentedControl } from '@/components/tower/data/SegmentedControl';
import { Toolbar } from '@/components/tower/data/Toolbar';
import { PageContent } from '@/components/tower/shell/PageContent';
import { PageHeader } from '@/components/tower/shell/PageHeader';
import { useProjects } from '@/queries/useProjects';
import { useDeleteTemplate, useTemplates, useUpdateTemplate, type Template } from '@/queries/useTemplates';
import { useTweaksStore } from '@/store/tweaks';

export const Route = createFileRoute('/templates/$templateId')({
  component: TemplateDetailRoute,
});

function TemplateDetailRoute() {
  const { templateId } = Route.useParams();
  const navigate = useNavigate();
  const templates = useTemplates();
  const projects = useProjects();
  const template = templates.data?.data.find((candidate) => candidate.id === templateId);
  const inheritedBy = useMemo(() => (projects.data?.data ?? []).filter((project) => project.templateIds?.includes(templateId)), [projects.data?.data, templateId]);
  const [editing, setEditing] = useState(false);
  const [confirmingDelete, setConfirmingDelete] = useState(false);
  const configViewMode = useTweaksStore((state) => state.configViewMode);
  const setConfigViewMode = useTweaksStore((state) => state.setConfigViewMode);

  if (templates.isLoading) {
    return (
      <div className="grid gap-5">
        <Skeleton className="h-32" />
        <Skeleton className="h-96" />
      </div>
    );
  }

  if (!template) {
    return (
      <PageContent>
        <div className="grid gap-5 pt-8">
          <div className="rounded-xl border border-stroke-subtle bg-bg-surface p-8 text-center text-fg-caption">
            Template not found.
          </div>
          <div className="flex justify-center">
            <Button onClick={() => void navigate({ to: '/templates' })} type="button" variant="secondary">Back to templates</Button>
          </div>
        </div>
      </PageContent>
    );
  }

  const owner = { id: template.id, kind: 'template' as const };

  return (
    <>
      <PageHeader
        actions={(
          <div className="flex shrink-0 items-center gap-2">
            <Button aria-label="Edit template" className="size-8 rounded-full p-0" onClick={() => setEditing(true)} size="sm" type="button" variant="secondary">
              <Pencil aria-hidden="true" className="size-3.5" strokeWidth={1.8} />
            </Button>
            <Button aria-label="Delete template" className="size-8 rounded-full p-0" onClick={() => setConfirmingDelete(true)} size="sm" type="button" variant="destructive">
              <Trash2 aria-hidden="true" className="size-3.5" strokeWidth={1.8} />
            </Button>
          </div>
        )}
        align="start"
        description={template.description?.trim() || 'No description provided.'}
        descriptionClassName="max-w-3xl text-[13.5px] text-fg-body"
        eyebrow={(
          <div className="flex items-center gap-2 font-mono text-[11.5px] uppercase tracking-wide">
            <Link className="transition-colors hover:text-fg-body" to="/templates">Templates</Link>
            <span aria-hidden="true">/</span>
            <span className="text-fg-body">{template.name}</span>
          </div>
        )}
        eyebrowClassName="normal-case"
        title={(
          <span className="flex flex-wrap items-center gap-3">
            <span>{template.name}</span>
          </span>
        )}
        titleClassName="font-mono text-[28px]"
      />

      <PageContent>
        <div className="grid gap-5 pt-8">
          <div className="grid gap-4">
            <Toolbar
              end={(
                <SegmentedControl
                  onChange={setConfigViewMode}
                  options={[
                    { icon: List, label: 'Flat', value: 'flat' },
                    { icon: FolderTree, label: 'Tree', value: 'tree' },
                    { icon: Braces, label: 'JSON', value: 'json' },
                  ]}
                  value={configViewMode}
                />
              )}
            />

            {configViewMode === 'tree' ? <ConfigTreeView owner={owner} /> : configViewMode === 'json' ? <ConfigJsonView owner={owner} /> : <ConfigFlatView owner={owner} />}
          </div>

          <InheritancePanel inheritedBy={inheritedBy.map((project) => ({ id: project.id, name: project.name }))} />
        </div>
      </PageContent>

      <EditTemplateModal onOpenChange={setEditing} open={editing} template={template} />
      <DeleteTemplateDialog onOpenChange={setConfirmingDelete} onSuccess={() => void navigate({ to: '/templates' })} open={confirmingDelete} template={template} />
    </>
  );
}

function InheritancePanel({ inheritedBy }: { inheritedBy: { id: string; name: string }[] }) {
  return (
    <section className="rounded-xl border border-stroke-subtle bg-bg-surface p-5">
      <div className="flex items-baseline justify-between gap-3">
        <h2 className="text-[15px] font-semibold text-fg-heading">Inherited by</h2>
        <span className="text-[12px] text-fg-caption">{inheritedBy.length === 0 ? 'no projects' : `${inheritedBy.length} ${inheritedBy.length === 1 ? 'project' : 'projects'}`}</span>
      </div>

      <div className="mt-3 flex flex-wrap items-center gap-2">
        {inheritedBy.length === 0 ? (
          <span className="inline-flex items-center rounded-md border border-dashed border-stroke-subtle px-2 py-0.5 font-mono text-[11px] uppercase tracking-wide text-fg-caption">not inherited</span>
        ) : (
          inheritedBy.map((project) => (
            <Link
              className="inline-flex items-center gap-1.5 rounded-md bg-badge-info-bg px-2 py-1 font-mono text-[11.5px] text-badge-info-fg transition-opacity hover:opacity-80 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-stroke-field-focus"
              key={project.id}
              params={{ projectId: project.id }}
              to="/projects/$projectId/config"
            >
              <Layers3 aria-hidden="true" className="size-3" />
              <span>{project.name}</span>
            </Link>
          ))
        )}
      </div>
    </section>
  );
}

function EditTemplateModal({ onOpenChange, open, template }: { onOpenChange: (open: boolean) => void; open: boolean; template: Template }) {
  const updateTemplate = useUpdateTemplate();
  const [name, setName] = useState(template.name);
  const [description, setDescription] = useState(template.description ?? '');

  useEffect(() => {
    if (!open) {
      return;
    }

    setName(template.name);
    setDescription(template.description ?? '');
  }, [open, template]);

  async function save() {
    await updateTemplate.mutateAsync({
      body: { description: description.trim() || null, groupId: template.groupId ?? null, name: name.trim() },
      id: template.id,
      version: template.version.toString(),
    });
    onOpenChange(false);
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="w-[min(calc(100vw-32px),620px)]">
        <DialogHeader>
          <DialogTitle>Edit template</DialogTitle>
          <DialogDescription>Update the template name and description.</DialogDescription>
        </DialogHeader>
        <div className="grid gap-4">
          <div className="grid gap-1.5">
            <label className="text-[12px] font-medium text-fg-body" htmlFor="edit-template-name">Name</label>
            <Input id="edit-template-name" onChange={(event) => setName(event.target.value)} placeholder="checkout-defaults" value={name} />
          </div>
          <div className="grid gap-1.5">
            <label className="text-[12px] font-medium text-fg-body" htmlFor="edit-template-description">Description</label>
            <Textarea id="edit-template-description" onChange={(event) => setDescription(event.target.value)} placeholder="Optional context" value={description} />
          </div>
        </div>
        <DialogFooter>
          <Button onClick={() => onOpenChange(false)} type="button" variant="secondary">Cancel</Button>
          <Button disabled={updateTemplate.isPending || !name.trim()} onClick={() => void save()} type="button">{updateTemplate.isPending ? 'Saving…' : 'Save changes'}</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

function DeleteTemplateDialog({ onOpenChange, onSuccess, open, template }: { onOpenChange: (open: boolean) => void; onSuccess: () => void; open: boolean; template: Template }) {
  const deleteTemplate = useDeleteTemplate();
  const [confirmText, setConfirmText] = useState('');
  const confirmInputId = useId();
  const isConfirmed = confirmText === template.name;

  useEffect(() => {
    if (!open) {
      setConfirmText('');
    }
  }, [open]);

  async function confirm() {
    await deleteTemplate.mutateAsync({ id: template.id, version: template.version.toString() });
    onOpenChange(false);
    onSuccess();
  }

  return (
    <AlertDialog open={open} onOpenChange={onOpenChange}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>Delete {template.name}?</AlertDialogTitle>
          <AlertDialogDescription>Projects that inherit this template will no longer receive its entries. This cannot be undone.</AlertDialogDescription>
        </AlertDialogHeader>
        <div className="grid gap-1.5">
          <label className="text-[12px] text-fg-body" htmlFor={confirmInputId}>
            Type <span className="font-mono font-semibold text-fg-heading">{template.name}</span> to confirm.
          </label>
          <Input
            autoComplete="off"
            disabled={deleteTemplate.isPending}
            id={confirmInputId}
            onChange={(event) => setConfirmText(event.target.value)}
            placeholder={template.name}
            value={confirmText}
          />
        </div>
        <AlertDialogFooter>
          <AlertDialogCancel disabled={deleteTemplate.isPending}>Cancel</AlertDialogCancel>
          <AlertDialogAction disabled={!isConfirmed || deleteTemplate.isPending} onClick={(event) => { event.preventDefault(); void confirm(); }}>{deleteTemplate.isPending ? 'Deleting…' : 'Delete'}</AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}
