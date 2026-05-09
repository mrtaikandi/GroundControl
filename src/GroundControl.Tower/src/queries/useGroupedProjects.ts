import { useQuery } from '@tanstack/react-query';
import { getGroupedProjects } from '@/api/endpoints/projects';
import type { ApiQuery, ApiResponse } from '@/api/client';

export type GroupedProjectsResponse = NonNullable<ApiResponse<'ListGroupedProjectsHandler'>>;
export type GroupedProjectsQuery = ApiQuery<'ListGroupedProjectsHandler'>;

export const groupedProjectsQueryKey = (query: GroupedProjectsQuery) => ['projects', 'grouped', query] as const;

export function useGroupedProjects(query?: GroupedProjectsQuery) {
  const request = buildQuery(query);

  return useQuery({
    queryFn: () => getGroupedProjects(request),
    queryKey: groupedProjectsQueryKey(request),
    staleTime: 60_000,
  });
}

function buildQuery(query?: GroupedProjectsQuery): GroupedProjectsQuery {
  return {
    Search: query?.Search,
    PerGroup: query?.PerGroup,
  };
}