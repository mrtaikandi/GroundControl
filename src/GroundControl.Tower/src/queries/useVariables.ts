import { useMutation, useQuery } from '@tanstack/react-query';
import { createVariable, deleteVariable, getVariables, updateVariable } from '@/api/endpoints/variables';
import type { ApiQuery, ApiRequestBody, ApiResponse } from '@/api/client';
import { useConflictMutation } from '@/lib/mutations';
import { queryClient } from '@/lib/query-client';

export type Variable = NonNullable<ApiResponse<'ListVariablesHandler'>>['data'][number];
export type CreateVariableRequest = ApiRequestBody<'CreateVariableHandler'>;
export type UpdateVariableRequest = ApiRequestBody<'UpdateVariableHandler'>;
export type VariablesQuery = ApiQuery<'ListVariablesHandler'>;

export const variablesQueryKey = ['variables'] as const;

export function useVariables(query?: VariablesQuery) {
  const request: VariablesQuery = {
    Limit: 100,
    SortField: 'name',
    SortOrder: 'asc',
    ...query,
  };

  return useQuery({
    queryFn: () => getVariables(request),
    queryKey: [...variablesQueryKey, request] as const,
  });
}

export function useCreateVariable() {
  return useMutation({
    mutationFn: (body: CreateVariableRequest) => createVariable(body),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: variablesQueryKey }),
  });
}

export function useUpdateVariable() {
  return useConflictMutation<{ body: UpdateVariableRequest; id: string }, Variable>(
    (variables) => updateVariable(variables.id, variables.body, variables.version),
    { onSuccess: () => queryClient.invalidateQueries({ queryKey: variablesQueryKey }) },
  );
}

export function useDeleteVariable() {
  return useConflictMutation<{ id: string }, void>(
    (variables) => deleteVariable(variables.id, variables.version),
    { onSuccess: () => queryClient.invalidateQueries({ queryKey: variablesQueryKey }) },
  );
}
