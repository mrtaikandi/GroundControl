import { Link, useRouterState } from '@tanstack/react-router';
import { LayoutGrid, MonitorSmartphone, ScrollText, SlidersHorizontal } from 'lucide-react';
import type { ComponentType } from 'react';
import { PageContent } from '@/components/tower/shell/PageContent';
import { cn } from '@/lib/utils';

type TabRoute =
  | '/projects/$projectId'
  | '/projects/$projectId/clients'
  | '/projects/$projectId/config'
  | '/projects/$projectId/snapshots';

interface TabItem {
  exact?: boolean;
  icon: ComponentType<{ className?: string; strokeWidth?: number }>;
  label: string;
  to: TabRoute;
}

interface ProjectTabsProps {
  projectId: string;
}

export function ProjectTabs({ projectId }: ProjectTabsProps) {
  const pathname = useRouterState({ select: (state) => state.location.pathname });
  const projectRoot = `/projects/${projectId}`;
  const tabs: TabItem[] = [
    { exact: true, icon: LayoutGrid, label: 'Overview', to: '/projects/$projectId' },
    { icon: SlidersHorizontal, label: 'Configuration', to: '/projects/$projectId/config' },
    { icon: ScrollText, label: 'Snapshots', to: '/projects/$projectId/snapshots' },
    { icon: MonitorSmartphone, label: 'Clients', to: '/projects/$projectId/clients' },
  ];

  return (
    <div className="border-b border-stroke-divider">
      <PageContent>
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
            </Link>
          );
        })}
        </nav>
      </PageContent>
    </div>
  );
}
