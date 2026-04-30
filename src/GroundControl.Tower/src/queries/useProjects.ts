import { useMutation, useQuery } from '@tanstack/react-query';
import { createProject, getProjects } from '@/api/endpoints/projects';
import type { ApiRequestBody } from '@/api/client';
import { queryClient } from '@/lib/query-client';

export type CreateProjectRequest = ApiRequestBody<'CreateProjectHandler'>;

export function useProjects() {
  return useQuery({
    queryFn: () => getProjects({ Limit: 100, SortField: 'updatedAt', SortOrder: 'desc' }),
    queryKey: ['projects'],
  });
}

export function useCreateProject() {
  return useMutation({
    mutationFn: (body: CreateProjectRequest) => createProject(body),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['projects'] }),
  });
}