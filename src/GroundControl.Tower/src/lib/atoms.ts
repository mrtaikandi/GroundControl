import { atom } from 'jotai';
import type { LiveActivity } from './sse';

export const liveActivityAtom = atom<LiveActivity>({
  clientCount: 0,
  eventsPerSecond: 0,
  isConnected: false,
  lastEventAt: null,
});