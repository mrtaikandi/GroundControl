import { useMutation, useQuery } from '@tanstack/react-query';
import { createConfigEntry, deleteConfigEntry, getConfigEntries, updateConfigEntry } from '@/api/endpoints/config-entries';
import type { ApiRequestBody, ApiResponse } from '@/api/client';
import { queryClient } from '@/lib/query-client';
import { useConflictMutation } from '@/lib/mutations';

export type ConfigEntry = NonNullable<ApiResponse<'ListConfigEntriesHandler'>>['data'][number];
export type CreateConfigEntryRequest = ApiRequestBody<'CreateConfigEntryHandler'>;
export type UpdateConfigEntryRequest = ApiRequestBody<'UpdateConfigEntryHandler'>;

export function configEntriesQueryKey(projectId: string) {
  return ['projects', projectId, 'config-entries'] as const;
}

export function useConfigEntries(projectId: string) {
  return useQuery({
    queryFn: () => getConfigEntries({ Limit: 100, OwnerId: projectId, OwnerType: 1, SortField: 'key', SortOrder: 'asc', decrypt: false }),
    queryKey: configEntriesQueryKey(projectId),
  });
}

export function useCreateEntry(projectId: string) {
  return useMutation({
    mutationFn: (body: CreateConfigEntryRequest) => createConfigEntry(body),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: configEntriesQueryKey(projectId) }),
  });
}

export function useUpdateEntry(projectId: string) {
  return useConflictMutation<{ body: UpdateConfigEntryRequest; id: string }, ConfigEntry>(
    (variables) => updateConfigEntry(variables.id, variables.body, variables.version),
    { onSuccess: () => queryClient.invalidateQueries({ queryKey: configEntriesQueryKey(projectId) }) },
  );
}

export function useDeleteEntry(projectId: string) {
  return useConflictMutation<{ id: string }, void>(
    (variables) => deleteConfigEntry(variables.id, variables.version),
    { onSuccess: () => queryClient.invalidateQueries({ queryKey: configEntriesQueryKey(projectId) }) },
  );
}