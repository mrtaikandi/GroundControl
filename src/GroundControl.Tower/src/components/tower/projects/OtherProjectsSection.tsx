import { ChevronDown } from 'lucide-react';
import { useState } from 'react';
import { useGroupProjectsPage } from '@/queries/useProjects';
import { ProjectRow, type ProjectRowItem } from './ProjectRow';

interface OtherProjectsSectionProps {
  initialNextCursor: string | null | undefined;
  initialProjects: ProjectRowItem[];
  search: string | undefined;
  totalCount: number;
}

interface SectionState {
  cursor: string | null | undefined;
  pendingCursor: string | undefined;
  projects: ProjectRowItem[];
}

export function OtherProjectsSection({ initialNextCursor, initialProjects, search, totalCount }: OtherProjectsSectionProps) {
  const [state, setState] = useState<SectionState>({
    cursor: initialNextCursor,
    pendingCursor: undefined,
    projects: initialProjects,
  });

  const remaining = Math.max(0, totalCount - state.projects.length);
  const next = useGroupProjectsPage('ungrouped', search, state.pendingCursor);

  if (next.isSuccess && next.data && state.pendingCursor) {
    setState((current) => current.pendingCursor === undefined
      ? current
      : {
          cursor: next.data!.nextCursor ?? null,
          pendingCursor: undefined,
          projects: [...current.projects, ...(next.data!.data ?? [])],
        });
  }

  function loadMore() {
    if (!state.cursor) {
      return;
    }

    setState((current) => ({ ...current, pendingCursor: current.cursor ?? undefined }));
  }

  return (
    <section className="grid gap-2.5">
      <header className="flex items-center justify-between px-1">
        <div className="flex items-baseline gap-2">
          <span className="text-[10.5px] font-semibold uppercase tracking-[0.08em] text-fg-caption">Other projects</span>
          <span className="font-mono text-[12px] text-fg-caption">{totalCount}</span>
        </div>
      </header>

      <div className="overflow-hidden rounded-[10px] border border-dashed border-stroke-divider bg-bg-surface">
        <ul className="grid divide-y divide-stroke-subtle">
          {state.projects.map((project) => (
            <li key={project.id}>
              <ProjectRow project={project} />
            </li>
          ))}
        </ul>
      </div>

      {state.cursor && remaining > 0 ? (
        <div className="flex justify-center">
          <button
            className="inline-flex items-center gap-2 rounded-full border border-stroke-divider bg-bg-surface px-4 py-1.5 text-[12.5px] text-fg-body transition-colors hover:bg-bg-container disabled:opacity-50"
            disabled={next.isFetching}
            onClick={loadMore}
            type="button"
          >
            <ChevronDown aria-hidden="true" className="size-3.5" />
            <span>Show more in Other projects</span>
            <span className="font-mono text-[11.5px] text-fg-caption">· {remaining} left</span>
          </button>
        </div>
      ) : null}
    </section>
  );
}