import { atom } from 'jotai';
import type { LiveActivity, LiveAuditRecord } from './sse';

export const liveActivityAtom = atom<LiveActivity>({
  clientCount: 0,
  eventsPerSecond: 0,
  isConnected: false,
  lastEventAt: null,
});

export const liveAuditRecordsAtom = atom<LiveAuditRecord[]>([]);