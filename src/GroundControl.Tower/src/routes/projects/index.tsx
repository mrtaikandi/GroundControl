import { createFileRoute, Link } from '@tanstack/react-router';
import { Badge } from '@/components/tower/data/Badge';
import { InlineCode } from '@/components/tower/data/InlineCode';
import { NewProjectModal } from '@/components/tower/projects/NewProjectModal';
import { Skeleton } from '@/components/ui/skeleton';
import { useGroups } from '@/queries/useGroups';
import { useProjects } from '@/queries/useProjects';

export const Route = createFileRoute('/projects/')({
  component: ProjectsRoute,
});

function ProjectsRoute() {
  const projects = useProjects();
  const groups = useGroups();
  const groupNames = new Map((groups.data?.data ?? []).map((group) => [group.id, group.name]));
  const projectItems = projects.data?.data ?? [];

  return (
    <div className="grid gap-8">
      <div className="flex items-start justify-between gap-4">
        <div>
          <h1 className="text-[34px] font-bold leading-tight text-fg-heading">Projects</h1>
          <p className="mt-2 text-[14.5px] text-fg-caption">Manage your configuration projects</p>
        </div>
        <NewProjectModal />
      </div>

      {projects.isLoading ? <ProjectSkeletonList /> : null}
      {projects.isError ? <div className="rounded-xl border border-stroke-subtle bg-bg-surface p-6 text-fg-caption">Projects could not be loaded.</div> : null}
      {!projects.isLoading && !projects.isError && projectItems.length === 0 ? <EmptyProjects /> : null}
      {projectItems.length > 0 ? (
        <ul className="grid gap-3">
          {projectItems.map((project) => (
            <li key={project.id}>
              <Link
                className="block rounded-xl border border-stroke-subtle bg-bg-surface p-5 transition-colors hover:border-stroke-divider hover:bg-bg-container focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-stroke-field-focus"
                params={{ projectId: project.id }}
                to="/projects/$projectId"
              >
                <div className="flex flex-wrap items-center gap-3">
                  <h2 className="text-[15px] font-semibold text-fg-heading">
                    <InlineCode>{project.name}</InlineCode>
                  </h2>
                  <Badge variant="neutral">{project.groupId ? groupNames.get(project.groupId) ?? 'group pending' : 'ungrouped'}</Badge>
                  <Badge variant={project.activeSnapshotId ? 'info' : 'neutral'}>
                    {project.activeSnapshotId ? 'active snapshot' : 'no active snapshot'}
                  </Badge>
                </div>
                <div className="mt-2 flex flex-wrap items-end justify-between gap-3">
                  <p className="min-w-0 flex-1 text-[13px] text-fg-body">{project.description || 'No description provided.'}</p>
                  <span className="shrink-0 text-[12.5px] text-fg-caption">Updated {formatDate(project.updatedAt)}</span>
                </div>
              </Link>
            </li>
          ))}
        </ul>
      ) : null}
    </div>
  );
}

function ProjectSkeletonList() {
  return (
    <div className="grid gap-3">
      {Array.from({ length: 5 }, (_, index) => <Skeleton className="h-24" key={index} />)}
    </div>
  );
}

function EmptyProjects() {
  return (
    <div className="rounded-xl border border-stroke-subtle bg-bg-surface p-8 text-center">
      <h2 className="text-[19px] font-semibold text-fg-heading">No projects yet</h2>
      <p className="mx-auto mt-2 max-w-md text-[13px] text-fg-caption">Create the first project to start collecting entries, scopes, variables, templates, and snapshots.</p>
      <div className="mt-5"><NewProjectModal /></div>
    </div>
  );
}

function formatDate(value: string) {
  return new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value));
}
