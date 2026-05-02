import { atom, getDefaultStore } from 'jotai';
import type { ErrorBucket } from '@/lib/error-buckets';

export interface ErrorNotification {
  bucket: ErrorBucket;
  createdAt: number;
  description: string;
  id: string;
  status?: number;
  title: string;
}

const MAX_NOTIFICATIONS = 50;

export const notificationsAtom = atom<ErrorNotification[]>([]);
export const lastSeenAtAtom = atom<number>(0);

export const unreadCountAtom = atom((get) => {
  const lastSeenAt = get(lastSeenAtAtom);

  return get(notificationsAtom).filter((notification) => notification.createdAt > lastSeenAt).length;
});

export function addNotification(input: Omit<ErrorNotification, 'createdAt' | 'id'>): void {
  const notification: ErrorNotification = {
    ...input,
    createdAt: Date.now(),
    id: crypto.randomUUID(),
  };

  getDefaultStore().set(notificationsAtom, (current) => {
    const next = [notification, ...current];

    return next.length > MAX_NOTIFICATIONS ? next.slice(0, MAX_NOTIFICATIONS) : next;
  });
}

export function dismissNotification(id: string): void {
  getDefaultStore().set(notificationsAtom, (current) => current.filter((notification) => notification.id !== id));
}

export function clearAllNotifications(): void {
  getDefaultStore().set(notificationsAtom, []);
}

export function markAllNotificationsRead(): void {
  getDefaultStore().set(lastSeenAtAtom, Date.now());
}
