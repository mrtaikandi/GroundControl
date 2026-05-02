import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { getDefaultStore } from 'jotai';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { addNotification, clearAllNotifications, lastSeenAtAtom, notificationsAtom } from '@/lib/notifications';
import { NotificationsPopover } from './NotificationsPopover';

const store = getDefaultStore();

describe('NotificationsPopover', () => {
  beforeEach(() => {
    store.set(notificationsAtom, []);
    store.set(lastSeenAtAtom, 0);
  });

  afterEach(() => {
    clearAllNotifications();
  });

  it('shows no badge when there are no unread notifications', () => {
    render(<NotificationsPopover />);

    expect(screen.getByRole('button', { name: 'Notifications' })).toBeInTheDocument();
  });

  it('shows the unread count on the bell and caps display at 9+', () => {
    for (let i = 0; i < 12; i += 1) {
      addNotification({ bucket: 'critical', description: 'x', status: 500, title: `t-${i}` });
    }

    render(<NotificationsPopover />);

    const button = screen.getByRole('button', { name: /Notifications, 12 unread/ });
    expect(within(button).getByText('9+')).toBeInTheDocument();
  });

  it('renders empty state when opened with no notifications', async () => {
    const user = userEvent.setup();
    render(<NotificationsPopover />);

    await user.click(screen.getByRole('button', { name: 'Notifications' }));

    expect(await screen.findByText("You're all caught up.")).toBeInTheDocument();
  });

  it('opening the panel resets the unread count', async () => {
    const user = userEvent.setup();
    addNotification({ bucket: 'critical', description: 'd', status: 500, title: 'Boom' });
    render(<NotificationsPopover />);

    expect(screen.getByRole('button', { name: /1 unread/ })).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: /1 unread/ }));

    expect(await screen.findByRole('button', { name: 'Notifications' })).toBeInTheDocument();
  });

  it('dismisses an individual notification', async () => {
    const user = userEvent.setup();
    addNotification({ bucket: 'critical', description: 'd', status: 500, title: 'Boom' });
    render(<NotificationsPopover />);

    await user.click(screen.getByRole('button', { name: /1 unread/ }));
    await user.click(await screen.findByRole('button', { name: 'Dismiss notification' }));

    expect(screen.getByText("You're all caught up.")).toBeInTheDocument();
    expect(store.get(notificationsAtom)).toEqual([]);
  });

  it('clears all notifications', async () => {
    const user = userEvent.setup();
    addNotification({ bucket: 'critical', description: 'd1', status: 500, title: 'Boom 1' });
    addNotification({ bucket: 'neutral', description: 'd2', status: 404, title: 'Boom 2' });
    render(<NotificationsPopover />);

    await user.click(screen.getByRole('button', { name: /2 unread/ }));
    await user.click(await screen.findByRole('button', { name: 'Clear all' }));

    expect(screen.getByText("You're all caught up.")).toBeInTheDocument();
    expect(store.get(notificationsAtom)).toEqual([]);
  });
});
