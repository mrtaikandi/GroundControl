import { useMutation, useQuery } from '@tanstack/react-query';
import { addProjectTemplate, createProject, getProjects, removeProjectTemplate, updateProject } from '@/api/endpoints/projects';
import type { ApiQuery, ApiRequestBody, ApiResponse } from '@/api/client';
import { useConflictMutation } from '@/lib/mutations';
import { queryClient } from '@/lib/query-client';

export type CreateProjectRequest = ApiRequestBody<'CreateProjectHandler'>;
export type UpdateProjectRequest = ApiRequestBody<'UpdateProjectHandler'>;
export type ProjectResponse = ApiResponse<'GetProjectHandler'>;

export function useProjects(query?: ProjectsQuery) {
  const request = buildProjectsQuery(query);

  return useQuery({
    queryFn: () => getProjects(request),
    queryKey: ['projects', request],
  });
}

function buildProjectsQuery(query?: ProjectsQuery): ProjectsQuery {
  return {
    Limit: query?.Limit ?? 100,
    SortField: query?.SortField ?? 'name',
    SortOrder: query?.SortOrder ?? 'asc',
    After: query?.After,
    Before: query?.Before,
    GroupId: query?.GroupId,
    Search: query?.Search,
  };
}

export type ProjectsQuery = ApiQuery<'ListProjectsHandler'>;

export function useCreateProject() {
  return useMutation({
    mutationFn: (body: CreateProjectRequest) => createProject(body),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['projects'] }),
  });
}

export function useUpdateProject(projectId: string) {
  return useConflictMutation<{ body: UpdateProjectRequest }, ProjectResponse>(
    (variables) => updateProject(projectId, variables.body, variables.version),
    { onSuccess: () => queryClient.invalidateQueries({ queryKey: ['projects'] }) },
  );
}

export function useAttachProjectTemplate(projectId: string) {
  return useConflictMutation<{ templateId: string }, unknown>(
    (variables) => addProjectTemplate(projectId, variables.templateId, variables.version),
    { onSuccess: () => queryClient.invalidateQueries({ queryKey: ['projects'] }) },
  );
}

export function useDetachProjectTemplate(projectId: string) {
  return useConflictMutation<{ templateId: string }, unknown>(
    (variables) => removeProjectTemplate(projectId, variables.templateId, variables.version),
    { onSuccess: () => queryClient.invalidateQueries({ queryKey: ['projects'] }) },
  );
}