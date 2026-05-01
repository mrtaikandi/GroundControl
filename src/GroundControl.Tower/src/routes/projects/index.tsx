import { createFileRoute } from '@tanstack/react-router';
import { Badge } from '@/components/tower/data/Badge';
import { InlineCode } from '@/components/tower/data/InlineCode';
import { NewProjectModal } from '@/components/tower/projects/NewProjectModal';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
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

      {projects.isLoading ? <ProjectSkeletonGrid /> : null}
      {projects.isError ? <div className="rounded-xl border border-stroke-subtle bg-bg-surface p-6 text-fg-caption">Projects could not be loaded.</div> : null}
      {!projects.isLoading && !projects.isError && projectItems.length === 0 ? <EmptyProjects /> : null}
      {projectItems.length > 0 ? (
        <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
          {projectItems.map((project) => (
            <Card key={project.id}>
              <CardHeader>
                <CardTitle><InlineCode>{project.name}</InlineCode></CardTitle>
                <div className="flex flex-wrap gap-2 pt-2">
                  <Badge variant="neutral">{project.groupId ? groupNames.get(project.groupId) ?? 'group pending' : 'ungrouped'}</Badge>
                  <Badge variant={project.activeSnapshotId ? 'info' : 'neutral'}>{project.activeSnapshotId ? 'active snapshot' : 'no active snapshot'}</Badge>
                </div>
              </CardHeader>
              <CardContent className="grid gap-4 text-[12.5px] text-fg-caption">
                <p className="min-h-10 text-fg-body">{project.description || 'No description provided.'}</p>
                <div className="grid grid-cols-2 gap-3">
                  <Metric label="Entries" value="—" />
                  <Metric label="Templates" value={project.templateIds.length.toString()} />
                </div>
                <div>Updated {formatDate(project.updatedAt)}</div>
              </CardContent>
            </Card>
          ))}
        </div>
      ) : null}
    </div>
  );
}

function ProjectSkeletonGrid() {
  return (
    <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
      {Array.from({ length: 6 }, (_, index) => <Skeleton className="h-52" key={index} />)}
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

function Metric({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-lg bg-bg-container px-3 py-2">
      <div className="text-[11.5px] text-fg-caption">{label}</div>
      <div className="mt-1 font-mono text-[13px] text-fg-heading">{value}</div>
    </div>
  );
}

function formatDate(value: string) {
  return new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value));
}
