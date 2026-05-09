import { createFileRoute } from '@tanstack/react-router';
import { Braces, FolderTree, List, Plus, X } from 'lucide-react';
import { useEffect, useMemo, useRef, useState } from 'react';
import { ConfigFlatView } from '@/components/tower/config/ConfigFlatView';
import { ConfigJsonView } from '@/components/tower/config/ConfigJsonView';
import { ConfigTreeView } from '@/components/tower/config/ConfigTreeView';
import { SegmentedControl } from '@/components/tower/data/SegmentedControl';
import { Toolbar } from '@/components/tower/data/Toolbar';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { useAttachProjectTemplate, useDetachProjectTemplate, useProjects } from '@/queries/useProjects';
import { useTemplates, type Template } from '@/queries/useTemplates';
import { useTweaksStore } from '@/store/tweaks';

export const Route = createFileRoute('/projects/$projectId/config')({
  component: ConfigRoute,
});

function ConfigRoute() {
  const { projectId } = Route.useParams();
  const projects = useProjects();
  const templates = useTemplates();
  const project = projects.data?.data.find((candidate) => candidate.id === projectId);
  const allTemplates = templates.data?.data ?? [];
  const attachedIds = useMemo(() => new Set(project?.templateIds ?? []), [project?.templateIds]);
  const attached = useMemo(() => allTemplates.filter((template) => attachedIds.has(template.id)), [allTemplates, attachedIds]);
  const available = useMemo(() => allTemplates.filter((template) => !attachedIds.has(template.id)), [allTemplates, attachedIds]);
  const attach = useAttachProjectTemplate(projectId);
  const detach = useDetachProjectTemplate(projectId);
  const configViewMode = useTweaksStore((state) => state.configViewMode);
  const setConfigViewMode = useTweaksStore((state) => state.setConfigViewMode);
  const [open, setOpen] = useState(false);
  const [search, setSearch] = useState('');
  const inputRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    if (!open) {
      setSearch('');
    }
  }, [open]);

  const filteredAvailable = useMemo(() => {
    const needle = search.trim().toLowerCase();
    if (!needle) {
      return available;
    }

    return available.filter((template) => template.name.toLowerCase().includes(needle));
  }, [available, search]);

  const templatesLabel = useMemo(() => {
    if (attached.length === 0) {
      return 'Templates';
    }

    if (attached.length === 1) {
      return attached[0]?.name ?? '1 Template';
    }

    return `${attached.length} Templates`;
  }, [attached]);

  const projectVersion = project?.version.toString();

  return (
    <div className="grid gap-4">
      <Toolbar
        end={
          <SegmentedControl
            onChange={setConfigViewMode}
            options={[
              { icon: List, label: 'Flat', value: 'flat' },
              { icon: FolderTree, label: 'Tree', value: 'tree' },
              { icon: Braces, label: 'JSON', value: 'json' },
            ]}
            value={configViewMode}
          />
        }
        start={
          <Popover onOpenChange={setOpen} open={open}>
            <PopoverTrigger asChild>
              <Button className="max-w-full sm:max-w-80" disabled={!project} size="sm" title={templatesLabel} type="button" variant="outline">
                <Plus aria-hidden="true" className="size-3.5" strokeWidth={2} />
                <span className="truncate">{templatesLabel}</span>
              </Button>
            </PopoverTrigger>
            <PopoverContent
              align="start"
              className="w-[min(26rem,calc(100vw-2rem))] p-3"
              onOpenAutoFocus={(event) => {
                event.preventDefault();
                inputRef.current?.focus();
              }}
            >
              <div className="grid gap-4">
                <div className="grid gap-1">
                  <span className="text-[12.5px] font-semibold text-fg-heading">Manage templates</span>
                  <span className="text-[11.5px] text-fg-caption">Attach templates to inherit configuration or remove ones this project no longer needs.</span>
                </div>

                <div className="grid gap-2">
                  <div className="flex items-center justify-between gap-3">
                    <span className="text-[11.5px] font-medium uppercase tracking-wide text-fg-caption">Attached</span>
                    <span className="text-[11.5px] text-fg-caption">{attached.length}</span>
                  </div>
                  <div className="max-h-44 overflow-auto rounded-xl border border-stroke-subtle bg-bg-page p-1">
                    {attached.length === 0 ? (
                      <div className="px-3 py-2 text-[12.5px] text-fg-caption">No templates attached</div>
                    ) : (
                      attached.map((template) => (
                        <AttachedTemplateRow
                          disabled={!projectVersion || detach.isPending}
                          key={template.id}
                          onDetach={() => detach.mutate({ templateId: template.id, version: projectVersion ?? '' })}
                          template={template}
                        />
                      ))
                    )}
                  </div>
                </div>

                <div className="grid gap-2">
                  <span className="text-[11.5px] font-medium uppercase tracking-wide text-fg-caption">Add template</span>
                  <Input className="h-8 text-[12.5px]" onChange={(event) => setSearch(event.target.value)} placeholder="Search templates" ref={inputRef} value={search} />
                  <div className="max-h-56 overflow-auto rounded-xl border border-stroke-subtle bg-bg-page p-1">
                    {filteredAvailable.length === 0 ? (
                      <div className="px-3 py-2 text-[12.5px] text-fg-caption">{available.length === 0 ? 'No templates available to attach' : `No templates match "${search}"`}</div>
                    ) : (
                      filteredAvailable.map((template) => (
                        <AvailableTemplateRow
                          disabled={!projectVersion || attach.isPending}
                          key={template.id}
                          onAttach={() => {
                            attach.mutate({ templateId: template.id, version: projectVersion ?? '' });
                            setOpen(false);
                          }}
                          template={template}
                        />
                      ))
                    )}
                  </div>
                </div>
              </div>
            </PopoverContent>
          </Popover>
        }
      />

      {configViewMode === 'tree' ? <ConfigTreeView owner={{ kind: 'project', id: projectId }} /> : configViewMode === 'json' ? <ConfigJsonView owner={{ kind: 'project', id: projectId }} /> : <ConfigFlatView owner={{ kind: 'project', id: projectId }} />}
    </div>
  );
}

function AttachedTemplateRow({ disabled, onDetach, template }: { disabled: boolean; onDetach: () => void; template: Template }) {
  return (
    <div className="flex items-start justify-between gap-3 rounded-lg px-3 py-2">
      <div className="min-w-0 flex-1">
        {template.name}
        {template.description ? <div className="truncate text-[11.5px] text-fg-caption">{template.description}</div> : null}
      </div>
      <button
        aria-label={`Remove ${template.name}`}
        className="grid size-7 shrink-0 place-items-center rounded-full text-fg-icon-subtle transition-colors hover:bg-bg-container hover:text-fg-body disabled:pointer-events-none disabled:opacity-45"
        disabled={disabled}
        onClick={onDetach}
        type="button"
      >
        <X aria-hidden="true" className="size-3.5" strokeWidth={2} />
      </button>
    </div>
  );
}

function AvailableTemplateRow({ disabled, onAttach, template }: { disabled: boolean; onAttach: () => void; template: Template }) {
  return (
    <button
      className="flex w-full items-start justify-between gap-3 rounded-lg px-3 py-2 text-left transition-colors hover:bg-bg-container disabled:pointer-events-none disabled:opacity-45"
      disabled={disabled}
      onClick={onAttach}
      title={template.description || undefined}
      type="button"
    >
      <span className="min-w-0 flex-1">
        {template.name}
        {template.description ? <span className="block truncate text-[11.5px] text-fg-caption">{template.description}</span> : null}
      </span>
      <Plus aria-hidden="true" className="mt-0.5 size-3.5 shrink-0 text-fg-icon-subtle" strokeWidth={2} />
    </button>
  );
}
