import { toast } from 'sonner';
import { ApiError } from '@/api/client';
import { BUCKET_STRIPE_STYLE, bucketForStatus, type ErrorBucket } from '@/lib/error-buckets';
import { addNotification } from '@/lib/notifications';

interface ProblemDetails {
  detail?: string;
  errors?: Record<string, string[]>;
  title?: string;
}

interface ToastTemplate {
  description: string;
  duration?: number;
  title: string;
}

const STICKY_DURATION = Number.POSITIVE_INFINITY;
const DEFAULT_DURATION = 6_000;

const STATUS_TEMPLATES: Record<number, ToastTemplate> = {
  400: { title: "Couldn't save changes", description: 'The request was invalid.' },
  401: { title: 'Sign in again', description: 'Your session has expired.', duration: STICKY_DURATION },
  403: { title: 'Not allowed', description: "You don't have permission to do this.", duration: STICKY_DURATION },
  404: { title: 'Not found', description: 'This resource was deleted or moved.' },
  409: { title: 'Conflict', description: 'This change conflicts with existing data.' },
  422: { title: "Couldn't save changes", description: 'The request was invalid.' },
  428: { title: 'Refresh required', description: 'Reload the page and try again.' },
};

interface ErrorContent {
  bucket: ErrorBucket;
  description: string;
  duration: number;
  status?: number;
  title: string;
}

export function showApiErrorToast(error: unknown, dedupeKey?: string) {
  const content = buildErrorContent(error);
  if (!content) {
    return;
  }

  addNotification({
    bucket: content.bucket,
    description: content.description,
    status: content.status,
    title: content.title,
  });

  toast.error(content.title, {
    description: content.description,
    duration: content.duration,
    id: buildToastId(content.status === undefined ? 'network' : String(content.status), dedupeKey),
    style: BUCKET_STRIPE_STYLE[content.bucket],
  });
}

function buildErrorContent(error: unknown): ErrorContent | null {
  if (!(error instanceof ApiError)) {
    return {
      bucket: bucketForStatus(undefined),
      description: 'Check your connection and try again.',
      duration: DEFAULT_DURATION,
      title: "Can't reach the server",
    };
  }

  if (error.status === 412) {
    return null;
  }

  const problem = isProblemDetails(error.body) ? error.body : undefined;
  const template = STATUS_TEMPLATES[error.status] ?? defaultTemplateForStatus(error.status);
  const validationDescription = formatValidationErrors(problem);

  return {
    bucket: bucketForStatus(error.status),
    description: validationDescription ?? problem?.detail ?? template.description,
    duration: template.duration ?? DEFAULT_DURATION,
    status: error.status,
    title: template.title,
  };
}

function defaultTemplateForStatus(status: number): ToastTemplate {
  if (status >= 500) {
    return { title: 'Server error', description: "We couldn't complete that. Please try again shortly." };
  }

  return { title: "Couldn't complete request", description: 'Please try again.' };
}

function isProblemDetails(body: unknown): body is ProblemDetails {
  return typeof body === 'object' && body !== null;
}

function formatValidationErrors(problem: ProblemDetails | undefined): string | undefined {
  if (!problem?.errors) {
    return undefined;
  }

  const entries = Object.entries(problem.errors).slice(0, 2);
  if (entries.length === 0) {
    return undefined;
  }

  return entries
    .map(([field, messages]) => {
      const message = messages[0] ?? '';

      return field ? `${field}: ${message}` : message;
    })
    .join('\n');
}

function buildToastId(statusKey: string, dedupeKey?: string): string {
  return dedupeKey ? `api-error:${dedupeKey}:${statusKey}` : `api-error:${statusKey}`;
}
