import type { ReactNode } from 'react';
import { Header } from './Header';
import { Sidebar } from './Sidebar';

interface AppShellProps {
  children: ReactNode;
}

export function AppShell({ children }: AppShellProps) {
  return (
    <div className="grid h-screen grid-cols-[232px_1fr] bg-bg-page text-[13px] text-fg-body">
      <Sidebar />
      <div className="grid h-screen min-h-0 grid-rows-[56px_1fr] overflow-hidden">
        <Header />
        <main className="min-h-0 overflow-y-auto bg-bg-page">
          <div className="w-full px-page-h py-page-v 2xl:px-16">
            {children}
          </div>
        </main>
      </div>
    </div>
  );
}