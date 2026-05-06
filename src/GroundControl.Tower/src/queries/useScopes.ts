import { useMutation, useQuery } from '@tanstack/react-query';
import { createScope, deleteScope, getScopes, updateScope } from '@/api/endpoints/scopes';
import type { ApiRequestBody, ApiResponse } from '@/api/client';
import { useConflictMutation } from '@/lib/mutations';
import { queryClient } from '@/lib/query-client';

export type Scope = NonNullable<ApiResponse<'ListScopesHandler'>>['data'][number];
export type CreateScopeRequest = ApiRequestBody<'CreateScopeHandler'>;
export type UpdateScopeRequest = ApiRequestBody<'UpdateScopeHandler'>;

export const scopesQueryKey = ['scopes'] as const;

export function useScopes() {
  return useQuery({
    queryFn: () => getScopes({ Limit: 100, SortField: 'dimension', SortOrder: 'asc' }),
    queryKey: scopesQueryKey,
    staleTime: 30_000,
  });
}

export function useCreateScope() {
  return useMutation({
    mutationFn: (body: CreateScopeRequest) => createScope(body),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: scopesQueryKey }),
  });
}

export function useUpdateScope() {
  return useConflictMutation<{ body: UpdateScopeRequest; id: string }, Scope>(
    (variables) => updateScope(variables.id, variables.body, variables.version),
    { onSuccess: () => queryClient.invalidateQueries({ queryKey: scopesQueryKey }) },
  );
}

export function useDeleteScope() {
  return useConflictMutation<{ id: string }, void>(
    (variables) => deleteScope(variables.id, variables.version),
    { onSuccess: () => queryClient.invalidateQueries({ queryKey: scopesQueryKey }) },
  );
}