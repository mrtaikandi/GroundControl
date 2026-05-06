import { useMutation, useQuery } from '@tanstack/react-query';
import { createToken, getTokens, revokeToken } from '@/api/endpoints/tokens';
import type { ApiRequestBody, ApiResponse } from '@/api/client';
import { queryClient } from '@/lib/query-client';

export type Token = ApiResponse<'ListPatsHandler'>[number];
export type CreateTokenRequest = ApiRequestBody<'CreatePatHandler'>;

export function tokensQueryKey() {
  return ['tokens'] as const;
}

export function useTokens() {
  return useQuery({
    queryFn: getTokens,
    queryKey: tokensQueryKey(),
  });
}

export function useCreateToken(onCreated: (rawToken: string) => void) {
  return useMutation({
    mutationFn: async (body: CreateTokenRequest) => {
      const response = await createToken(body);

      onCreated(response.token);
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: tokensQueryKey() }),
  });
}

export function useRevokeToken() {
  return useMutation({
    mutationFn: (id: string) => revokeToken(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: tokensQueryKey() }),
  });
}