import { AppShell } from '@/components/tower/shell/AppShell';
import { TweaksPanel } from '@/components/tower/shell/TweaksPanel';
import { SensitiveProvider } from '@/lib/sensitive';
import { createRootRoute, Outlet } from '@tanstack/react-router';

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
    </SensitiveProvider>
  );
}
