type Status = 'live' | 'warning' | 'error' | 'offline';

interface StatusDotProps {
  pulse?: boolean;
  status: Status;
}

const statusClassNames: Record<Status, string> = {
  live: 'text-badge-success-fg',
  warning: 'text-badge-warning-fg',
  error: 'text-badge-critical-fg',
  offline: 'text-fg-icon-subtle',
};

export function StatusDot({ pulse = false, status }: StatusDotProps) {
  return (
    <span
      aria-hidden="true"
      className={`tower-status-dot relative inline-block size-2 shrink-0 rounded-full bg-current ${statusClassNames[status]}`}
      data-pulse={pulse ? 'true' : 'false'}
    />
  );
}