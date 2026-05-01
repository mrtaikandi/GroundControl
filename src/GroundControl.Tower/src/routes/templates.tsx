import { createFileRoute } from '@tanstack/react-router';
import { useEffect, useMemo, useState } from 'react';
import { AlertDialog, AlertDialogAction, AlertDialogCancel, AlertDialogContent, AlertDialogDescription, AlertDialogFooter, AlertDialogHeader, AlertDialogTitle } from '@/components/ui/alert-dialog';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Skeleton } from '@/components/ui/skeleton';
import { Textarea } from '@/components/ui/textarea';
import { SensitiveValue } from '@/components/tower/code/SensitiveValue';
import { Badge } from '@/components/tower/data/Badge';
import { InlineCode } from '@/components/tower/data/InlineCode';
import { DeleteEntryDialog } from '@/components/tower/config/DeleteEntryDialog';
import { EntryModal } from '@/components/tower/config/EntryModal';
import { useConfigEntries, type ConfigEntry } from '@/queries/useConfigEntries';
import { useProjects } from '@/queries/useProjects';
import { useCreateTemplate, useDeleteTemplate, useTemplates, useUpdateTemplate, type Template } from '@/queries/useTemplates';

export const Route = createFileRoute('/templates')({
  component: TemplatesRoute,
});

function TemplatesRoute() {
  const templates = useTemplates();
  const projects = useProjects();
  const [selectedTemplateId, setSelectedTemplateId] = useState<string | undefined>();
  const [creating, setCreating] = useState(false);
  const [editingTemplate, setEditingTemplate] = useState<Template | undefined>();
  const [deletingTemplate, setDeletingTemplate] = useState<Template | undefined>();
  const items = templates.data?.data ?? [];
  const selectedTemplate = items.find((template) => template.id === selectedTemplateId) ?? items[0];

  useEffect(() => {
    if (!selectedTemplateId && items[0]) {
      setSelectedTemplateId(items[0].id);
    }
  }, [items, selectedTemplateId]);

  return (
    <div className="grid gap-8">
      <div className="flex items-start justify-between gap-4">
        <div>
          <h1 className="text-[34px] font-bold leading-tight text-fg-heading">Templates</h1>
          <p className="mt-2 text-[14.5px] text-fg-caption">Reusable bundles of entries that projects can inherit</p>
        </div>
        <Button onClick={() => setCreating(true)} type="button">New template</Button>
      </div>

      {templates.isLoading ? <Skeleton className="h-96" /> : null}
      {!templates.isLoading && items.length === 0 ? <div className="rounded-xl border border-stroke-subtle bg-bg-surface p-8 text-center text-fg-caption">No templates yet.</div> : null}
      {items.length > 0 ? (
        <div className="grid gap-5 xl:grid-cols-[1fr_460px]">
          <div className="grid gap-3">
            {items.map((template) => (
              <TemplateRow inheritedBy={projects.data?.data.filter((project) => project.templateIds.includes(template.id)).map((project) => project.name) ?? []} key={template.id} onDelete={() => setDeletingTemplate(template)} onEdit={() => setEditingTemplate(template)} onSelect={() => setSelectedTemplateId(template.id)} selected={template.id === selectedTemplate?.id} template={template} />
            ))}
          </div>
          {selectedTemplate ? <TemplateDetail template={selectedTemplate} /> : null}
        </div>
      ) : null}

      <TemplateModal mode="create" onOpenChange={setCreating} open={creating} />
      <TemplateModal mode="edit" onOpenChange={(open) => !open && setEditingTemplate(undefined)} open={Boolean(editingTemplate)} template={editingTemplate} />
      <DeleteTemplateDialog onOpenChange={(open) => !open && setDeletingTemplate(undefined)} open={Boolean(deletingTemplate)} template={deletingTemplate} />
    </div>
  );
}

function TemplateRow({ inheritedBy, onDelete, onEdit, onSelect, selected, template }: { inheritedBy: string[]; onDelete: () => void; onEdit: () => void; onSelect: () => void; selected: boolean; template: Template }) {
  const entries = useConfigEntries(template.id, 0);

  return (
    <div className={`grid cursor-pointer gap-4 rounded-xl border p-5 transition-colors md:grid-cols-[1fr_auto] ${selected ? 'border-stroke-field-focus bg-bg-selected' : 'border-stroke-subtle bg-bg-surface hover:bg-bg-container'}`} onClick={onSelect}>
      <div className="grid gap-3">
        <div className="flex flex-wrap items-center gap-2">
          <InlineCode>{template.name}</InlineCode>
          <Badge variant="neutral">{entries.data?.data.length ?? 0} entries</Badge>
        </div>
        <p className="text-[12.5px] text-fg-caption">{template.description || 'No description provided.'}</p>
        <div className="flex flex-wrap gap-2">
          {inheritedBy.length === 0 ? <Badge variant="neutral">not inherited</Badge> : inheritedBy.map((project) => <Badge key={project} variant="info">{project}</Badge>)}
        </div>
      </div>
      <div className="flex items-start justify-end gap-2" onClick={(event) => event.stopPropagation()}>
        <Button onClick={onEdit} type="button" variant="secondary">Edit</Button>
        <Button onClick={onDelete} type="button" variant="ghost">Delete</Button>
      </div>
    </div>
  );
}

function TemplateDetail({ template }: { template: Template }) {
  const entries = useConfigEntries(template.id, 0);
  const [creatingEntry, setCreatingEntry] = useState(false);
  const [editingEntry, setEditingEntry] = useState<ConfigEntry | undefined>();
  const [deletingEntry, setDeletingEntry] = useState<ConfigEntry | undefined>();

  return (
    <div className="rounded-xl border border-stroke-subtle bg-bg-container p-5">
      <div className="flex items-start justify-between gap-3">
        <div>
          <div className="text-[11px] font-medium uppercase text-fg-caption">Template detail</div>
          <h2 className="mt-1 font-mono text-[19px] font-semibold text-fg-heading">{template.name}</h2>
        </div>
        <Button onClick={() => setCreatingEntry(true)} type="button" variant="secondary">Add entry</Button>
      </div>

      <div className="mt-5 grid gap-2">
        {entries.isLoading ? <Skeleton className="h-64" /> : null}
        {(entries.data?.data ?? []).map((entry) => (
          <div className="grid gap-2 rounded-lg bg-bg-surface p-3" key={entry.id}>
            <div className="flex items-start justify-between gap-3">
              <div className="grid gap-2">
                <InlineCode>{entry.key}</InlineCode>
                <div className="flex flex-wrap gap-2">
                  <Badge variant="neutral">{entry.valueType}</Badge>
                  <Badge variant="info">{scopeCount(entry)} scopes</Badge>
                  {entry.isSensitive ? <Badge variant="critical">sensitive</Badge> : null}
                </div>
              </div>
              <div className="flex gap-1">
                <Button onClick={() => setEditingEntry(entry)} size="sm" type="button" variant="ghost">Edit</Button>
                <Button onClick={() => setDeletingEntry(entry)} size="sm" type="button" variant="ghost">Delete</Button>
              </div>
            </div>
            <SensitiveValue isSensitive={entry.isSensitive} value={defaultValue(entry)} />
          </div>
        ))}
        {!entries.isLoading && (entries.data?.data ?? []).length === 0 ? <div className="rounded-lg bg-bg-surface p-6 text-center text-[12.5px] text-fg-caption">No entries in this template.</div> : null}
      </div>

      <EntryModal mode="create" onOpenChange={setCreatingEntry} open={creatingEntry} ownerId={template.id} ownerType={0} />
      <EntryModal entry={editingEntry} mode="edit" onOpenChange={(open) => !open && setEditingEntry(undefined)} open={Boolean(editingEntry)} ownerId={template.id} ownerType={0} />
      <DeleteEntryDialog entry={deletingEntry} onOpenChange={(open) => !open && setDeletingEntry(undefined)} open={Boolean(deletingEntry)} ownerId={template.id} ownerType={0} />
    </div>
  );
}

function TemplateModal({ mode, onOpenChange, open, template }: { mode: 'create' | 'edit'; onOpenChange: (open: boolean) => void; open: boolean; template?: Template }) {
  const createTemplate = useCreateTemplate();
  const updateTemplate = useUpdateTemplate();
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const pending = createTemplate.isPending || updateTemplate.isPending;

  useEffect(() => {
    if (!open) {
      return;
    }

    setName(template?.name ?? '');
    setDescription(template?.description ?? '');
  }, [open, template]);

  async function save() {
    const body = { description: description.trim() || null, groupId: template?.groupId ?? null, name: name.trim() };

    if (mode === 'create') {
      await createTemplate.mutateAsync(body);
    } else if (template) {
      await updateTemplate.mutateAsync({ body, id: template.id, version: template.version.toString() });
    }

    onOpenChange(false);
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="w-[min(calc(100vw-32px),620px)]">
        <DialogHeader>
          <DialogTitle>{mode === 'create' ? 'New template' : 'Edit template'}</DialogTitle>
          <DialogDescription>Templates collect entries that can be inherited by projects.</DialogDescription>
        </DialogHeader>
        <div className="grid gap-4">
          <div className="grid gap-1.5">
            <label className="text-[12px] font-medium text-fg-body" htmlFor="template-name">Name</label>
            <Input id="template-name" onChange={(event) => setName(event.target.value)} placeholder="checkout-defaults" value={name} />
          </div>
          <div className="grid gap-1.5">
            <label className="text-[12px] font-medium text-fg-body" htmlFor="template-description">Description</label>
            <Textarea id="template-description" onChange={(event) => setDescription(event.target.value)} placeholder="Optional context" value={description} />
          </div>
        </div>
        <DialogFooter>
          <Button disabled={pending || !name.trim()} onClick={() => void save()} type="button">{pending ? 'Saving…' : 'Save template'}</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

function DeleteTemplateDialog({ onOpenChange, open, template }: { onOpenChange: (open: boolean) => void; open: boolean; template?: Template }) {
  const deleteTemplate = useDeleteTemplate();

  async function confirmDelete() {
    if (!template) {
      return;
    }

    await deleteTemplate.mutateAsync({ id: template.id, version: template.version.toString() });
    onOpenChange(false);
  }

  return (
    <AlertDialog open={open} onOpenChange={onOpenChange}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>Delete template</AlertDialogTitle>
          <AlertDialogDescription>Delete <InlineCode>{template?.name ?? 'template'}</InlineCode>. Projects that inherit it will no longer receive its entries.</AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel>Cancel</AlertDialogCancel>
          <AlertDialogAction disabled={deleteTemplate.isPending} onClick={(event) => { event.preventDefault(); void confirmDelete(); }}>Delete</AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}

function defaultValue(entry: ConfigEntry): string {
  return entry.values.find((value) => !value.scopes || Object.keys(value.scopes).length === 0)?.value ?? '';
}

function scopeCount(entry: ConfigEntry): number {
  return entry.values.filter((value) => value.scopes && Object.keys(value.scopes).length > 0).length;
}
