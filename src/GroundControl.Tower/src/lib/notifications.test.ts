import { getDefaultStore } from 'jotai';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { addNotification, clearAllNotifications, dismissNotification, lastSeenAtAtom, markAllNotificationsRead, notificationsAtom, unreadCountAtom } from './notifications';

const store = getDefaultStore();

describe('notifications store', () => {
  beforeEach(() => {
    store.set(notificationsAtom, []);
    store.set(lastSeenAtAtom, 0);
    vi.useRealTimers();
  });

  it('prepends new notifications and assigns id and timestamp', () => {
    addNotification({ bucket: 'critical', description: 'first', status: 500, title: 'a' });
    addNotification({ bucket: 'neutral', description: 'second', status: 404, title: 'b' });

    const list = store.get(notificationsAtom);
    expect(list.map((n) => n.title)).toEqual(['b', 'a']);
    expect(list[0]!.id).not.toBe(list[1]!.id);
    expect(list[0]!.createdAt).toBeGreaterThan(0);
  });

  it('caps the list at 50 entries', () => {
    for (let i = 0; i < 60; i += 1) {
      addNotification({ bucket: 'critical', description: 'x', title: `t-${i}` });
    }

    const list = store.get(notificationsAtom);
    expect(list).toHaveLength(50);
    expect(list[0]!.title).toBe('t-59');
    expect(list[49]!.title).toBe('t-10');
  });

  it('dismissNotification removes the entry by id', () => {
    addNotification({ bucket: 'critical', description: 'x', title: 'one' });
    addNotification({ bucket: 'critical', description: 'y', title: 'two' });
    const [first, second] = store.get(notificationsAtom);

    dismissNotification(first!.id);

    expect(store.get(notificationsAtom)).toEqual([second]);
  });

  it('clearAllNotifications empties the list', () => {
    addNotification({ bucket: 'critical', description: 'x', title: 'one' });
    addNotification({ bucket: 'critical', description: 'y', title: 'two' });

    clearAllNotifications();

    expect(store.get(notificationsAtom)).toEqual([]);
  });

  it('unreadCount reflects entries newer than lastSeenAt and resets on markAllNotificationsRead', () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date('2026-05-02T10:00:00Z'));
    addNotification({ bucket: 'critical', description: 'old', title: 'old' });

    vi.setSystemTime(new Date('2026-05-02T10:00:05Z'));
    markAllNotificationsRead();
    expect(store.get(unreadCountAtom)).toBe(0);

    vi.setSystemTime(new Date('2026-05-02T10:00:10Z'));
    addNotification({ bucket: 'critical', description: 'new1', title: 'new1' });
    addNotification({ bucket: 'critical', description: 'new2', title: 'new2' });
    expect(store.get(unreadCountAtom)).toBe(2);

    vi.setSystemTime(new Date('2026-05-02T10:00:15Z'));
    markAllNotificationsRead();
    expect(store.get(unreadCountAtom)).toBe(0);
  });
});
