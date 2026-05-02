import { MutationCache, QueryClient } from '@tanstack/react-query';
import { ApiError } from '@/api/client';
import { showApiErrorToast } from '@/lib/api-error-toast';

const mutationCache = new MutationCache({
  onError: (error, _variables, _context, mutation) => {
    const meta = mutation.meta as { skipErrorToast?: boolean } | undefined;
    if (meta?.skipErrorToast) {
      return;
    }

    const dedupeKey = mutation.options.mutationKey?.join(':');
    showApiErrorToast(error, dedupeKey);
  },
});

export const queryClient = new QueryClient({
  mutationCache,
  defaultOptions: {
    queries: {
      retry: (failureCount, error) => error instanceof ApiError && error.status >= 500 && failureCount < 3,
      staleTime: 30_000,
    },
  },
});