import { useMutation, useQuery } from '@tanstack/react-query';
import { createClient, getClients } from '@/api/endpoints/clients';
import type { ApiRequestBody, ApiResponse } from '@/api/client';
import { queryClient } from '@/lib/query-client';

export type Client = NonNullable<ApiResponse<'ListClientsHandler'>>['data'][number];
export type CreateClientRequest = ApiRequestBody<'CreateClientHandler'>;

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

export function useCreateClient(projectId: string, onCreated: (rawToken: string) => void) {
  return useMutation({
    mutationFn: async (body: CreateClientRequest) => {
      const response = await createClient(projectId, body);

      onCreated(response.clientSecret);
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: clientsQueryKey(projectId) }),
  });
}