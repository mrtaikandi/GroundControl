import { AppShell } from '@/components/tower/shell/AppShell';
import { TweaksPanel } from '@/components/tower/shell/TweaksPanel';
import { SensitiveProvider } from '@/lib/sensitive';
import { ReactQueryDevtools } from '@tanstack/react-query-devtools';
import { createRootRoute, Outlet } from '@tanstack/react-router';
import { Toaster } from 'sonner';

export const Route = createRootRoute({
  component: RootComponent,
  head: () => ({
    meta: [
      {
        title: 'GroundControl Tower',
      },
    ],
  }),
});

function RootComponent() {
  return (
    <SensitiveProvider>
      <AppShell>
        <Outlet />
      </AppShell>
      <TweaksPanel />
      <Toaster position="bottom-right" richColors={false} />
      <ReactQueryDevtools buttonPosition="bottom-left" initialIsOpen={false} />
    </SensitiveProvider>
  );
}
