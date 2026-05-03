import { AppShell } from '@/components/tower/shell/AppShell';
import { liveActivityAtom, liveAuditRecordsAtom } from '@/lib/atoms';
import { prependLiveAuditRecord } from '@/lib/live-audit';
import { queryClient } from '@/lib/query-client';
import { SensitiveProvider } from '@/lib/sensitive';
import { useLiveActivity, type LiveAuditRecord } from '@/lib/sse';
import { ReactQueryDevtools } from '@tanstack/react-query-devtools';
import { createRootRoute, Outlet } from '@tanstack/react-router';
import { useSetAtom } from 'jotai';
import { useCallback, useEffect, useRef } from 'react';
import { Toaster } from 'sonner';

export const Route = createRootRoute({
  component: RootComponent,
  head: () => ({
    meta: [
      {
        title: 'Control Tower - GroundControl',
      },
    ],
  }),
});

function RootComponent() {
  const setLiveActivity = useSetAtom(liveActivityAtom);
  const setLiveAuditRecords = useSetAtom(liveAuditRecordsAtom);
  const statsRefetchTimer = useRef<number | null>(null);
  const scheduleStatsRefetch = useCallback(() => {
    if (statsRefetchTimer.current !== null) {
      return;
    }

    statsRefetchTimer.current = window.setTimeout(() => {
      statsRefetchTimer.current = null;
      void queryClient.refetchQueries({ predicate: (query) => query.queryKey[0] === 'stats' });
    }, 500);
  }, []);
  const handleAuditRecord = useCallback((record: LiveAuditRecord) => {
    setLiveAuditRecords((current) => prependLiveAuditRecord(current, record));
    scheduleStatsRefetch();
  }, [scheduleStatsRefetch, setLiveAuditRecords]);
  const liveActivity = useLiveActivity({ onAuditRecord: handleAuditRecord });

  useEffect(() => () => {
    if (statsRefetchTimer.current !== null) {
      window.clearTimeout(statsRefetchTimer.current);
    }
  }, []);

  useEffect(() => {
    setLiveActivity(liveActivity);
  }, [liveActivity, setLiveActivity]);

  return (
    <SensitiveProvider>
      <AppShell>
        <Outlet />
      </AppShell>
      <Toaster position="bottom-right" richColors={false} />
      <ReactQueryDevtools buttonPosition="bottom-left" initialIsOpen={false} />
    </SensitiveProvider>
  );
}
