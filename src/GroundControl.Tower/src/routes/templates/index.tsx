import { createFileRoute, Link } from '@tanstack/react-router';
import { Plus } from 'lucide-react';
import { useEffect, useMemo, useState } from 'react';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Skeleton } from '@/components/ui/skeleton';
import { Textarea } from '@/components/ui/textarea';
import { SearchFilterPopover } from '@/components/tower/data/SearchFilterPopover';
import { PageContent } from '@/components/tower/shell/PageContent';
import { PageHeader } from '@/components/tower/shell/PageHeader';
import { formatRelativeTime } from '@/lib/relative-time';
import { useConfigEntries } from '@/queries/useConfigEntries';
import { useProjects } from '@/queries/useProjects';
import { useCreateTemplate, useTemplates, type Template } from '@/queries/useTemplates';

const MaxInheritedDisplay = 3;

export const Route = createFileRoute('/templates/')({
  component: TemplatesIndexRoute,
});

function TemplatesIndexRoute() {
  const templates = useTemplates();
  const projects = useProjects();
  const [creating, setCreating] = useState(false);
  const [search, setSearch] = useState<string | undefined>(undefined);
  const items = templates.data?.data ?? [];
  const projectsByTemplate = useMemo(() => {
    const map = new Map<string, string[]>();
    for (const project of projects.data?.data ?? []) {
      for (const templateId of project.templateIds ?? []) {
        const list = map.get(templateId) ?? [];
        list.push(project.name);
        map.set(templateId, list);
      }
    }

    return map;
  }, [projects.data?.data]);

  const filtered = useMemo(() => {
    const needle = search?.trim().toLowerCase();
    if (!needle) {
      return items;
    }

    return items.filter((template) => {
      const inherited = (projectsByTemplate.get(template.id) ?? []).join(' ').toLowerCase();
      return template.name.toLowerCase().includes(needle) || (template.description ?? '').toLowerCase().includes(needle) || inherited.includes(needle);
    });
  }, [items, projectsByTemplate, search]);

  return (
    <>
      <PageHeader
        actions={(
          <div className="flex items-center gap-2">
            <SearchFilterPopover
              appliedSearch={search}
              ariaLabel="Filter templates"
              onApply={setSearch}
              placeholder="Template name, description, or inheriting project"
            />
            <Button onClick={() => setCreating(true)} type="button">
              <Plus aria-hidden="true" className="size-3.5" strokeWidth={2} />
              <span>New template</span>
            </Button>
          </div>
        )}
        description="Share common settings across multiple projects."
        title="Templates"
      />

      <PageContent>
        <div className="grid gap-8 pt-8">
          {templates.isLoading ? <Skeleton className="h-80" /> : null}
          {!templates.isLoading && items.length === 0 ? <div className="rounded-xl border border-stroke-subtle bg-bg-surface p-8 text-center text-fg-caption">No templates yet.</div> : null}
          {!templates.isLoading && items.length > 0 && filtered.length === 0 ? <div className="rounded-xl border border-stroke-subtle bg-bg-surface p-8 text-center text-fg-caption">No templates match the current filter.</div> : null}
          {filtered.length > 0 ? (
            <div className="overflow-hidden rounded-xl border border-stroke-subtle bg-bg-surface">
              <ul className="grid divide-y divide-stroke-subtle">
                {filtered.map((template) => (
                  <li key={template.id}>
                    <TemplateRow inheritedBy={projectsByTemplate.get(template.id) ?? []} template={template} />
                  </li>
                ))}
              </ul>
            </div>
          ) : null}
        </div>
      </PageContent>

      <NewTemplateModal onOpenChange={setCreating} open={creating} />
    </>
  );
}

interface TemplateRowProps {
  inheritedBy: string[];
  template: Template;
}

function TemplateRow({ inheritedBy, template }: TemplateRowProps) {
  const entries = useConfigEntries(template.id, 0);
  const entryCount = entries.data?.data.length ?? 0;
  const description = template.description?.trim();

  return (
    <Link
      className="grid cursor-pointer grid-cols-[minmax(0,1fr)_auto] items-start gap-4 px-[18px] py-[14px] transition-colors hover:bg-bg-container focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-stroke-field-focus"
      params={{ templateId: template.id }}
      to="/templates/$templateId"
    >
      <div className="min-w-0">
        <div className="flex flex-wrap items-center gap-x-3 gap-y-1">
          <h2 className="font-mono text-[13.5px] font-semibold text-fg-heading [overflow-wrap:anywhere]">{template.name}</h2>
          <span className="inline-flex items-center rounded-md bg-bg-selected px-2 py-0.5 font-mono text-[11px] uppercase tracking-wide text-fg-on-selected">
            {entryCount} {entryCount === 1 ? 'entry' : 'entries'}
          </span>
        </div>
        {description ? <p className="mt-2 text-[12.5px] text-fg-body [overflow-wrap:anywhere]">{description}</p> : null}
        <div className="mt-2 flex flex-wrap items-center gap-1.5">
          {inheritedBy.length === 0 ? (
            <span className="inline-flex items-center rounded-md border border-dashed border-stroke-subtle px-2 py-0.5 font-mono text-[11px] uppercase tracking-wide text-fg-caption">not inherited</span>
          ) : (
            <>
              {inheritedBy.slice(0, MaxInheritedDisplay).map((projectName) => (
                <span className="inline-flex items-center rounded-md bg-badge-info-bg px-2 py-0.5 font-mono text-[11.5px] text-badge-info-fg" key={projectName}>{projectName}</span>
              ))}
              {inheritedBy.length > MaxInheritedDisplay ? (
                <span className="inline-flex items-center rounded-md bg-bg-selected px-2 py-0.5 font-mono text-[11.5px] text-fg-on-selected" title={inheritedBy.slice(MaxInheritedDisplay).join(', ')}>
                  +{inheritedBy.length - MaxInheritedDisplay} more
                </span>
              ) : null}
            </>
          )}
        </div>
      </div>
      <div className="shrink-0 text-right text-[11.5px] text-fg-caption">
        Updated {formatRelativeTime(template.updatedAt)}
      </div>
    </Link>
  );
}

function NewTemplateModal({ onOpenChange, open }: { onOpenChange: (open: boolean) => void; open: boolean }) {
  const createTemplate = useCreateTemplate();
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');

  useEffect(() => {
    if (!open) {
      return;
    }

    setName('');
    setDescription('');
  }, [open]);

  async function save() {
    await createTemplate.mutateAsync({ description: description.trim() || null, groupId: null, name: name.trim() });
    onOpenChange(false);
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="w-[min(calc(100vw-32px),620px)]">
        <DialogHeader>
          <DialogTitle>New Template</DialogTitle>
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
          <Button onClick={() => onOpenChange(false)} type="button" variant="secondary">Cancel</Button>
          <Button disabled={createTemplate.isPending || !name.trim()} onClick={() => void save()} type="button">{createTemplate.isPending ? 'Saving…' : 'Save template'}</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
