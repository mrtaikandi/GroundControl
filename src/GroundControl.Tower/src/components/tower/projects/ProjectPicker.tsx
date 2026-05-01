import { Check, ChevronDown } from 'lucide-react';
import { useEffect, useMemo, useRef, useState } from 'react';
import { Input } from '@/components/ui/input';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { cn } from '@/lib/utils';
import type { useProjects } from '@/queries/useProjects';

type ProjectSummary = NonNullable<ReturnType<typeof useProjects>['data']>['data'][number];

interface ProjectPickerProps {
  onChange: (projectId: string) => void;
  projects: ProjectSummary[];
  selectedId: string;
}

export function ProjectPicker({ onChange, projects, selectedId }: ProjectPickerProps) {
  const [open, setOpen] = useState(false);
  const [search, setSearch] = useState('');
  const inputRef = useRef<HTMLInputElement>(null);
  const selected = projects.find((project) => project.id === selectedId);

  useEffect(() => {
    if (!open) {
      setSearch('');
    }
  }, [open]);

  const filtered = useMemo(() => {
    const needle = search.trim().toLowerCase();
    if (!needle) {
      return projects;
    }

    return projects.filter((project) => project.name.toLowerCase().includes(needle) || project.id.toLowerCase().includes(needle));
  }, [projects, search]);

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger asChild>
        <button
          className="inline-flex h-8 items-center gap-2 rounded-lg border border-stroke-field-initial bg-bg-surface px-3 text-[13px] font-medium text-fg-body outline-none transition-colors hover:bg-bg-container focus-visible:ring-2 focus-visible:ring-stroke-field-focus"
          type="button"
        >
          <span className="font-mono">{selected?.name ?? 'Select project'}</span>
          <ChevronDown aria-hidden="true" className="size-3.5 text-fg-icon-subtle" strokeWidth={1.8} />
        </button>
      </PopoverTrigger>
      <PopoverContent align="start" className="w-72 p-2" onOpenAutoFocus={(event) => { event.preventDefault(); inputRef.current?.focus(); }}>
        <Input className="h-8 text-[12.5px]" onChange={(event) => setSearch(event.target.value)} placeholder="Search projects" ref={inputRef} value={search} />
        <div className="mt-2 max-h-72 overflow-auto">
          {filtered.length === 0 ? (
            <div className="px-3 py-2 text-[12.5px] text-fg-caption">No projects match "{search}"</div>
          ) : (
            filtered.map((project) => (
              <button
                className={cn(
                  'flex w-full items-center justify-between gap-2 rounded-lg px-3 py-2 text-left text-[13px] transition-colors',
                  project.id === selectedId ? 'bg-bg-selected text-fg-heading' : 'text-fg-body hover:bg-bg-container',
                )}
                key={project.id}
                onClick={() => { onChange(project.id); setOpen(false); }}
                type="button"
              >
                <span className="truncate font-mono">{project.name}</span>
                {project.id === selectedId ? <Check aria-hidden="true" className="size-4 shrink-0 text-stroke-field-focus" strokeWidth={2} /> : null}
              </button>
            ))
          )}
        </div>
      </PopoverContent>
    </Popover>
  );
}
