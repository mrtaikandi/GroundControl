import { useMutation, useQuery } from '@tanstack/react-query';
import { createConfigEntry, deleteConfigEntry, getConfigEntries, updateConfigEntry } from '@/api/endpoints/config-entries';
import type { ApiRequestBody, ApiResponse } from '@/api/client';
import { queryClient } from '@/lib/query-client';
import { useConflictMutation } from '@/lib/mutations';

export type ConfigEntry = NonNullable<ApiResponse<'ListConfigEntriesHandler'>>['data'][number];
export type CreateConfigEntryRequest = ApiRequestBody<'CreateConfigEntryHandler'>;
export type UpdateConfigEntryRequest = ApiRequestBody<'UpdateConfigEntryHandler'>;
export type ConfigEntryOwnerType = CreateConfigEntryRequest['ownerType'];

export function configEntriesQueryKey(ownerId: string, ownerType: ConfigEntryOwnerType = 1) {
  return ['config-entries', ownerType, ownerId] as const;
}

export function useConfigEntries(ownerId: string, ownerType: ConfigEntryOwnerType = 1) {
  return useQuery({
    enabled: ownerId.length > 0,
    queryFn: () => getConfigEntries({ Limit: 100, OwnerId: ownerId, OwnerType: ownerType, SortField: 'key', SortOrder: 'asc', decrypt: false }),
    queryKey: configEntriesQueryKey(ownerId, ownerType),
  });
}

export function useCreateEntry(ownerId: string, ownerType: ConfigEntryOwnerType = 1) {
  return useMutation({
    mutationFn: (body: CreateConfigEntryRequest) => createConfigEntry(body),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: configEntriesQueryKey(ownerId, ownerType) }),
  });
}

export function useUpdateEntry(ownerId: string, ownerType: ConfigEntryOwnerType = 1) {
  return useConflictMutation<{ body: UpdateConfigEntryRequest; id: string }, ConfigEntry>(
    (variables) => updateConfigEntry(variables.id, variables.body, variables.version),
    { onSuccess: () => queryClient.invalidateQueries({ queryKey: configEntriesQueryKey(ownerId, ownerType) }) },
  );
}

export function useDeleteEntry(ownerId: string, ownerType: ConfigEntryOwnerType = 1) {
  return useConflictMutation<{ id: string }, void>(
    (variables) => deleteConfigEntry(variables.id, variables.version),
    { onSuccess: () => queryClient.invalidateQueries({ queryKey: configEntriesQueryKey(ownerId, ownerType) }) },
  );
}