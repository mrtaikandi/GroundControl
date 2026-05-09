import { useMutation, useQuery } from '@tanstack/react-query';
import { createClient, deleteClient, getClients, updateClient } from '@/api/endpoints/clients';
import type { ApiRequestBody, ApiResponse } from '@/api/client';
import { useConflictMutation } from '@/lib/mutations';
import { queryClient } from '@/lib/query-client';

export type Client = NonNullable<ApiResponse<'ListClientsHandler'>>['data'][number];
export type CreateClientRequest = ApiRequestBody<'CreateClientHandler'>;
export type UpdateClientRequest = ApiRequestBody<'UpdateClientHandler'>;

export function clientsQueryKey(projectId: string) {
  return ['projects', projectId, 'clients'] as const;
}

export function useClients(projectId: string) {
  return useQuery({
    enabled: !!projectId,
    queryFn: () => getClients(projectId, { Limit: 100, SortField: 'name', SortOrder: 'asc' }),
    queryKey: clientsQueryKey(projectId),
  });
}

interface CreateClientVariables {
  body: CreateClientRequest;
  projectId: string;
}

export function useCreateClient(onCreated: (rawToken: string) => void) {
  return useMutation({
    mutationFn: async ({ body, projectId }: CreateClientVariables) => {
      const response = await createClient(projectId, body);

      onCreated(response.clientSecret);
    },
    onSuccess: (_, variables) => queryClient.invalidateQueries({ queryKey: clientsQueryKey(variables.projectId) }),
  });
}

export function useUpdateClient() {
  return useConflictMutation(
    ({ body, id, projectId, version }: { body: UpdateClientRequest; id: string; projectId: string; version: string }) =>
      updateClient(projectId, id, body, version),
    { onSuccess: (_, variables) => queryClient.invalidateQueries({ queryKey: clientsQueryKey(variables.projectId) }) },
  );
}

export function useRevokeClient(projectId: string) {
  return useConflictMutation(
    ({ id, version }: { id: string; version: string }) => deleteClient(projectId, id, version),
    { onSuccess: () => queryClient.invalidateQueries({ queryKey: clientsQueryKey(projectId) }) },
  );
}
