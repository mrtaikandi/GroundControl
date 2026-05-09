import { createFileRoute, Link } from '@tanstack/react-router';
import { Layers3, Pencil, Plus, Rocket, Trash2, type LucideIcon } from 'lucide-react';
import { useMemo } from 'react';
import { Badge } from '@/components/tower/data/Badge';
import { InlineCode } from '@/components/tower/data/InlineCode';
import { Skeleton } from '@/components/ui/skeleton';
import { formatUserId } from '@/lib/user';
import { cn } from '@/lib/utils';
import { useEntityAuditRecords, type AuditRecord } from '@/queries/useAuditRecords';
import { useClients } from '@/queries/useClients';
import { useConfigEntries } from '@/queries/useConfigEntries';
import { useProjects } from '@/queries/useProjects';
import { useSnapshots, type SnapshotSummary } from '@/queries/useSnapshots';
import { useTemplates, type Template } from '@/queries/useTemplates';

export const Route = createFileRoute('/projects/$projectId/')({
  component: ProjectOverviewRoute,
});

const ACTIVITY_WINDOW_DAYS = 7;

function ProjectOverviewRoute() {
  const { projectId } = Route.useParams();
  const projects = useProjects();
  const project = projects.data?.data.find((candidate) => candidate.id === projectId);
  const snapshots = useSnapshots(projectId);
  const clients = useClients(projectId);
  const configEntries = useConfigEntries(projectId);
  const templates = useTemplates();
  const activityFrom = useMemo(() => sevenDaysAgoIso(), []);
  const projectAudit = useEntityAuditRecords(projectId, { from: activityFrom, limit: 20 });

  const snapshotItems = snapshots.data?.data ?? [];
  const totalSnapshots = snapshots.data?.totalCount !== undefined ? Number(snapshots.data.totalCount) : snapshotItems.length;
  const activeSnapshotId = project?.activeSnapshotId || undefined;
  const activeSnapshot = activeSnapshotId ? snapshotItems.find((snapshot) => snapshot.id === activeSnapshotId) : undefined;
  const latestSnapshot = snapshotItems[0];
  const clientItems = clients.data?.data ?? [];
  const totalClients = clients.data?.totalCount !== undefined ? Number(clients.data.totalCount) : clientItems.length;
  const activeClients = clientItems.filter((client) => client.isActive).length;
  const configItems = configEntries.data?.data ?? [];
  const projectOwnedConfigCount = configItems.length;
  const inheritedConfigCount = Math.max(0, (configEntries.data?.totalCount !== undefined ? Number(configEntries.data.totalCount) : projectOwnedConfigCount) - projectOwnedConfigCount);
  const attachedTemplates = (templates.data?.data ?? []).filter((template) => project?.templateIds?.includes(template.id));
  const activity = useMemo(
    () => buildActivityFeed(snapshotItems, projectAudit.data?.data ?? [], activityFrom),
    [activityFrom, projectAudit.data?.data, snapshotItems],
  );

  if (!project) {
    return null;
  }

  return (
    <div className="grid gap-5">
      <div className="grid gap-3 sm:grid-cols-3">
        <SummaryCard
          eyebrow="Active snapshot"
          isLoading={projects.isLoading || snapshots.isLoading}
          primary={activeSnapshot ? <span className="font-mono text-[24px] font-bold text-badge-success-fg">v{activeSnapshot.snapshotVersion}</span> : <span className="text-[15px] text-fg-caption">No active snapshot</span>}
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
        <RecentActivityPanel activity={activity} isLoading={snapshots.isLoading || projectAudit.isLoading} />
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
      <div className="mt-2 text-[12.5px] text-fg-caption [overflow-wrap:anywhere]">{secondary}</div>
    </section>
  );
}

interface ActivityItem {
  description: React.ReactNode;
  id: string;
  kind: ActivityKind;
  performedAt: string;
  performedBy: string;
}

type ActivityKind = 'create' | 'delete' | 'publish' | 'update';

interface ActivityKindStyle {
  Icon: LucideIcon;
  iconClass: string;
  label: string;
}

const ACTIVITY_KIND_STYLES: Record<ActivityKind, ActivityKindStyle> = {
  create: { Icon: Plus, iconClass: 'text-[var(--tower-badge-info-fg)]', label: 'CREATE' },
  delete: { Icon: Trash2, iconClass: 'text-[var(--tower-badge-critical-fg)]', label: 'DELETE' },
  publish: { Icon: Rocket, iconClass: 'text-[var(--tower-badge-success-fg)]', label: 'PUBLISH' },
  update: { Icon: Pencil, iconClass: 'text-[var(--tower-badge-warning-fg)]', label: 'UPDATE' },
};

function RecentActivityPanel({ activity, isLoading }: { activity: ActivityItem[]; isLoading: boolean }) {
  return (
    <section className="rounded-xl border border-stroke-subtle bg-bg-surface p-5">
      <div className="flex items-start justify-between gap-3">
        <div>
          <h2 className="text-[15px] font-semibold text-fg-heading">Recent Activity</h2>
          <p className="text-[12.5px] text-fg-caption">Last {ACTIVITY_WINDOW_DAYS} days</p>
        </div>
        <Link className="text-[12.5px] font-medium text-fg-link transition-colors hover:underline" to="/audit">
          open audit →
        </Link>
      </div>

      <div className="mt-4 grid gap-1">
        {isLoading ? <Skeleton className="h-24" /> : null}
        {!isLoading && activity.length === 0 ? (
          <div className="rounded-lg border border-dashed border-stroke-subtle bg-bg-container px-4 py-5 text-center text-[12.5px] text-fg-caption">
            No activity in the last {ACTIVITY_WINDOW_DAYS} days.
          </div>
        ) : null}
        {!isLoading && activity.map((item) => <ActivityRow item={item} key={item.id} />)}
      </div>
    </section>
  );
}

function ActivityRow({ item }: { item: ActivityItem }) {
  const style = ACTIVITY_KIND_STYLES[item.kind];

  return (
    <div className="flex items-baseline gap-3 rounded-lg px-2 py-1.5 text-[12.5px] hover:bg-bg-container">
      <div className={cn('flex shrink-0 items-baseline gap-1.5 min-w-[68px]', style.iconClass)}>
        <style.Icon aria-hidden="true" className="size-3.5 self-center" strokeWidth={1.8} />
        <span className="font-mono text-[11px] font-semibold uppercase tracking-wide">{style.label}</span>
      </div>
      <div className="min-w-0 flex-1 text-fg-body [overflow-wrap:anywhere]">{item.description}</div>
      <span className="shrink-0 font-mono text-[11.5px] text-fg-caption">{formatRelative(item.performedAt)}</span>
    </div>
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

interface SnapshotsHistoryPanelProps {
  isLoading: boolean;
  latestSnapshot: SnapshotSummary | undefined;
  projectId: string;
  snapshots: SnapshotSummary[];
  totalCount: number;
}

function SnapshotsHistoryPanel({ isLoading, latestSnapshot, projectId, snapshots, totalCount }: SnapshotsHistoryPanelProps) {
  const recent = snapshots.slice(0, 4);

  return (
    <section className="rounded-xl border border-stroke-subtle bg-bg-surface p-5">
      <div className="flex items-start justify-between gap-3">
        <div>
          <h2 className="text-[15px] font-semibold text-fg-heading">Recent Snapshots</h2>
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
                {snapshot.description?.trim() ? <span className="text-[12.5px] text-fg-body [overflow-wrap:anywhere]">{snapshot.description.trim()}</span> : null}
              </div>
              <div className="mt-1 text-[11.5px] text-fg-caption [overflow-wrap:anywhere]">
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

function sevenDaysAgoIso(): string {
  const cutoff = Date.now() - ACTIVITY_WINDOW_DAYS * 24 * 60 * 60 * 1000;
  return new Date(cutoff).toISOString();
}

function buildActivityFeed(snapshots: SnapshotSummary[], auditRecords: AuditRecord[], cutoffIso: string): ActivityItem[] {
  const cutoff = new Date(cutoffIso).getTime();
  const items: ActivityItem[] = [];

  for (const snapshot of snapshots) {
    const time = new Date(snapshot.publishedAt).getTime();

    if (Number.isFinite(time) && time >= cutoff) {
      items.push({
        description: <SnapshotDescription snapshot={snapshot} />,
        id: `snapshot-${snapshot.id}`,
        kind: 'publish',
        performedAt: snapshot.publishedAt,
        performedBy: snapshot.publishedBy,
      });
    }
  }

  for (const record of auditRecords) {
    const kind = mapAuditKind(record.action);

    if (!kind) {
      continue;
    }

    items.push({
      description: <AuditDescription record={record} />,
      id: `audit-${record.id}`,
      kind,
      performedAt: record.performedAt,
      performedBy: record.performedBy,
    });
  }

  items.sort((left, right) => new Date(right.performedAt).getTime() - new Date(left.performedAt).getTime());

  return items.slice(0, 5);
}

function mapAuditKind(action: string): ActivityKind | undefined {
  switch (action) {
    case 'Activated':
    case 'Published':
      return 'publish';
    case 'Created':
    case 'TemplateAdded':
      return 'create';
    case 'Deleted':
    case 'Revoked':
    case 'TemplateRemoved':
      return 'delete';
    case 'PasswordChanged':
    case 'Updated':
      return 'update';
    default:
      return undefined;
  }
}

function SnapshotDescription({ snapshot }: { snapshot: SnapshotSummary }) {
  const description = snapshot.description?.trim();

  return (
    <span className="flex min-w-0 items-baseline gap-2">
      <span className="font-mono font-semibold text-fg-heading">v{snapshot.snapshotVersion}</span>
      <span className="[overflow-wrap:anywhere]">{description || 'no description'}</span>
    </span>
  );
}

function AuditDescription({ record }: { record: AuditRecord }) {
  if (record.entityType === 'Project' && record.action === 'TemplateAdded') {
    return <span>Attached template</span>;
  }

  if (record.entityType === 'Project' && record.action === 'TemplateRemoved') {
    return <span>Detached template</span>;
  }

  if (record.entityType === 'Project' && record.action === 'Created') {
    return <span>Project created</span>;
  }

  if (record.entityType === 'Project' && record.action === 'Updated') {
    return <span>Project metadata updated</span>;
  }

  return <span>{humaniseEntityType(record.entityType)} {record.action.toLowerCase()}</span>;
}

function humaniseEntityType(entityType: string): string {
  return entityType.replace(/([a-z])([A-Z])/g, '$1 $2');
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
