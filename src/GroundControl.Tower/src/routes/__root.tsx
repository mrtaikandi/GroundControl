import { DriftBanner } from '@/components/tower/feedback/DriftBanner';
import { AppShell } from '@/components/tower/shell/AppShell';
import { TweaksPanel } from '@/components/tower/shell/TweaksPanel';
import { liveActivityAtom } from '@/lib/atoms';
import { SensitiveProvider } from '@/lib/sensitive';
import { useLiveActivity } from '@/lib/sse';
import { ReactQueryDevtools } from '@tanstack/react-query-devtools';
import { createRootRoute, Outlet } from '@tanstack/react-router';
import { useSetAtom } from 'jotai';
import { useEffect } from 'react';
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
  const liveActivity = useLiveActivity();
  const setLiveActivity = useSetAtom(liveActivityAtom);

  useEffect(() => {
    setLiveActivity(liveActivity);
  }, [liveActivity, setLiveActivity]);

  return (
    <SensitiveProvider>
      <AppShell>
        <DriftBanner />
        <Outlet />
      </AppShell>
      <TweaksPanel />
      <Toaster position="bottom-right" richColors={false} />
      <ReactQueryDevtools buttonPosition="bottom-left" initialIsOpen={false} />
    </SensitiveProvider>
  );
}
