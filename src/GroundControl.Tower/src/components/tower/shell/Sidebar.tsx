import { useProjects } from '@/queries/useProjects';
import { Link, useRouterState } from '@tanstack/react-router';
import { Activity, CircleGauge, FileClock, FolderKanban, KeyRound, Layers3, ListTree, MonitorSmartphone, ScrollText, ShieldCheck, SlidersHorizontal, Users, Variable } from 'lucide-react';
import type { ComponentType } from 'react';

type StaticRoute = '/admin/groups' | '/admin/tokens' | '/admin/users' | '/audit' | '/overview' | '/projects' | '/scopes' | '/templates' | '/variables';
type ProjectScopedRoute = '/projects/$projectId/clients' | '/projects/$projectId/config' | '/projects/$projectId/snapshots';

interface NavItem {
  exact?: boolean;
  icon: ComponentType<{ className?: string; strokeWidth?: number }>;
  label: string;
  match: string[];
  params?: { projectId: string };
  to: ProjectScopedRoute | StaticRoute;
}

const adminNavItems: NavItem[] = [
  { icon: Users, label: 'Users', match: ['/admin/users'], to: '/admin/users' },
  { icon: ShieldCheck, label: 'Groups & roles', match: ['/admin/groups'], to: '/admin/groups' },
  { icon: KeyRound, label: 'Access tokens', match: ['/admin/tokens'], to: '/admin/tokens' },
];

export function Sidebar() {
  const pathname = useRouterState({ select: (state) => state.location.pathname });
  const activeProjectId = useRouterState({
    select: (state) => {
      const projectMatch = state.matches.find((entry) => entry.params && 'projectId' in entry.params);
      const value = projectMatch?.params && 'projectId' in projectMatch.params ? projectMatch.params.projectId : undefined;
      return typeof value === 'string' ? value : undefined;
    },
  });
  const projects = useProjects();
  const fallbackProjectId = activeProjectId ?? projects.data?.data[0]?.id;

  const primaryNavItems: NavItem[] = [
    { icon: CircleGauge, label: 'Overview', match: ['/overview'], to: '/overview' },
    { exact: true, icon: FolderKanban, label: 'Projects', match: ['/projects'], to: '/projects' },
    projectScoped(SlidersHorizontal, 'Configuration', '/config', '/projects/$projectId/config', fallbackProjectId),
    projectScoped(FileClock, 'Snapshots', '/snapshots', '/projects/$projectId/snapshots', fallbackProjectId),
    projectScoped(MonitorSmartphone, 'Clients', '/clients', '/projects/$projectId/clients', fallbackProjectId),
    { icon: Layers3, label: 'Scopes', match: ['/scopes'], to: '/scopes' },
    { icon: Variable, label: 'Variables', match: ['/variables'], to: '/variables' },
    { icon: ListTree, label: 'Templates', match: ['/templates'], to: '/templates' },
    { icon: Activity, label: 'Audit trail', match: ['/audit'], to: '/audit' },
  ];

  return (
    <aside className="flex h-screen flex-col border-r border-stroke-subtle bg-bg-container px-3 py-4">
      <div className="flex items-center gap-3 px-2 pb-6 pt-1">
        <LogoMark />
        <div className="min-w-0">
          <div className="truncate text-[14px] font-semibold text-fg-heading">GroundControl</div>
          <div className="text-[11.5px] text-fg-caption">Tower</div>
        </div>
      </div>

      <nav className="flex flex-1 flex-col gap-1">
        {primaryNavItems.map((item) => (
          <NavLink active={isActive(pathname, item.match, item.exact)} item={item} key={item.label} />
        ))}

        <div className="mt-5 px-3 pb-1 pt-2 text-[11px] font-medium uppercase text-fg-caption">Admin</div>
        {adminNavItems.map((item) => (
          <NavLink active={isActive(pathname, item.match, item.exact)} item={item} key={item.label} />
        ))}
      </nav>
    </aside>
  );
}

function NavLink({ active, item }: { active: boolean; item: NavItem }) {
  const Icon = item.icon;
  const linkProps = item.params ? { params: item.params, to: item.to } : { to: item.to };

  return (
    <Link
      className={`relative flex h-9 items-center gap-3 rounded-lg px-3 text-[13px] transition-colors ${
        active ? 'bg-bg-surface font-semibold text-fg-heading' : 'text-fg-body hover:bg-bg-surface hover:text-fg-heading'
      }`}
      {...linkProps}
    >
      <span className={`absolute left-0 top-1/2 h-5 w-0.5 -translate-y-1/2 rounded-full ${active ? 'bg-stroke-field-focus' : 'bg-transparent'}`} />
      <Icon aria-hidden="true" className={active ? 'size-4 text-stroke-field-focus' : 'size-4 text-fg-icon-subtle'} strokeWidth={1.8} />
      <span className="truncate">{item.label}</span>
    </Link>
  );
}

function projectScoped(
  icon: NavItem['icon'],
  label: string,
  matchPrefix: string,
  route: ProjectScopedRoute,
  projectId: string | undefined,
): NavItem {
  if (projectId) {
    return { icon, label, match: [matchPrefix], params: { projectId }, to: route };
  }

  return { icon, label, match: [matchPrefix], to: '/projects' };
}

function LogoMark() {
  return (
    <svg aria-hidden="true" className="size-9 shrink-0 text-stroke-field-focus" fill="none" viewBox="0 0 36 36">
      <circle cx="18" cy="18" fill="currentColor" opacity="0.13" r="16" />
      <circle cx="18" cy="18" r="10.5" stroke="currentColor" strokeWidth="1.6" />
      <circle cx="18" cy="18" fill="currentColor" r="4" />
    </svg>
  );
}

function isActive(pathname: string, matches: string[], exact?: boolean): boolean {
  if (exact) {
    return matches.some((match) => pathname === match || pathname === `${match}/`);
  }

  return matches.some((match) => pathname === match || pathname.startsWith(match) || pathname.endsWith(match));
}