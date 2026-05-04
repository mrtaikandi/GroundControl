import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import { DriftBanner } from '@/components/tower/feedback/DriftBanner';
import { Badge } from '@/components/tower/data/Badge';
import { StatusDot } from '@/components/tower/data/StatusDot';
import { PageHeader } from '@/components/tower/shell/PageHeader';
import { liveActivityAtom, liveAuditRecordsAtom } from '@/lib/atoms';
import { maxLiveAuditRecords } from '@/lib/live-audit';
import { isSystemUser, SYSTEM_USER_LABEL } from '@/lib/user';
import { useAuditRecords, type AuditRecord } from '@/queries/useAuditRecords';
import { useOverviewStats } from '@/queries/useOverviewStats';
import { DefaultProjectsSearch } from '@/routes/projects';
import { useTweaksStore } from '@/store/tweaks';
import { createFileRoute, Link } from '@tanstack/react-router';
import { useAtomValue } from 'jotai';
import { Activity, Boxes, FileClock, FolderKanban, KeyRound, Layers3, ScrollText, ShieldCheck, UserRound } from 'lucide-react';
import { useMemo } from 'react';

export const Route = createFileRoute('/overview')({
  component: OverviewRoute,
});

function OverviewRoute() {
  const stats = useOverviewStats();
  const liveActivity = useAtomValue(liveActivityAtom);
  const liveAuditRecords = useAtomValue(liveAuditRecordsAtom);
  const driftBannerVisible = useTweaksStore((state) => state.driftBannerVisible);
  const audit = useAuditRecords({ entityTypes: [] });
  const initialAuditRecords = useMemo(() => audit.data?.pages.flatMap((page) => page.data) ?? [], [audit.data]);
  const records = useMemo(() => mergeAuditRecords(liveAuditRecords, initialAuditRecords), [initialAuditRecords, liveAuditRecords]);
  const liveIds = useMemo(() => new Set(liveAuditRecords.map((record) => record.id)), [liveAuditRecords]);

  return (
    <div className="grid gap-8">
      <PageHeader
        actions={(
          <div className="flex items-center gap-2 rounded-full border border-stroke-subtle bg-bg-surface px-3 py-2 text-[12.5px] text-fg-caption">
          <StatusDot pulse={liveActivity.isConnected} status={liveActivity.isConnected ? 'live' : liveActivity.lastEventAt ? 'warning' : 'offline'} />
          <span>{liveActivity.isConnected ? 'Live' : liveActivity.lastEventAt ? 'Reconnecting' : 'Offline'}</span>
          </div>
        )}
        align="start"
        description="Live operations, recent changes and deployment health across GroundControl."
        title="Overview"
      />

      <div className="grid gap-4 lg:grid-cols-3">
        <StatCard href="/projects" icon={FolderKanban} label="Active projects" loading={stats.isLoading} value={stats.activeProjects} />
        <StatCard icon={KeyRound} label="Active clients" value={liveActivity.clientCount} />
        <StatCard icon={FileClock} label="Snapshots today" loading={stats.isLoading} value={stats.snapshotsToday} />
      </div>

      <div className="grid gap-6 xl:grid-cols-[1fr_360px]">
        <Card className="overflow-hidden rounded-xl border-stroke-subtle bg-bg-surface">
          <CardHeader className="border-b border-stroke-subtle p-5">
            <div className="flex items-center justify-between gap-3">
              <CardTitle className="text-[18px]">Live activity</CardTitle>
              <Badge variant={audit.isFetching ? 'warning' : 'success'}>{audit.isFetching ? 'syncing' : 'current'}</Badge>
            </div>
          </CardHeader>
          <CardContent className="p-0">
            {audit.isLoading ? <ActivitySkeleton /> : null}
            {!audit.isLoading && records.length === 0 ? <div className="px-5 py-12 text-center text-[13px] text-fg-caption">No audit records yet.</div> : null}
            <div className="divide-y divide-stroke-subtle">
              {records.map((record) => <ActivityItem animate={liveIds.has(record.id)} key={record.id} record={record} />)}
            </div>
          </CardContent>
        </Card>

        <div className="grid content-start gap-6">
          <section className="grid gap-3">
            <div>
              <h2 className="text-[18px] font-semibold text-fg-heading">Alerts</h2>
              <p className="mt-1 text-[13px] text-fg-caption">Current operator attention items.</p>
            </div>
            {driftBannerVisible ? <DriftBanner /> : <div className="rounded-xl border border-stroke-subtle bg-bg-surface px-4 py-5 text-[13px] text-fg-caption">No active alerts.</div>}
          </section>

          <Card className="rounded-xl border-stroke-subtle bg-bg-surface">
            <CardHeader className="p-5 pb-3">
              <CardTitle className="text-[18px]">Snapshot pulse</CardTitle>
            </CardHeader>
            <CardContent className="grid gap-3 p-5 pt-0 text-[13px] text-fg-caption">
              <div className="flex items-center justify-between gap-3"><span>Projects scanned</span><span className="font-mono text-fg-heading">{formatCount(stats.projects.length)}</span></div>
              <div className="flex items-center justify-between gap-3"><span>Stats refresh</span><span className="font-mono text-fg-heading">{stats.isFetching ? 'active' : 'idle'}</span></div>
              <div className="flex items-center justify-between gap-3"><span>Events/sec</span><span className="font-mono text-fg-heading">{formatRate(liveActivity.eventsPerSecond)}</span></div>
            </CardContent>
          </Card>
        </div>
      </div>
    </div>
  );
}

function StatCard({ href, icon: Icon, label, loading = false, value }: { href?: '/projects'; icon: typeof FolderKanban; label: string; loading?: boolean; value: number }) {
  const content = (
    <Card className="rounded-xl border-stroke-subtle bg-bg-surface transition-colors hover:border-stroke-field-initial">
      <CardContent className="flex items-center justify-between gap-5 p-5">
        <div>
          <div className="text-[12px] font-medium text-fg-caption">{label}</div>
          {loading ? <Skeleton className="mt-3 h-9 w-20" /> : <div className="mt-2 text-[32px] font-semibold leading-none text-fg-heading">{formatCount(value)}</div>}
        </div>
        <div className="grid size-10 place-items-center rounded-lg bg-bg-selected text-fg-on-selected">
          <Icon aria-hidden="true" className="size-5" strokeWidth={1.8} />
        </div>
      </CardContent>
    </Card>
  );

  return href ? <Link search={DefaultProjectsSearch} to={href}>{content}</Link> : content;
}

function ActivityItem({ animate, record }: { animate: boolean; record: AuditRecord }) {
  const Icon = iconForEntityType(record.entityType);

  return (
    <div className={`grid grid-cols-[32px_1fr_auto] gap-3 px-5 py-4 ${animate ? 'animate-tower-fade-in bg-bg-selected/50' : ''}`}>
      <div className="grid size-8 place-items-center rounded-lg bg-bg-container text-fg-icon-subtle">
        <Icon aria-hidden="true" className="size-4" strokeWidth={1.8} />
      </div>
      <div className="min-w-0">
        <div className="flex min-w-0 flex-wrap items-center gap-2">
          <span className="font-medium text-fg-heading">{record.entityType} {labelForAction(record.action)}</span>
          <Badge variant="neutral">{record.action}</Badge>
        </div>
        <div className="mt-1 truncate text-[12.5px] text-fg-caption">Actor <span className="font-mono">{isSystemUser(record.performedBy) ? SYSTEM_USER_LABEL : shortId(record.performedBy)}</span> · Entity <span className="font-mono">{shortId(record.entityId)}</span></div>
      </div>
      <time className="whitespace-nowrap pt-1 text-[12px] text-fg-caption" dateTime={record.performedAt}>{formatRelativeTime(record.performedAt)}</time>
    </div>
  );
}

function ActivitySkeleton() {
  return <div className="grid gap-3 p-5">{Array.from({ length: 5 }, (_, index) => <Skeleton className="h-14" key={index} />)}</div>;
}

function mergeAuditRecords(liveRecords: AuditRecord[], initialRecords: AuditRecord[]) {
  const seen = new Set<string>();
  const records: AuditRecord[] = [];

  for (const record of [...liveRecords, ...initialRecords]) {
    if (seen.has(record.id)) {
      continue;
    }

    seen.add(record.id);
    records.push(record);
  }

  return records.slice(0, maxLiveAuditRecords);
}

function iconForEntityType(entityType: string) {
  const icons = new Map([
    ['Client', KeyRound],
    ['ConfigEntry', Boxes],
    ['Group', ShieldCheck],
    ['PersonalAccessToken', KeyRound],
    ['Project', FolderKanban],
    ['Role', ShieldCheck],
    ['Scope', Layers3],
    ['Snapshot', FileClock],
    ['Template', ScrollText],
    ['User', UserRound],
    ['Variable', Activity],
  ]);

  return icons.get(entityType) ?? Activity;
}

function labelForAction(action: string) {
  return action.replace(/([a-z])([A-Z])/g, '$1 $2').toLowerCase();
}

function shortId(value: string) {
  return value.length > 8 ? value.slice(0, 8) : value;
}

function formatCount(value: number) {
  return new Intl.NumberFormat(undefined, { maximumFractionDigits: 0 }).format(value);
}

function formatRate(value: number) {
  return new Intl.NumberFormat(undefined, { maximumFractionDigits: value < 10 ? 1 : 0 }).format(value);
}

function formatRelativeTime(value: string) {
  const deltaSeconds = Math.max(0, Math.round((Date.now() - new Date(value).getTime()) / 1000));

  if (deltaSeconds < 60) {
    return 'just now';
  }

  const deltaMinutes = Math.round(deltaSeconds / 60);
  if (deltaMinutes < 60) {
    return `${deltaMinutes} min ago`;
  }

  const deltaHours = Math.round(deltaMinutes / 60);
  if (deltaHours < 24) {
    return `${deltaHours} hr ago`;
  }

  return new Intl.DateTimeFormat(undefined, { dateStyle: 'medium' }).format(new Date(value));
}
