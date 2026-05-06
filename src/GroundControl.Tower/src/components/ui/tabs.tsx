import { Link, useRouterState } from '@tanstack/react-router';
import type { ReactNode } from 'react';
import type { ComponentType } from 'react';
import { PageContent } from '@/components/tower/shell/PageContent';
import { cn } from '@/lib/utils';

type TabIcon = ComponentType<{ className?: string; strokeWidth?: number }>;

export interface TabsItem {
  exact?: boolean;
  icon?: TabIcon;
  key?: string;
  label: ReactNode;
  match?: string | ((pathname: string) => boolean);
  params?: Record<string, number | string | undefined>;
  to: string;
  trailing?: ReactNode;
}

interface TabsProps {
  activeTabClassName?: string;
  ariaLabel: string;
  containerClassName?: string;
  contentClassName?: string;
  inactiveTabClassName?: string;
  items: TabsItem[];
  tabClassName?: string;
  tabsClassName?: string;
  usePageContent?: boolean;
}

export function Tabs({
  activeTabClassName,
  ariaLabel,
  containerClassName,
  contentClassName,
  inactiveTabClassName,
  items,
  tabClassName,
  tabsClassName,
  usePageContent = true,
}: TabsProps) {
  const pathname = useRouterState({ select: (state) => state.location.pathname });

  const nav = (
    <nav aria-label={ariaLabel} className={cn('flex flex-wrap gap-1', tabsClassName)}>
      {items.map((item) => {
        const Icon = item.icon;
        const active = isTabActive(item, pathname);

        return (
          <Link
            className={cn(
              '-mb-px inline-flex items-center gap-2 border-b-2 px-3 py-3 text-[13px] transition-colors',
              active ? 'border-stroke-field-focus text-fg-heading' : 'border-transparent text-fg-caption hover:text-fg-body',
              active ? activeTabClassName : inactiveTabClassName,
              tabClassName,
            )}
            key={item.key ?? item.to}
            params={item.params as never}
            to={item.to as never}
          >
            {Icon ? <Icon aria-hidden="true" className={active ? 'size-4 text-stroke-field-focus' : 'size-4 text-fg-icon-subtle'} strokeWidth={1.8} /> : null}
            <span className={active ? 'font-semibold' : undefined}>{item.label}</span>
            {item.trailing}
          </Link>
        );
      })}
    </nav>
  );

  return (
    <div className={cn('border-b border-stroke-divider', containerClassName)}>
      {usePageContent ? <PageContent className={contentClassName}>{nav}</PageContent> : nav}
    </div>
  );
}

function isTabActive(item: TabsItem, pathname: string) {
  if (typeof item.match === 'function') {
    return item.match(pathname);
  }

  const target = item.match ?? resolveTabPath(item.to, item.params);

  if (item.exact) {
    return pathname === target || pathname === `${target.replace(/\/$/, '')}`;
  }

  return pathname === target || pathname.startsWith(`${target}/`);
}

function resolveTabPath(pathTemplate: string, params: TabsItem['params']) {
  if (!params) {
    return pathTemplate;
  }

  return pathTemplate.replace(/\$([A-Za-z0-9_]+)/g, (_, key: string) => String(params[key] ?? `$${key}`));
}