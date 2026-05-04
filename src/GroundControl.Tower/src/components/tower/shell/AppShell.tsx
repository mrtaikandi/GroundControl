import type { ReactNode } from 'react';
import { Sidebar } from './Sidebar';

interface AppShellProps {
  children: ReactNode;
}

export function AppShell({ children }: AppShellProps) {
  return (
    <div className="grid h-screen grid-cols-[232px_1fr] bg-bg-page text-[13px] text-fg-body">
      <Sidebar />
      <div className="h-screen min-h-0 overflow-hidden bg-bg-surface">
        <main className="min-h-0 h-full overflow-y-auto bg-bg-page [scrollbar-gutter:stable]">
          <div className="w-full py-page-v">
            {children}
          </div>
        </main>
      </div>
    </div>
  );
}