import { useAtomValue } from 'jotai';
import { Bell, X } from 'lucide-react';
import { useState } from 'react';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { Badge } from '@/components/tower/data/Badge';
import { Button } from '@/components/ui/button';
import { clearAllNotifications, dismissNotification, markAllNotificationsRead, notificationsAtom, unreadCountAtom, type ErrorNotification } from '@/lib/notifications';

export function NotificationsPopover() {
  const notifications = useAtomValue(notificationsAtom);
  const unreadCount = useAtomValue(unreadCountAtom);
  const [open, setOpen] = useState(false);
  const handleOpenChange = (next: boolean) => {
    setOpen(next);

    if (next) {
      markAllNotificationsRead();
    }
  };
  const ariaLabel = unreadCount > 0 ? `Notifications, ${unreadCount} unread` : 'Notifications';

  return (
    <Popover onOpenChange={handleOpenChange} open={open}>
      <PopoverTrigger asChild>
        <Button
          aria-label={ariaLabel}
          className="relative size-11 text-fg-icon-subtle"
          size={null}
          variant="ghost"
          type="button"
        >
          <Bell aria-hidden="true" className="size-4" strokeWidth={1.8} />
          {unreadCount > 0 ? (
            <span
              aria-hidden="true"
              className="absolute -right-0.5 -top-0.5 grid h-4 min-w-4 place-items-center rounded-full bg-badge-critical-fg px-1 text-[10px] font-semibold leading-none text-bg-surface"
            >
              {unreadCount > 9 ? '9+' : unreadCount}
            </span>
          ) : null}
        </Button>
      </PopoverTrigger>
      <PopoverContent align="end" className="w-[min(360px,calc(100vw-24px))] p-0 sm:w-[min(360px,calc(100vw-32px))]">
        <div className="flex items-center justify-between border-b border-stroke-subtle px-3 py-2">
          <span className="text-[12px] font-semibold uppercase tracking-wider text-fg-heading">Notifications</span>
          {notifications.length > 0 ? (
            <button
              className="text-[12px] text-fg-caption transition-colors hover:text-fg-body"
              onClick={clearAllNotifications}
              type="button"
            >
              Clear all
            </button>
          ) : null}
        </div>
        {notifications.length === 0 ? (
          <div className="px-4 py-10 text-center text-[12.5px] text-fg-caption">
            You&apos;re all caught up.
          </div>
        ) : (
          <ul className="max-h-[360px] overflow-y-auto" role="list">
            {notifications.map((notification) => (
              <NotificationItem key={notification.id} notification={notification} />
            ))}
          </ul>
        )}
      </PopoverContent>
    </Popover>
  );
}

interface NotificationItemProps {
  notification: ErrorNotification;
}

function NotificationItem({ notification }: NotificationItemProps) {
  return (
    <li className="flex items-start gap-2.5 border-b border-stroke-subtle px-3 py-2.5 last:border-b-0">
      <Badge className="mt-0.5 shrink-0 text-[10px]" variant={notification.bucket}>
        {badgeLabelForStatus(notification.status)}
      </Badge>
      <div className="min-w-0 flex-1">
        <div className="text-[13px] font-medium text-fg-heading [overflow-wrap:anywhere]">{notification.title}</div>
        <div className="mt-0.5 whitespace-pre-line text-[12px] text-fg-caption [overflow-wrap:anywhere]">{notification.description}</div>
        <div className="mt-1 text-[11px] text-fg-caption">{formatRelativeTime(notification.createdAt)}</div>
      </div>
      <button
        aria-label="Dismiss notification"
        className="grid size-9 shrink-0 place-items-center rounded-lg text-fg-icon-subtle transition-colors hover:bg-bg-container hover:text-fg-body"
        onClick={() => dismissNotification(notification.id)}
        type="button"
      >
        <X aria-hidden="true" className="size-3.5" strokeWidth={1.8} />
      </button>
    </li>
  );
}

function badgeLabelForStatus(status: number | undefined): string {
  return status === undefined ? 'Network' : String(status);
}

function formatRelativeTime(createdAt: number): string {
  const seconds = Math.floor((Date.now() - createdAt) / 1000);

  if (seconds < 5) {
    return 'just now';
  }

  if (seconds < 60) {
    return `${seconds}s ago`;
  }

  const minutes = Math.floor(seconds / 60);
  if (minutes < 60) {
    return `${minutes}m ago`;
  }

  const hours = Math.floor(minutes / 60);
  if (hours < 24) {
    return `${hours}h ago`;
  }

  const days = Math.floor(hours / 24);

  return `${days}d ago`;
}
