import { DropdownMenu, DropdownMenuContent, DropdownMenuLabel, DropdownMenuRadioGroup, DropdownMenuRadioItem, DropdownMenuSeparator, DropdownMenuTrigger } from '@/components/ui/dropdown-menu';
import { NotificationsPopover } from '@/components/tower/shell/NotificationsPopover';
import { Button } from '@/components/ui/button';
import { SYSTEM_USER_LABEL } from '@/lib/user';
import { cn } from '@/lib/utils';
import { useTweaksStore, type Theme } from '@/store/tweaks';
import { Link, useRouterState } from '@tanstack/react-router';
import { Activity, CircleGauge, FolderKanban, KeyRound, Layers3, ListTree, MonitorSmartphone, Moon, ShieldCheck, Sun, Users, Variable } from 'lucide-react';
import type { ComponentType } from 'react';

type NavRoute = '/admin/groups' | '/admin/tokens' | '/admin/users' | '/audit' | '/clients' | '/overview' | '/projects' | '/scopes' | '/templates' | '/variables';

interface NavItem {
  exact?: boolean;
  icon: ComponentType<{ className?: string; strokeWidth?: number }>;
  label: string;
  match: string[];
  to: NavRoute;
}

interface SidebarProps {
  className?: string;
  onNavigate?: () => void;
}

const adminNavItems: NavItem[] = [
  { icon: Users, label: 'Users', match: ['/admin/users'], to: '/admin/users' },
  { icon: ShieldCheck, label: 'Groups & roles', match: ['/admin/groups'], to: '/admin/groups' },
  { icon: KeyRound, label: 'Access tokens', match: ['/admin/tokens'], to: '/admin/tokens' },
];

const primaryNavItems: NavItem[] = [
  { icon: CircleGauge, label: 'Overview', match: ['/overview'], to: '/overview' },
  { icon: FolderKanban, label: 'Projects', match: ['/projects'], to: '/projects' },
  { icon: MonitorSmartphone, label: 'Clients', match: ['/clients'], to: '/clients' },
  { icon: Layers3, label: 'Scopes', match: ['/scopes'], to: '/scopes' },
  { icon: Variable, label: 'Variables', match: ['/variables'], to: '/variables' },
  { icon: ListTree, label: 'Templates', match: ['/templates'], to: '/templates' },
  { icon: Activity, label: 'Audit trail', match: ['/audit'], to: '/audit' },
];

export function Sidebar({ className, onNavigate }: SidebarProps) {
  const pathname = useRouterState({ select: (state) => state.location.pathname });

  return (
    <aside className={cn('flex h-full min-h-0 flex-col overflow-y-auto border-r border-stroke-divider bg-bg-page px-3 py-4', className)}>
      <div className="flex items-center gap-3 px-2 pb-5 pt-1">
        <LogoMark />
        <div className="min-w-0">
          <div className="truncate text-[14px] font-semibold text-fg-heading">Control Tower</div>
          <div className="text-[11.5px] text-fg-caption">GroundControl</div>
        </div>
      </div>

      <nav className="flex flex-1 flex-col gap-1 pt-4">
        {primaryNavItems.map((item) => (
          <NavLink active={isActive(pathname, item.match, item.exact)} item={item} key={item.label} onNavigate={onNavigate} />
        ))}

        <div className="mt-6 px-3 pb-1 pt-4 text-[11px] font-medium uppercase text-fg-caption">Admin</div>
        {adminNavItems.map((item) => (
          <NavLink active={isActive(pathname, item.match, item.exact)} item={item} key={item.label} onNavigate={onNavigate} />
        ))}
      </nav>

      <div className="mt-auto flex items-center gap-2 px-2 pt-4">
        <AccountMenu />
        <NotificationsPopover />
      </div>
    </aside>
  );
}

export function AccountMenu() {
  const theme = useTweaksStore((state) => state.theme);
  const setTheme = useTweaksStore((state) => state.setTheme);
  const userName = SYSTEM_USER_LABEL;
  const userInitial = userName.charAt(0).toUpperCase();

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button
          aria-label={`Account menu for ${userName}`}
          className="size-11 rounded-full bg-bg-chip-selected text-[12px] font-semibold text-fg-chip-selected hover:bg-bg-chip-selected/90 hover:text-fg-chip-selected"
          size={null}
          variant="ghost"
        >
          {userInitial}
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" className="min-w-52">
        <DropdownMenuLabel>
          <div className="text-[12.5px] font-medium text-fg-heading">{userName}</div>
          <div className="text-[11px] font-normal text-fg-caption">Signed in</div>
        </DropdownMenuLabel>
        <DropdownMenuSeparator />
        <DropdownMenuLabel>Theme</DropdownMenuLabel>
        <DropdownMenuRadioGroup onValueChange={(value) => setTheme(value as Theme)} value={theme}>
          <DropdownMenuRadioItem value="light">
            <Sun aria-hidden="true" className="size-4 text-fg-icon-subtle" strokeWidth={1.8} />
            Light
          </DropdownMenuRadioItem>
          <DropdownMenuRadioItem value="dark">
            <Moon aria-hidden="true" className="size-4 text-fg-icon-subtle" strokeWidth={1.8} />
            Dark
          </DropdownMenuRadioItem>
        </DropdownMenuRadioGroup>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}

function NavLink({ active, item, onNavigate }: { active: boolean; item: NavItem; onNavigate?: () => void }) {
  const Icon = item.icon;

  return (
    <Link
      className={`relative flex min-h-11 items-center gap-3 rounded-lg px-3 text-[13px] transition-colors sm:min-h-10 lg:min-h-9 ${
        active ? 'bg-bg-surface font-semibold text-fg-heading shadow-ui-button-subtle' : 'text-fg-body hover:bg-bg-surface hover:text-fg-heading'
      }`}
      onClick={onNavigate}
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

function isActive(pathname: string, matches: string[], exact?: boolean): boolean {
  if (exact) {
    return matches.some((match) => pathname === match || pathname === `${match}/`);
  }

  return matches.some((match) => pathname === match || pathname.startsWith(`${match}/`));
}
