import { createFileRoute, Link } from '@tanstack/react-router';
import { Layers3 } from 'lucide-react';
import { Badge } from '@/components/tower/data/Badge';
import { InlineCode } from '@/components/tower/data/InlineCode';
import { Skeleton } from '@/components/ui/skeleton';
import { formatUserId } from '@/lib/user';
import { useClients } from '@/queries/useClients';
import { useConfigEntries } from '@/queries/useConfigEntries';
import { useProjects } from '@/queries/useProjects';
import { useSnapshots, type SnapshotSummary } from '@/queries/useSnapshots';
import { useTemplates, type Template } from '@/queries/useTemplates';

export const Route = createFileRoute('/projects/$projectId/')({
  component: ProjectOverviewRoute,
});

function ProjectOverviewRoute() {
  const { projectId } = Route.useParams();
  const projects = useProjects();
  const project = projects.data?.data.find((candidate) => candidate.id === projectId);
  const snapshots = useSnapshots(projectId);
  const clients = useClients(projectId);
  const configEntries = useConfigEntries(projectId);
  const templates = useTemplates();

  if (!project) {
    return null;
  }

  const snapshotItems = snapshots.data?.data ?? [];
  const totalSnapshots = snapshots.data?.totalCount !== undefined ? Number(snapshots.data.totalCount) : snapshotItems.length;
  const activeSnapshotId = project.activeSnapshotId || undefined;
  const activeSnapshot = activeSnapshotId ? snapshotItems.find((snapshot) => snapshot.id === activeSnapshotId) : undefined;
  const latestSnapshot = snapshotItems[0];
  const clientItems = clients.data?.data ?? [];
  const totalClients = clients.data?.totalCount !== undefined ? Number(clients.data.totalCount) : clientItems.length;
  const activeClients = clientItems.filter((client) => client.isActive).length;
  const configItems = configEntries.data?.data ?? [];
  const projectOwnedConfigCount = configItems.length;
  const inheritedConfigCount = Math.max(0, (configEntries.data?.totalCount !== undefined ? Number(configEntries.data.totalCount) : projectOwnedConfigCount) - projectOwnedConfigCount);
  const attachedTemplates = (templates.data?.data ?? []).filter((template) => project.templateIds?.includes(template.id));

  return (
    <div className="grid gap-5">
      <div className="grid gap-3 sm:grid-cols-3">
        <SummaryCard
          eyebrow="Active snapshot"
          isLoading={projects.isLoading || snapshots.isLoading}
          primary={activeSnapshot ? <span className="font-mono text-[24px] font-bold text-fg-heading">v{activeSnapshot.snapshotVersion}</span> : <span className="text-[15px] text-fg-caption">No active snapshot</span>}
          secondary={activeSnapshot ? `published ${formatRelative(activeSnapshot.publishedAt)} by ${formatUserId(activeSnapshot.publishedBy)}` : 'Publish a snapshot to make config available to clients.'}
        />
        <SummaryCard
          eyebrow="Config entries"
          isLoading={configEntries.isLoading}
          primary={<span className="font-mono text-[24px] font-bold text-fg-heading">{configEntries.data?.totalCount !== undefined ? Number(configEntries.data.totalCount) : projectOwnedConfigCount}</span>}
          secondary={`${projectOwnedConfigCount} project · ${inheritedConfigCount} inherited`}
        />
        <SummaryCard
          eyebrow="Clients"
          isLoading={clients.isLoading}
          primary={<span className="font-mono text-[24px] font-bold text-fg-heading">{totalClients}</span>}
          secondary={`${activeClients} active · bound to ${project.name}`}
        />
      </div>

      <div className="grid gap-5 lg:grid-cols-2">
        <RecentSnapshotsPanel isLoading={snapshots.isLoading} latestSnapshot={latestSnapshot} projectId={projectId} snapshots={snapshotItems} totalCount={totalSnapshots} />
        <div className="grid gap-5">
          <InheritancePanel isLoading={templates.isLoading} templates={attachedTemplates} />
          <SnapshotsHistoryPanel isLoading={snapshots.isLoading} latestSnapshot={latestSnapshot} projectId={projectId} snapshots={snapshotItems} totalCount={totalSnapshots} />
        </div>
      </div>
    </div>
  );
}

interface SummaryCardProps {
  eyebrow: string;
  isLoading: boolean;
  primary: React.ReactNode;
  secondary: string;
}

function SummaryCard({ eyebrow, isLoading, primary, secondary }: SummaryCardProps) {
  return (
    <section className="rounded-xl border border-stroke-subtle bg-bg-surface p-4">
      <div className="text-[11px] font-medium uppercase tracking-wide text-fg-caption">{eyebrow}</div>
      {isLoading ? <Skeleton className="mt-2 h-7 w-20" /> : <div className="mt-1.5">{primary}</div>}
      <div className="mt-2 text-[12.5px] text-fg-caption">{secondary}</div>
    </section>
  );
}

interface RecentSnapshotsPanelProps {
  isLoading: boolean;
  latestSnapshot: SnapshotSummary | undefined;
  projectId: string;
  snapshots: SnapshotSummary[];
  totalCount: number;
}

function RecentSnapshotsPanel({ isLoading, latestSnapshot, projectId, snapshots, totalCount }: RecentSnapshotsPanelProps) {
  const recent = snapshots.slice(0, 5);

  return (
    <section className="rounded-xl border border-stroke-subtle bg-bg-surface p-5">
      <div className="flex items-start justify-between gap-3">
        <div>
          <h2 className="text-[15px] font-semibold text-fg-heading">Recent activity</h2>
          <p className="text-[12.5px] text-fg-caption">Latest snapshot publishes for this project.</p>
        </div>
        <Link className="text-[12.5px] font-medium text-fg-link transition-colors hover:underline" to="/audit">
          open audit →
        </Link>
      </div>

      <div className="mt-4 grid gap-2">
        {isLoading ? <Skeleton className="h-24" /> : null}
        {!isLoading && recent.length === 0 ? (
          <div className="rounded-lg border border-dashed border-stroke-subtle bg-bg-container px-4 py-5 text-center text-[12.5px] text-fg-caption">
            No snapshot activity yet.
          </div>
        ) : null}
        {!isLoading && recent.map((snapshot) => (
          <div className="flex flex-wrap items-center justify-between gap-3 rounded-lg px-2 py-2 text-[12.5px] hover:bg-bg-container" key={snapshot.id}>
            <div className="flex min-w-0 items-center gap-3">
              <Badge variant={snapshot.id === latestSnapshot?.id ? 'success' : 'neutral'}>publish</Badge>
              <div className="min-w-0">
                <div className="flex flex-wrap items-baseline gap-2">
                  <span className="font-mono text-[13px] font-semibold text-fg-heading">v{snapshot.snapshotVersion}</span>
                  {snapshot.description?.trim() ? <span className="truncate text-fg-body">{snapshot.description.trim()}</span> : <span className="text-fg-caption">no description</span>}
                </div>
              </div>
            </div>
            <span className="shrink-0 font-mono text-[11.5px] text-fg-caption">{formatUserId(snapshot.publishedBy)} · {formatRelative(snapshot.publishedAt)}</span>
          </div>
        ))}
      </div>

      {totalCount > recent.length ? (
        <Link className="mt-3 inline-flex text-[12.5px] font-medium text-fg-link transition-colors hover:underline" params={{ projectId }} to="/projects/$projectId/snapshots">
          View all {totalCount} snapshots →
        </Link>
      ) : null}
    </section>
  );
}

function InheritancePanel({ isLoading, templates }: { isLoading: boolean; templates: Template[] }) {
  return (
    <section className="rounded-xl border border-stroke-subtle bg-bg-surface p-5">
      <h2 className="text-[15px] font-semibold text-fg-heading">Inheritance</h2>
      <p className="text-[12.5px] text-fg-caption">Templates this project pulls entries from.</p>

      <div className="mt-4 grid gap-2">
        {isLoading ? <Skeleton className="h-16" /> : null}
        {!isLoading && templates.length === 0 ? (
          <div className="rounded-lg border border-dashed border-stroke-subtle bg-bg-container px-4 py-5 text-center text-[12.5px] text-fg-caption">
            No templates attached.
          </div>
        ) : null}
        {!isLoading && templates.map((template) => (
          <div className="flex items-center justify-between gap-3 rounded-lg border border-stroke-subtle bg-bg-container px-3 py-2" key={template.id}>
            <div className="flex min-w-0 items-center gap-2">
              <Layers3 aria-hidden="true" className="size-4 text-fg-icon-subtle" strokeWidth={1.8} />
              <InlineCode>{template.name}</InlineCode>
            </div>
          </div>
        ))}
      </div>
    </section>
  );
}

function SnapshotsHistoryPanel({ isLoading, latestSnapshot, projectId, snapshots, totalCount }: RecentSnapshotsPanelProps) {
  const recent = snapshots.slice(0, 4);

  return (
    <section className="rounded-xl border border-stroke-subtle bg-bg-surface p-5">
      <div className="flex items-start justify-between gap-3">
        <div>
          <h2 className="text-[15px] font-semibold text-fg-heading">Recent snapshots</h2>
          <p className="text-[12.5px] text-fg-caption">{totalCount} total{latestSnapshot ? ` · v${latestSnapshot.snapshotVersion} is the latest` : ''}</p>
        </div>
        <Link className="text-[12.5px] font-medium text-fg-link transition-colors hover:underline" params={{ projectId }} to="/projects/$projectId/snapshots">
          view all →
        </Link>
      </div>
      <div className="mt-3 grid gap-2">
        {isLoading ? <Skeleton className="h-24" /> : null}
        {!isLoading && recent.length === 0 ? (
          <div className="rounded-lg border border-dashed border-stroke-subtle bg-bg-container px-3 py-4 text-center text-[12.5px] text-fg-caption">
            No snapshots yet.
          </div>
        ) : null}
        {!isLoading && recent.map((snapshot) => (
          <div className="flex items-start justify-between gap-3 rounded-lg px-2 py-2 hover:bg-bg-container" key={snapshot.id}>
            <div className="min-w-0">
              <div className="flex items-center gap-2">
                <span className="font-mono text-[13px] font-semibold text-fg-heading">v{snapshot.snapshotVersion}</span>
                {snapshot.description?.trim() ? <span className="truncate text-[12.5px] text-fg-body">{snapshot.description.trim()}</span> : null}
              </div>
              <div className="mt-1 truncate text-[11.5px] text-fg-caption">
                {formatUserId(snapshot.publishedBy)} · {formatRelative(snapshot.publishedAt)}
              </div>
            </div>
            {snapshot.id === latestSnapshot?.id ? <Badge variant="success">latest</Badge> : null}
          </div>
        ))}
      </div>
    </section>
  );
}

function formatRelative(value: string) {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return value;
  }

  const now = new Date();
  const time = `${String(date.getHours()).padStart(2, '0')}:${String(date.getMinutes()).padStart(2, '0')}`;
  const isSameDay = date.toDateString() === now.toDateString();
  if (isSameDay) {
    return `${time} today`;
  }

  const daysDiff = Math.floor((now.getTime() - date.getTime()) / (24 * 60 * 60 * 1000));
  if (daysDiff < 7 && daysDiff >= 0) {
    return `${date.toLocaleDateString(undefined, { weekday: 'short' })} ${time}`;
  }

  return `${date.toLocaleDateString(undefined, { day: '2-digit', month: 'short' })} ${time}`;
}
