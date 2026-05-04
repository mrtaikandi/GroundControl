import { Link, useRouterState } from '@tanstack/react-router';
import { LayoutGrid, MonitorSmartphone, ScrollText, SlidersHorizontal } from 'lucide-react';
import type { ComponentType } from 'react';
import { cn } from '@/lib/utils';

type TabRoute =
  | '/projects/$projectId'
  | '/projects/$projectId/clients'
  | '/projects/$projectId/config'
  | '/projects/$projectId/snapshots';

interface TabItem {
  count?: number;
  exact?: boolean;
  icon: ComponentType<{ className?: string; strokeWidth?: number }>;
  label: string;
  to: TabRoute;
}

interface ProjectTabsProps {
  clientCount?: number;
  configCount?: number;
  projectId: string;
  snapshotCount?: number;
}

export function ProjectTabs({ clientCount, configCount, projectId, snapshotCount }: ProjectTabsProps) {
  const pathname = useRouterState({ select: (state) => state.location.pathname });
  const projectRoot = `/projects/${projectId}`;
  const tabs: TabItem[] = [
    { exact: true, icon: LayoutGrid, label: 'Overview', to: '/projects/$projectId' },
    { count: configCount, icon: SlidersHorizontal, label: 'Configuration', to: '/projects/$projectId/config' },
    { count: snapshotCount, icon: ScrollText, label: 'Snapshots', to: '/projects/$projectId/snapshots' },
    { count: clientCount, icon: MonitorSmartphone, label: 'Clients', to: '/projects/$projectId/clients' },
  ];

  return (
    <div className="-mx-page-h border-b border-stroke-divider px-page-h 2xl:-mx-8 2xl:px-8">
      <nav aria-label="Project sections" className="flex flex-wrap gap-1">
        {tabs.map((tab) => {
          const Icon = tab.icon;
          const target = tab.to.replace('/projects/$projectId', projectRoot);
          const active = tab.exact
            ? pathname === target || pathname === `${target.replace(/\/$/, '')}`
            : pathname === target || pathname.startsWith(`${target}/`);

          return (
            <Link
              className={cn(
                '-mb-px inline-flex items-center gap-2 border-b-2 px-3 py-3 text-[13px] transition-colors',
                active ? 'border-stroke-field-focus text-fg-heading' : 'border-transparent text-fg-caption hover:text-fg-body',
              )}
              key={tab.label}
              params={{ projectId }}
              to={tab.to}
            >
              <Icon aria-hidden="true" className={active ? 'size-4 text-stroke-field-focus' : 'size-4 text-fg-icon-subtle'} strokeWidth={1.8} />
              <span className={active ? 'font-semibold' : undefined}>{tab.label}</span>
              {typeof tab.count === 'number' ? (
                <span className={cn('rounded-full px-1.5 py-px font-mono text-[11px]', active ? 'bg-bg-selected text-fg-on-selected' : 'bg-bg-container text-fg-caption')}>
                  {tab.count}
                </span>
              ) : null}
            </Link>
          );
        })}
      </nav>
    </div>
  );
}
