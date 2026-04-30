import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { act, renderHook, waitFor } from '@testing-library/react';
import type { ReactNode } from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from '@/api/client';
import { useConflictMutation } from './mutations';
import { showConflictToast } from '@/components/tower/feedback/ConflictToast';

vi.mock('@/components/tower/feedback/ConflictToast', () => ({
  showConflictToast: vi.fn(),
}));

describe('useConflictMutation', () => {
  beforeEach(() => {
    vi.mocked(showConflictToast).mockReset();
  });

  it('shows a conflict toast and retries with the latest version', async () => {
    const mutationFn = vi.fn()
      .mockRejectedValueOnce(new ApiError(412, { title: 'Conflict' }, 'v2'))
      .mockResolvedValueOnce({ ok: true });

    const { result } = renderHook(() => useConflictMutation<{ id: string }, { ok: boolean }>(mutationFn), { wrapper: createWrapper() });

    act(() => {
      result.current.mutate({ id: 'project-1', version: 'v1' });
    });

    await waitFor(() => expect(showConflictToast).toHaveBeenCalledOnce());

    const toastArgs = vi.mocked(showConflictToast).mock.calls[0]![0];
    expect(toastArgs.latestVersion).toBe('v2');

    act(() => {
      toastArgs.retryWithLatest?.('v2');
    });

    await waitFor(() => expect(mutationFn).toHaveBeenCalledTimes(2));
    expect(mutationFn).toHaveBeenLastCalledWith({ id: 'project-1', version: 'v2' });
  });
});

function createWrapper() {
  const client = new QueryClient({ defaultOptions: { mutations: { retry: false }, queries: { retry: false } } });

  return function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={client}>{children}</QueryClientProvider>;
  };
}