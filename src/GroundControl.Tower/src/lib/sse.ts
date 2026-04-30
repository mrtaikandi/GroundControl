import { fetchEventSource } from '@microsoft/fetch-event-source';
import { useEffect, useRef, useState } from 'react';
import { getAccessToken, type ApiResponse } from '@/api/client';
import { env } from './env';

export interface LiveActivity {
  clientCount: number;
  eventsPerSecond: number;
  isConnected: boolean;
  lastEventAt: Date | null;
}

interface ActivityPayload {
  clients?: number;
  rate?: number;
}

export type LiveAuditRecord = NonNullable<ApiResponse<'ListAuditRecordsHandler'>>['data'][number];

interface UseLiveActivityOptions {
  onAuditRecord?: (record: LiveAuditRecord) => void;
}

const initialActivity: LiveActivity = {
  clientCount: 0,
  eventsPerSecond: 0,
  isConnected: false,
  lastEventAt: null,
};

// Must be called exactly once at the root. Use liveActivityAtom to read from child components.
export function useLiveActivity(options: UseLiveActivityOptions = {}): LiveActivity {
  const [activity, setActivity] = useState<LiveActivity>(initialActivity);
  const eventTimestamps = useRef<number[]>([]);
  const onAuditRecord = useRef(options.onAuditRecord);

  useEffect(() => {
    onAuditRecord.current = options.onAuditRecord;
  }, [options.onAuditRecord]);

  useEffect(() => {
    if (!supportsSse()) {
      return startPolling(setActivity);
    }

    const controller = new AbortController();

    void fetchEventSource(buildUrl('/activity/stream'), {
      headers: createHeaders(),
      onclose: () => setActivity((current) => ({ ...current, isConnected: false })),
      onerror: () => {
        setActivity((current) => ({ ...current, isConnected: false }));
      },
      onmessage: (message) => {
        if (message.event === 'audit_record') {
          const record = parseJson<LiveAuditRecord>(message.data);

          if (record) {
            onAuditRecord.current?.(record);
            setActivity((current) => ({ ...current, isConnected: true, lastEventAt: new Date() }));
          }

          return;
        }

        if (message.event !== 'activity') {
          return;
        }

        const payload = parseJson<ActivityPayload>(message.data) ?? {};
        const now = Date.now();
        eventTimestamps.current = [...eventTimestamps.current, now].slice(-10);
        const computedRate = computeEventsPerSecond(eventTimestamps.current);

        setActivity({
          clientCount: payload.clients ?? 0,
          eventsPerSecond: computedRate || payload.rate || 0,
          isConnected: true,
          lastEventAt: new Date(now),
        });
      },
      onopen: async (response) => {
        if (response.status === 401 || response.status === 403) {
          controller.abort();
          setActivity((current) => ({ ...current, isConnected: false }));

          return;
        }

        if (!response.ok) {
          throw new Error(`Live activity stream failed with status ${response.status}`);
        }

        setActivity((current) => ({ ...current, isConnected: true }));
      },
      signal: controller.signal,
    }).catch(() => {
      if (!controller.signal.aborted) {
        setActivity((current) => ({ ...current, isConnected: false }));
      }
    });

    return () => controller.abort();
  }, []);

  return activity;
}

function supportsSse() {
  return typeof ReadableStream !== 'undefined';
}

function startPolling(setActivity: (value: LiveActivity | ((current: LiveActivity) => LiveActivity)) => void) {
  let disposed = false;

  async function poll() {
    try {
      const response = await fetch(buildUrl('/activity/summary'), { headers: createHeaders() });

      if (!response.ok) {
        throw new Error(`Live activity summary failed with status ${response.status}`);
      }

      const payload = await response.json() as ActivityPayload;

      if (!disposed) {
        setActivity({
          clientCount: payload.clients ?? 0,
          eventsPerSecond: payload.rate ?? 0,
          isConnected: true,
          lastEventAt: new Date(),
        });
      }
    } catch {
      if (!disposed) {
        setActivity((current) => ({ ...current, isConnected: false }));
      }
    }
  }

  void poll();
  const interval = window.setInterval(() => void poll(), 30_000);

  return () => {
    disposed = true;
    window.clearInterval(interval);
  };
}

function parseJson<T>(data: string): T | null {
  try {
    return JSON.parse(data) as T;
  } catch {
    return null;
  }
}

function computeEventsPerSecond(timestamps: number[]) {
  if (timestamps.length < 2) {
    return 0;
  }

  const oldest = timestamps[0]!;
  const newest = timestamps[timestamps.length - 1]!;
  const elapsed = newest - oldest;

  return elapsed > 0 ? (timestamps.length / elapsed) * 1000 : 0;
}

function buildUrl(path: string) {
  const baseUrl = env.apiBaseUrl.replace(/\/$/, '');

  return `${baseUrl}${path.startsWith('/') ? path : `/${path}`}`;
}

function createHeaders() {
  const headers: Record<string, string> = { 'api-version': '1.0' };
  const token = getAccessToken();

  if (token) {
    headers.Authorization = `Bearer ${token}`;
  }

  return headers;
}