import { Link } from '@tanstack/react-router';
import { formatRelativeTime } from '@/lib/relative-time';
import type { ApiResponse } from '@/api/client';

export type ProjectRowItem = ApiResponse<'GetProjectHandler'>;

interface ProjectRowProps {
  project: ProjectRowItem;
}

export function ProjectRow({ project }: ProjectRowProps) {
  return (
    <Link
      className="grid cursor-pointer grid-cols-[minmax(0,1fr)_130px] items-center gap-4 px-[18px] py-[14px] transition-colors hover:bg-bg-container focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-stroke-field-focus"
      params={{ projectId: project.id }}
      to="/projects/$projectId"
    >
      <div className="min-w-0">
        <div className="font-mono text-[13.5px] font-semibold text-fg-heading">{project.name}</div>
        {project.description ? (
          <div className="mt-1 truncate text-[12.5px] leading-relaxed text-fg-body">{project.description}</div>
        ) : null}
      </div>
      <div className="text-right text-[12px] text-fg-caption">
        Updated {formatRelativeTime(project.updatedAt)}
      </div>
    </Link>
  );
}