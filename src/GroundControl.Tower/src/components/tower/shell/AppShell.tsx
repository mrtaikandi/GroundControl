import type { ReactNode } from 'react';
import { Menu } from 'lucide-react';
import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { Sheet, SheetContent, SheetTitle, SheetTrigger } from '@/components/ui/sheet';
import { NotificationsPopover } from './NotificationsPopover';
import { AccountMenu, Sidebar } from './Sidebar';

interface AppShellProps {
  children: ReactNode;
}

export function AppShell({ children }: AppShellProps) {
  const [mobileNavigationOpen, setMobileNavigationOpen] = useState(false);

  return (
    <div className="grid min-h-dvh bg-bg-page text-[13px] text-fg-body lg:h-screen lg:grid-cols-[232px_1fr]">
      <Sidebar className="hidden lg:flex" />
      <div className="min-h-0 overflow-hidden bg-bg-surface">
        <div className="flex items-center justify-between gap-3 border-b border-stroke-divider bg-bg-page px-4 py-3 sm:px-6 lg:hidden">
          <div className="flex min-w-0 items-center gap-3">
            <Sheet onOpenChange={setMobileNavigationOpen} open={mobileNavigationOpen}>
              <SheetTrigger asChild>
                <Button aria-label="Open navigation" size="icon" type="button" variant="ghost">
                  <Menu aria-hidden="true" className="size-4" strokeWidth={1.8} />
                </Button>
              </SheetTrigger>
              <SheetContent className="w-[min(240px,calc(100vw-40px))] p-0 sm:w-[min(260px,calc(100vw-32px))]" side="left">
                <SheetTitle className="sr-only">Navigation</SheetTitle>
                <Sidebar className="border-r-0" onNavigate={() => setMobileNavigationOpen(false)} />
              </SheetContent>
            </Sheet>
            <div className="min-w-0">
              <div className="truncate text-[14px] font-semibold text-fg-heading">Control Tower</div>
              <div className="text-[11.5px] text-fg-caption">GroundControl</div>
            </div>
          </div>
          <div className="flex items-center gap-2">
            <NotificationsPopover />
            <AccountMenu />
          </div>
        </div>
        <main className="h-full min-h-0 overflow-y-auto bg-bg-page [scrollbar-gutter:stable]">
          <div className="w-full py-6 lg:py-page-v">
            {children}
          </div>
        </main>
      </div>
    </div>
  );
}