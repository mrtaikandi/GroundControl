import { AppShell } from '@/components/tower/shell/AppShell';
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
    <AppShell>
      <Outlet />
    </AppShell>
  );
}
