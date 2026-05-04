import { createFileRoute, Link, Outlet, useRouterState } from '@tanstack/react-router';
import { Pencil, Rocket } from 'lucide-react';
import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { Badge } from '@/components/tower/data/Badge';
import { PageContent } from '@/components/tower/shell/PageContent';
import { PageHeader } from '@/components/tower/shell/PageHeader';
import { EditProjectModal } from '@/components/tower/projects/EditProjectModal';
import { ProjectStatusBar } from '@/components/tower/projects/ProjectStatusBar';
import { ProjectTabs } from '@/components/tower/projects/ProjectTabs';
import { PublishModal } from '@/components/tower/snapshots/PublishModal';
import { useClients } from '@/queries/useClients';
import { useConfigEntries } from '@/queries/useConfigEntries';
import { useGroups } from '@/queries/useGroups';
import { useProjectStatus } from '@/queries/useProjectStatus';
import { useProjects } from '@/queries/useProjects';
import { useSnapshots } from '@/queries/useSnapshots';
import { DefaultProjectsSearch } from '@/routes/projects';

export const Route = createFileRoute('/projects/$projectId')({
  component: ProjectLayout,
});

function ProjectLayout() {
  const { projectId } = Route.useParams();
  const pathname = useRouterState({ select: (state) => state.location.pathname });
  const projects = useProjects();
  const groups = useGroups();
  const project = projects.data?.data.find((candidate) => candidate.id === projectId);
  const snapshots = useSnapshots(projectId);
  const clients = useClients(projectId);
  const configEntries = useConfigEntries(projectId);
  const status = useProjectStatus(projectId);
  const [publishing, setPublishing] = useState(false);
  const [editing, setEditing] = useState(false);
  const groupName = project?.groupId ? groups.data?.data.find((g) => g.id === project.groupId)?.name ?? 'group pending' : 'ungrouped';
  const activeSnapshotId = project?.activeSnapshotId || undefined;
  const snapshotItems = snapshots.data?.data ?? [];
  const totalSnapshots = snapshots.data?.totalCount !== undefined ? Number(snapshots.data.totalCount) : snapshotItems.length;
  const configCount = configEntries.data?.totalCount !== undefined ? Number(configEntries.data.totalCount) : configEntries.data?.data.length;
  const clientCount = clients.data?.totalCount !== undefined ? Number(clients.data.totalCount) : clients.data?.data.length;

  if (projects.isLoading) {
    return <Skeleton className="h-96" />;
  }

  if (!project) {
    return (
      <div className="rounded-xl border border-stroke-subtle bg-bg-surface p-8 text-center text-fg-caption">
        Project not found.
      </div>
    );
  }

  const projectRoot = `/projects/${projectId}`;
  const isOnOverviewTab = pathname === projectRoot || pathname === `${projectRoot}/`;

  return (
    <>
      <div className="grid">
        <PageHeader
        actions={(
          <div className="flex shrink-0 items-center gap-2">
            <Button onClick={() => setEditing(true)} size="sm" type="button" variant="secondary">
              <Pencil aria-hidden="true" className="size-3.5" strokeWidth={1.8} />
              Edit
            </Button>
            <Button onClick={() => setPublishing(true)} size="sm" type="button">
              <Rocket aria-hidden="true" className="size-3.5" strokeWidth={1.8} />
              Publish snapshot
            </Button>
          </div>
        )}
        align="start"
        description={project.description || 'No description provided.'}
        descriptionClassName="max-w-3xl text-[13.5px] text-fg-body"
        eyebrow={(
          <div className="flex items-center gap-2 font-mono text-[11.5px] uppercase tracking-wide">
            <Link className="transition-colors hover:text-fg-body" search={DefaultProjectsSearch} to="/projects">Projects</Link>
            <span aria-hidden="true">/</span>
            <span className="text-fg-body">{project.name}</span>
          </div>
        )}
        eyebrowClassName="normal-case"
        title={(
          <span className="flex flex-wrap items-center gap-3">
            <span>{project.name}</span>
            <Badge variant="neutral">{groupName}</Badge>
          </span>
        )}
        titleClassName="font-mono text-[28px]"
      />

      <ProjectTabs projectId={projectId} />

        <PageContent>
          {isOnOverviewTab ? (
            <ProjectStatusBar
              onPublish={() => setPublishing(true)}
              projectId={projectId}
              status={status}
            />
          ) : null}

          <div className="mt-5">
            <Outlet />
          </div>
        </PageContent>
      </div>

      <PublishModal activeSnapshotId={activeSnapshotId} onOpenChange={setPublishing} open={publishing} projectId={projectId} />
      <EditProjectModal onOpenChange={setEditing} open={editing} project={project} />
    </>
  );
}
