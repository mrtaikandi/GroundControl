import { ChevronDown } from 'lucide-react';
import { Link } from '@tanstack/react-router';
import { useEffect, useState } from 'react';
import { useGroupProjectsPage } from '@/queries/useProjects';
import { ProjectRow, type ProjectRowItem } from './ProjectRow';

interface ProjectGroupSectionProps {
  description?: string | null;
  groupId: string;
  initialNextCursor: string | null | undefined;
  initialProjects: ProjectRowItem[];
  name: string;
  search: string | undefined;
  totalCount: number;
}

interface SectionState {
  cursor: string | null | undefined;
  pendingCursor: string | undefined;
  projects: ProjectRowItem[];
}

export function ProjectGroupSection({ groupId, initialNextCursor, initialProjects, name, search, totalCount }: ProjectGroupSectionProps) {
  const [state, setState] = useState<SectionState>({
    cursor: initialNextCursor,
    pendingCursor: undefined,
    projects: initialProjects,
  });

  const next = useGroupProjectsPage(groupId, search, state.pendingCursor);

  useEffect(() => {
    if (!next.isSuccess || !next.data || !state.pendingCursor) {
      return;
    }

    const page = next.data;
    setState((current) => current.pendingCursor === undefined
      ? current
      : {
          cursor: page.nextCursor ?? null,
          pendingCursor: undefined,
          projects: [...current.projects, ...(page.data ?? [])],
        });
  }, [next.isSuccess, next.data, state.pendingCursor]);

  const remaining = Math.max(0, totalCount - state.projects.length);

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
          <h2 className="text-[15px] font-bold text-fg-heading">{name}</h2>
          <span className="font-mono text-[12px] text-fg-caption">{totalCount} {totalCount === 1 ? 'project' : 'projects'}</span>
        </div>
        <Link
          className="text-[13px] font-medium text-primary hover:underline"
          params={{ groupId }}
          to="/projects/group/$groupId"
        >
          View all →
        </Link>
      </header>

      <div className="overflow-hidden rounded-[10px] border border-stroke-divider bg-bg-surface">
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
            <span>Show more in {name}</span>
            <span className="font-mono text-[11.5px] text-fg-caption">· {remaining} left</span>
          </button>
        </div>
      ) : null}
    </section>
  );
}
