import { Plus, X } from 'lucide-react';
import { useEffect, useMemo, useRef, useState } from 'react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from '@/components/ui/tooltip';
import { InlineCode } from '@/components/tower/data/InlineCode';
import { useAttachProjectTemplate, useDetachProjectTemplate, useProjects } from '@/queries/useProjects';
import { useTemplates, type Template } from '@/queries/useTemplates';

interface TemplateAttachmentsBarProps {
  projectId: string;
}

export function TemplateAttachmentsBar({ projectId }: TemplateAttachmentsBarProps) {
  const projects = useProjects();
  const templates = useTemplates();
  const project = projects.data?.data.find((candidate) => candidate.id === projectId);
  const allTemplates = templates.data?.data ?? [];
  const attachedIds = useMemo(() => new Set(project?.templateIds ?? []), [project?.templateIds]);
  const attached = allTemplates.filter((template) => attachedIds.has(template.id));
  const available = allTemplates.filter((template) => !attachedIds.has(template.id));

  const attach = useAttachProjectTemplate(projectId);
  const detach = useDetachProjectTemplate(projectId);
  const [open, setOpen] = useState(false);
  const [search, setSearch] = useState('');
  const inputRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    if (!open) {
      setSearch('');
    }
  }, [open]);

  const filtered = useMemo(() => {
    const needle = search.trim().toLowerCase();
    if (!needle) {
      return available;
    }

    return available.filter((template) => template.name.toLowerCase().includes(needle));
  }, [search, available]);

  if (!project) {
    return null;
  }

  return (
    <TooltipProvider>
      <div className="flex flex-wrap items-center gap-2 rounded-xl border border-stroke-subtle bg-bg-surface px-4 py-3">
        <span className="text-[11.5px] font-medium uppercase tracking-wide text-fg-caption">Templates</span>
        {attached.length === 0 ? (
          <span className="text-[12.5px] text-fg-caption">No templates attached</span>
        ) : (
          attached.map((template) => (
            <span className="inline-flex items-center gap-1.5 rounded-full border border-stroke-subtle bg-bg-container px-3 py-1 text-[12px]" key={template.id}>
              <InlineCode>{template.name}</InlineCode>
              <Tooltip>
                <TooltipTrigger asChild>
                  <button
                    aria-label={`Detach ${template.name}`}
                    className="grid size-4 place-items-center rounded-full text-fg-icon-subtle transition-colors hover:bg-bg-page hover:text-fg-body"
                    disabled={detach.isPending}
                    onClick={() => detach.mutate({ templateId: template.id, version: project.version.toString() })}
                    type="button"
                  >
                    <X aria-hidden="true" className="size-3" strokeWidth={2} />
                  </button>
                </TooltipTrigger>
                <TooltipContent>Detach template</TooltipContent>
              </Tooltip>
            </span>
          ))
        )}
        <Popover onOpenChange={setOpen} open={open}>
          <PopoverTrigger asChild>
            <Button disabled={available.length === 0 || attach.isPending} size="sm" type="button" variant="ghost">
              <Plus aria-hidden="true" className="size-3.5" strokeWidth={2} />
              Attach template
            </Button>
          </PopoverTrigger>
          <PopoverContent align="start" className="w-72 p-2" onOpenAutoFocus={(event) => { event.preventDefault(); inputRef.current?.focus(); }}>
            <Input className="h-8 text-[12.5px]" onChange={(event) => setSearch(event.target.value)} placeholder="Search templates" ref={inputRef} value={search} />
            <div className="mt-2 max-h-72 overflow-auto">
              {filtered.length === 0 ? (
                <div className="px-3 py-2 text-[12.5px] text-fg-caption">{available.length === 0 ? 'No templates available to attach' : `No templates match "${search}"`}</div>
              ) : (
                filtered.map((template) => (
                  <TemplateRow
                    key={template.id}
                    onAttach={() => {
                      attach.mutate({ templateId: template.id, version: project.version.toString() });
                      setOpen(false);
                    }}
                    template={template}
                  />
                ))
              )}
            </div>
          </PopoverContent>
        </Popover>
      </div>
    </TooltipProvider>
  );
}

function TemplateRow({ onAttach, template }: { onAttach: () => void; template: Template }) {
  return (
    <button
      className="flex w-full flex-col gap-0.5 rounded-lg px-3 py-2 text-left transition-colors hover:bg-bg-container"
      onClick={onAttach}
      type="button"
    >
      <InlineCode>{template.name}</InlineCode>
      {template.description ? <span className="truncate text-[11.5px] text-fg-caption">{template.description}</span> : null}
    </button>
  );
}
