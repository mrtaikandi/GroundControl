import type { CSSProperties } from 'react';

export type ErrorBucket = 'critical' | 'info' | 'neutral' | 'warning';

export function bucketForStatus(status: number | undefined): ErrorBucket {
  if (status === undefined) {
    return 'critical';
  }

  if (status >= 500) {
    return 'critical';
  }

  if (status === 401 || status === 403) {
    return 'info';
  }

  if (status === 404) {
    return 'neutral';
  }

  return 'warning';
}

export const BUCKET_STRIPE_STYLE: Record<ErrorBucket, CSSProperties> = {
  critical: { borderLeftColor: 'var(--tower-badge-critical-fg)', borderLeftStyle: 'solid', borderLeftWidth: '3px' },
  info: { borderLeftColor: 'var(--tower-badge-info-fg)', borderLeftStyle: 'solid', borderLeftWidth: '3px' },
  neutral: { borderLeftColor: 'var(--tower-badge-neutral-fg)', borderLeftStyle: 'solid', borderLeftWidth: '3px' },
  warning: { borderLeftColor: 'var(--tower-badge-warning-fg)', borderLeftStyle: 'solid', borderLeftWidth: '3px' },
};
