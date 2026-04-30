import { QueryClient } from '@tanstack/react-query';
import { ApiError } from '@/api/client';

export const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      retry: (failureCount, error) => error instanceof ApiError && error.status >= 500 && failureCount < 3,
      staleTime: 30_000,
    },
  },
});