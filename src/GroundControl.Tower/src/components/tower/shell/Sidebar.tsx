import { StatusDot } from '@/components/tower/data/StatusDot';
import { Link, useRouterState } from '@tanstack/react-router';
import { Activity, CircleGauge, FileClock, FolderKanban, KeyRound, Layers3, ListTree, MonitorSmartphone, ScrollText, ShieldCheck, SlidersHorizontal, Users, Variable } from 'lucide-react';
import type { ComponentType } from 'react';

interface NavItem {
  icon: ComponentType<{ className?: string; strokeWidth?: number }>;
  label: string;
  match: string[];
  to: '/admin/groups' | '/admin/tokens' | '/admin/users' | '/audit' | '/overview' | '/projects' | '/scopes' | '/templates' | '/variables';
}

const primaryNavItems: NavItem[] = [
  { icon: CircleGauge, label: 'Overview', match: ['/overview'], to: '/overview' },
  { icon: FolderKanban, label: 'Projects', match: ['/projects'], to: '/projects' },
  { icon: SlidersHorizontal, label: 'Configuration', match: ['/projects/', '/config'], to: '/projects' },
  { icon: FileClock, label: 'Snapshots', match: ['/snapshots'], to: '/projects' },
  { icon: MonitorSmartphone, label: 'Clients', match: ['/clients'], to: '/projects' },
  { icon: Layers3, label: 'Scopes', match: ['/scopes'], to: '/scopes' },
  { icon: Variable, label: 'Variables', match: ['/variables'], to: '/variables' },
  { icon: ListTree, label: 'Templates', match: ['/templates'], to: '/templates' },
  { icon: Activity, label: 'Audit trail', match: ['/audit'], to: '/audit' },
];

const adminNavItems: NavItem[] = [
  { icon: Users, label: 'Users', match: ['/admin/users'], to: '/admin/users' },
  { icon: ShieldCheck, label: 'Groups & roles', match: ['/admin/groups'], to: '/admin/groups' },
  { icon: KeyRound, label: 'Access tokens', match: ['/admin/tokens'], to: '/admin/tokens' },
];

export function Sidebar() {
  const pathname = useRouterState({ select: (state) => state.location.pathname });

  return (
    <aside className="flex h-screen flex-col border-r border-stroke-subtle bg-bg-container px-3 py-4">
      <div className="flex items-center gap-3 px-2 pb-6 pt-1">
        <LogoMark />
        <div className="min-w-0">
          <div className="truncate text-[14px] font-semibold text-fg-heading">GroundControl</div>
          <div className="text-[11.5px] text-fg-caption">Admin · v1.4.0</div>
        </div>
      </div>

      <nav className="flex flex-1 flex-col gap-1">
        {primaryNavItems.map((item) => (
          <NavLink active={isActive(pathname, item.match)} item={item} key={item.label} />
        ))}

        <div className="mt-5 px-3 pb-1 pt-2 text-[11px] font-medium uppercase text-fg-caption">Admin</div>
        {adminNavItems.map((item) => (
          <NavLink active={isActive(pathname, item.match)} item={item} key={item.label} />
        ))}
      </nav>

      <div className="mt-5 rounded-lg border border-stroke-subtle bg-bg-surface px-3 py-3">
        <div className="flex items-center gap-2 text-[12.5px] font-medium text-fg-heading">
          <StatusDot pulse status="live" />
          <span>All systems nominal</span>
        </div>
        <div className="mt-1 pl-4 text-[11.5px] text-fg-caption">3 replicas · rs0 healthy</div>
      </div>
    </aside>
  );
}

function NavLink({ active, item }: { active: boolean; item: NavItem }) {
  const Icon = item.icon;

  return (
    <Link
      className={`relative flex h-9 items-center gap-3 rounded-lg px-3 text-[13px] transition-colors ${
        active ? 'bg-bg-surface font-semibold text-fg-heading' : 'text-fg-body hover:bg-bg-surface hover:text-fg-heading'
      }`}
      to={item.to}
    >
      <span className={`absolute left-0 top-1/2 h-5 w-0.5 -translate-y-1/2 rounded-full ${active ? 'bg-stroke-field-focus' : 'bg-transparent'}`} />
      <Icon aria-hidden="true" className={active ? 'size-4 text-stroke-field-focus' : 'size-4 text-fg-icon-subtle'} strokeWidth={1.8} />
      <span className="truncate">{item.label}</span>
    </Link>
  );
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

function isActive(pathname: string, matches: string[]): boolean {
  return matches.some((match) => pathname === match || pathname.startsWith(match));
}