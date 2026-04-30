import type { ReactNode } from 'react';
import { Header } from './Header';
import { Sidebar } from './Sidebar';

interface AppShellProps {
  children: ReactNode;
}

export function AppShell({ children }: AppShellProps) {
  return (
    <div className="grid min-h-screen grid-cols-[232px_1fr] bg-bg-page text-[13px] text-fg-body">
      <Sidebar />
      <div className="grid min-h-screen grid-rows-[56px_1fr] overflow-hidden">
        <Header />
        <main className="overflow-y-auto bg-bg-page">
          <div className="mx-auto w-full max-w-[1280px] px-page-h py-page-v">
            {children}
          </div>
        </main>
      </div>
    </div>
  );
}