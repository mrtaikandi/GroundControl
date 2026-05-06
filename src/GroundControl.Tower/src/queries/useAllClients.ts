import { useQueries } from '@tanstack/react-query';
import { getClients } from '@/api/endpoints/clients';
import type { Client } from './useClients';
import { useProjects } from './useProjects';

export interface ClientWithProject extends Client {
  projectId: string;
}

export interface AllClientsResult {
  data: ClientWithProject[];
  isLoading: boolean;
}

export function useAllClients(): AllClientsResult {
  const projects = useProjects();
  const projectIds = projects.data?.data.map((project) => project.id) ?? [];

  return useQueries({
    queries: projectIds.map((projectId) => ({
      enabled: projects.isSuccess,
      queryFn: () => getClients(projectId, { Limit: 100, SortField: 'name', SortOrder: 'asc' }),
      queryKey: ['projects', projectId, 'clients'] as const,
    })),
    combine: (results) => ({
      data: results.flatMap((result, index) => {
        const projectId = projectIds[index];
        if (!projectId || !result.data?.data) {
          return [];
        }

        return result.data.data.map<ClientWithProject>((client) => ({ ...client, projectId }));
      }),
      isLoading: projects.isLoading || results.some((result) => result.isLoading),
    }),
  });
}
