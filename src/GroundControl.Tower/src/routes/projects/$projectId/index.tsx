import { createFileRoute, Link, useNavigate } from '@tanstack/react-router';
import { Braces, FolderTree, List } from 'lucide-react';
import { useState } from 'react';
import { Badge } from '@/components/tower/data/Badge';
import { SegmentedControl } from '@/components/tower/data/SegmentedControl';
import { ConfigFlatView } from '@/components/tower/config/ConfigFlatView';
import { ConfigJsonView } from '@/components/tower/config/ConfigJsonView';
import { ConfigTreeView } from '@/components/tower/config/ConfigTreeView';
import { TemplateAttachmentsBar } from '@/components/tower/projects/TemplateAttachmentsBar';
import { PublishModal } from '@/components/tower/snapshots/PublishModal';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { formatUserId } from '@/lib/user';
import { useClients } from '@/queries/useClients';
import { useGroups } from '@/queries/useGroups';
import { useProjects } from '@/queries/useProjects';
import { useSnapshotDetail } from '@/queries/useSnapshots';
import { useTweaksStore } from '@/store/tweaks';

export const Route = createFileRoute('/projects/$projectId/')({
  component: ProjectDetailRoute,
});

function ProjectDetailRoute() {
  const { projectId } = Route.useParams();
  const projects = useProjects();
  const groups = useGroups();
  const clients = useClients(projectId);
  const project = projects.data?.data.find((candidate) => candidate.id === projectId);
  const activeSnapshotId = project?.activeSnapshotId || undefined;
  const activeSnapshot = useSnapshotDetail(projectId, activeSnapshotId);
  const groupName = project?.groupId ? groups.data?.data.find((g) => g.id === project.groupId)?.name ?? 'group pending' : 'ungrouped';
  const configViewMode = useTweaksStore((state) => state.configViewMode);
  const setConfigViewMode = useTweaksStore((state) => state.setConfigViewMode);
  const [publishing, setPublishing] = useState(false);

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

  const clientItems = clients.data?.data ?? [];
  const activeClients = clientItems.filter((client) => client.isActive).length;
  const revokedClients = clientItems.length - activeClients;
  const lastUsedAt = clientItems.reduce<string | null>((acc, client) => {
    if (!client.lastUsedAt) {
      return acc;
    }

    return !acc || new Date(client.lastUsedAt).getTime() > new Date(acc).getTime() ? client.lastUsedAt : acc;
  }, null);

  return (
    <div className="grid gap-6">
      <div>
        <Link className="text-[12.5px] text-fg-caption transition-colors hover:text-fg-body" to="/projects">
          ← All projects
        </Link>
        <div className="mt-2 flex flex-wrap items-center gap-3">
          <h1 className="font-mono text-[34px] font-bold leading-tight text-fg-heading">{project.name}</h1>
          <Badge variant="neutral">{groupName}</Badge>
          <Badge variant={activeSnapshotId ? 'info' : 'neutral'}>
            {activeSnapshotId ? 'active snapshot' : 'no active snapshot'}
          </Badge>
        </div>
        <p className="mt-3 max-w-3xl text-[14.5px] text-fg-body">
          {project.description || 'No description provided.'}
        </p>
        <p className="mt-2 text-[12.5px] text-fg-caption">
          Created {formatDate(project.createdAt)} · Updated {formatDate(project.updatedAt)}
        </p>
      </div>

      <div className="grid gap-5 xl:grid-cols-2">
        <ActiveSnapshotPanel
          isLoading={Boolean(activeSnapshotId) && activeSnapshot.isLoading}
          onPublish={() => setPublishing(true)}
          projectId={projectId}
          snapshot={activeSnapshot.data}
        />
        <ClientsSummaryPanel
          activeCount={activeClients}
          isLoading={clients.isLoading}
          lastUsedAt={lastUsedAt}
          projectId={projectId}
          revokedCount={revokedClients}
          totalCount={clientItems.length}
        />
      </div>

      <ConfigurationSection
        projectId={projectId}
        configViewMode={configViewMode}
        setConfigViewMode={setConfigViewMode}
      />

      <PublishModal activeSnapshotId={activeSnapshotId} onOpenChange={setPublishing} open={publishing} projectId={projectId} />
    </div>
  );
}

interface ActiveSnapshotPanelProps {
  isLoading: boolean;
  onPublish: () => void;
  projectId: string;
  snapshot: { description?: string | null; entries: unknown[]; publishedAt: string; publishedBy: string; snapshotVersion: number | string } | undefined;
}

function ActiveSnapshotPanel({ isLoading, onPublish, projectId, snapshot }: ActiveSnapshotPanelProps) {
  const entryCount = snapshot?.entries.length ?? 0;
  return (
    <section className="flex flex-col rounded-xl border border-stroke-subtle bg-bg-surface p-5">
      <div className="flex items-start justify-between gap-3">
        <div>
          <h2 className="text-[15px] font-semibold text-fg-heading">Active snapshot</h2>
          <p className="text-[12.5px] text-fg-caption">The snapshot clients receive right now.</p>
        </div>
        <Link
          className="text-[12.5px] font-medium text-fg-link transition-colors hover:underline"
          params={{ projectId }}
          to="/projects/$projectId/snapshots"
        >
          Open snapshots →
        </Link>
      </div>

      <div className="mt-4 min-h-24 flex-1">
        {isLoading ? <Skeleton className="h-20" /> : null}
        {!isLoading && !snapshot ? (
          <div className="rounded-lg border border-dashed border-stroke-subtle bg-bg-container px-4 py-5 text-center text-[12.5px] text-fg-caption">
            No snapshot has been published yet. Configure entries below and publish to make them available to clients.
          </div>
        ) : null}
        {!isLoading && snapshot ? (
          <div>
            <div className="flex flex-wrap items-baseline gap-2">
              <span className="font-mono text-[18px] font-semibold text-fg-heading">v{String(snapshot.snapshotVersion)}</span>
              {snapshot.description?.trim() ? <span className="truncate text-[13px] text-fg-body">{snapshot.description.trim()}</span> : null}
            </div>
            <p className="mt-2 text-[12.5px] text-fg-caption">
              Published {formatDate(snapshot.publishedAt)} by {formatUserId(snapshot.publishedBy)} · {entryCount} resolved {entryCount === 1 ? 'entry' : 'entries'}
            </p>
          </div>
        ) : null}
      </div>

      <div className="mt-5">
        <Button onClick={onPublish} type="button">Publish new snapshot</Button>
      </div>
    </section>
  );
}

interface ClientsSummaryPanelProps {
  activeCount: number;
  isLoading: boolean;
  lastUsedAt: string | null;
  projectId: string;
  revokedCount: number;
  totalCount: number;
}

function ClientsSummaryPanel({ activeCount, isLoading, lastUsedAt, projectId, revokedCount, totalCount }: ClientsSummaryPanelProps) {
  return (
    <section className="flex flex-col rounded-xl border border-stroke-subtle bg-bg-surface p-5">
      <div className="flex items-start justify-between gap-3">
        <div>
          <h2 className="text-[15px] font-semibold text-fg-heading">Clients</h2>
          <p className="text-[12.5px] text-fg-caption">Credentials that read this project's configuration.</p>
        </div>
        <Link
          className="text-[12.5px] font-medium text-fg-link transition-colors hover:underline"
          params={{ projectId }}
          to="/projects/$projectId/clients"
        >
          Manage clients →
        </Link>
      </div>

      <div className="mt-4 min-h-24 flex-1">
        {isLoading ? <Skeleton className="h-20" /> : null}
        {!isLoading && totalCount === 0 ? (
          <div className="rounded-lg border border-dashed border-stroke-subtle bg-bg-container px-4 py-5 text-center text-[12.5px] text-fg-caption">
            No clients have been issued yet. Issue a credential to allow an app to read this project.
          </div>
        ) : null}
        {!isLoading && totalCount > 0 ? (
          <div>
            <div className="flex flex-wrap items-baseline gap-3">
              <span className="font-mono text-[18px] font-semibold text-fg-heading">{activeCount}</span>
              <span className="text-[13px] text-fg-body">active</span>
              {revokedCount > 0 ? (
                <>
                  <span aria-hidden="true" className="text-fg-caption">·</span>
                  <span className="font-mono text-[14px] text-fg-caption">{revokedCount}</span>
                  <span className="text-[13px] text-fg-caption">revoked</span>
                </>
              ) : null}
            </div>
            <p className="mt-2 text-[12.5px] text-fg-caption">
              {lastUsedAt ? `Last used ${formatDate(lastUsedAt)}` : 'No activity recorded yet.'}
            </p>
          </div>
        ) : null}
      </div>
    </section>
  );
}

interface ConfigurationSectionProps {
  configViewMode: 'flat' | 'json' | 'tree';
  projectId: string;
  setConfigViewMode: (mode: 'flat' | 'json' | 'tree') => void;
}

function ConfigurationSection({ configViewMode, projectId, setConfigViewMode }: ConfigurationSectionProps) {
  const navigate = useNavigate();

  return (
    <section className="grid gap-4">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h2 className="text-[19px] font-semibold text-fg-heading">Configuration</h2>
          <p className="mt-1 text-[12.5px] text-fg-caption">Manage the entries that resolve into the next snapshot.</p>
        </div>
        <div className="flex flex-wrap items-center gap-3">
          <SegmentedControl
            onChange={setConfigViewMode}
            options={[{ icon: List, label: 'Flat', value: 'flat' }, { icon: FolderTree, label: 'Tree', value: 'tree' }, { icon: Braces, label: 'JSON', value: 'json' }]}
            value={configViewMode}
          />
          <Button
            onClick={() => navigate({ params: { projectId }, to: '/projects/$projectId/config' })}
            type="button"
            variant="secondary"
          >
            Open full editor
          </Button>
        </div>
      </div>

      <TemplateAttachmentsBar projectId={projectId} />

      {configViewMode === 'tree' ? <ConfigTreeView projectId={projectId} /> : configViewMode === 'json' ? <ConfigJsonView projectId={projectId} /> : <ConfigFlatView projectId={projectId} />}
    </section>
  );
}

function formatDate(value: string) {
  return new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value));
}
