import { beforeEach, describe, expect, it, vi } from 'vitest';
import { toast } from 'sonner';
import { ApiError } from '@/api/client';
import { addNotification } from '@/lib/notifications';
import { showApiErrorToast } from './api-error-toast';

vi.mock('sonner', () => ({
  toast: { error: vi.fn() },
}));

vi.mock('@/lib/notifications', () => ({
  addNotification: vi.fn(),
}));

describe('showApiErrorToast', () => {
  beforeEach(() => {
    vi.mocked(toast.error).mockReset();
    vi.mocked(addNotification).mockReset();
  });

  it('shows a network error toast for non-ApiError failures', () => {
    showApiErrorToast(new TypeError('Failed to fetch'));

    expect(toast.error).toHaveBeenCalledWith(
      "Can't reach the server",
      expect.objectContaining({ description: 'Check your connection and try again.' }),
    );
  });

  it('skips 412 because ConflictToast handles it', () => {
    showApiErrorToast(new ApiError(412, { title: 'Conflict' }, 'v2'));

    expect(toast.error).not.toHaveBeenCalled();
    expect(addNotification).not.toHaveBeenCalled();
  });

  it('records a notification with the bucket for every error it surfaces', () => {
    showApiErrorToast(new ApiError(404, undefined));

    expect(addNotification).toHaveBeenCalledWith(expect.objectContaining({
      bucket: 'neutral',
      description: 'This resource was deleted or moved.',
      status: 404,
      title: 'Not found',
    }));
  });

  it('records network failures as critical without a status', () => {
    showApiErrorToast(new TypeError('Failed to fetch'));

    expect(addNotification).toHaveBeenCalledWith(expect.objectContaining({
      bucket: 'critical',
      status: undefined,
      title: "Can't reach the server",
    }));
  });

  it.each([
    [500, 'critical'],
    [503, 'critical'],
    [401, 'info'],
    [403, 'info'],
    [404, 'neutral'],
    [400, 'warning'],
    [409, 'warning'],
    [422, 'warning'],
    [428, 'warning'],
  ])('applies the %s bucket stripe style to the toast', (status, bucket) => {
    showApiErrorToast(new ApiError(status, undefined));

    expect(toast.error).toHaveBeenCalledWith(
      expect.anything(),
      expect.objectContaining({
        style: expect.objectContaining({
          borderLeftColor: `var(--tower-badge-${bucket}-fg)`,
          borderLeftWidth: '3px',
        }),
      }),
    );
  });

  it('uses status template title and falls back to template description without ProblemDetails', () => {
    showApiErrorToast(new ApiError(404, undefined));

    expect(toast.error).toHaveBeenCalledWith(
      'Not found',
      expect.objectContaining({ description: 'This resource was deleted or moved.' }),
    );
  });

  it('promotes problem.detail to the description when present', () => {
    showApiErrorToast(new ApiError(409, { title: 'Conflict', detail: 'Template has 3 attached projects.' }));

    expect(toast.error).toHaveBeenCalledWith(
      'Conflict',
      expect.objectContaining({ description: 'Template has 3 attached projects.' }),
    );
  });

  it('renders validation errors from HttpValidationProblemDetails', () => {
    const error = new ApiError(400, {
      title: 'Bad Request',
      errors: { key: ['Key is required.'], value: ['Value too long.'] },
    });

    showApiErrorToast(error);

    expect(toast.error).toHaveBeenCalledWith(
      "Couldn't save changes",
      expect.objectContaining({ description: 'key: Key is required.\nvalue: Value too long.' }),
    );
  });

  it('marks 401 as sticky', () => {
    showApiErrorToast(new ApiError(401, undefined));

    expect(toast.error).toHaveBeenCalledWith(
      'Sign in again',
      expect.objectContaining({ duration: Number.POSITIVE_INFINITY }),
    );
  });

  it('falls back to a generic 5xx template', () => {
    showApiErrorToast(new ApiError(503, 'gateway down'));

    expect(toast.error).toHaveBeenCalledWith(
      'Server error',
      expect.objectContaining({ description: "We couldn't complete that. Please try again shortly." }),
    );
  });

  it('namespaces the toast id by dedupe key so concurrent mutations do not collapse', () => {
    showApiErrorToast(new ApiError(500, undefined), 'config-entries:create');

    expect(toast.error).toHaveBeenCalledWith(
      expect.anything(),
      expect.objectContaining({ id: 'api-error:config-entries:create:500' }),
    );
  });
});
