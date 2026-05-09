import { useMutation, useQuery } from '@tanstack/react-query';
import { addProjectTemplate, createProject, getProjects, removeProjectTemplate, updateProject } from '@/api/endpoints/projects';
import type { ApiQuery, ApiRequestBody, ApiResponse } from '@/api/client';
import { useConflictMutation } from '@/lib/mutations';
import { queryClient } from '@/lib/query-client';

export type CreateProjectRequest = ApiRequestBody<'CreateProjectHandler'>;
export type UpdateProjectRequest = ApiRequestBody<'UpdateProjectHandler'>;
export type ProjectResponse = ApiResponse<'GetProjectHandler'>;
export type ProjectsQuery = ApiQuery<'ListProjectsHandler'>;

const PerGroupShowMoreSize = 4;

export function useProjects(query?: ProjectsQuery) {
  const request = buildProjectsQuery(query);

  return useQuery({
    queryFn: () => getProjects(request),
    queryKey: ['projects', 'list', request],
  });
}

export const UngroupedScope = 'ungrouped';
export type GroupScope = string;

export function useGroupProjectsPage(scope: GroupScope, search: string | undefined, cursor: string | undefined) {
  const isUngrouped = scope === UngroupedScope;
  const request: ProjectsQuery = {
    After: cursor,
    GroupId: isUngrouped ? undefined : scope,
    Limit: PerGroupShowMoreSize,
    Search: search,
    SortField: 'name',
    SortOrder: 'asc',
    Ungrouped: isUngrouped ? true : undefined,
  };

  return useQuery({
    enabled: cursor !== undefined,
    queryFn: () => getProjects(request),
    queryKey: ['projects', 'show-more', scope, search, cursor],
    staleTime: 60_000,
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
    Ungrouped: query?.Ungrouped,
    Search: query?.Search,
  };
}

function invalidateProjects() {
  return queryClient.invalidateQueries({ queryKey: ['projects'] });
}

export function useCreateProject() {
  return useMutation({
    mutationFn: (body: CreateProjectRequest) => createProject(body),
    onSuccess: invalidateProjects,
  });
}

export function useUpdateProject(projectId: string) {
  return useConflictMutation<{ body: UpdateProjectRequest }, ProjectResponse>(
    (variables) => updateProject(projectId, variables.body, variables.version),
    { onSuccess: invalidateProjects },
  );
}

export function useAttachProjectTemplate(projectId: string) {
  return useConflictMutation<{ templateId: string }, unknown>(
    (variables) => addProjectTemplate(projectId, variables.templateId, variables.version),
    { onSuccess: invalidateProjects },
  );
}

export function useDetachProjectTemplate(projectId: string) {
  return useConflictMutation<{ templateId: string }, unknown>(
    (variables) => removeProjectTemplate(projectId, variables.templateId, variables.version),
    { onSuccess: invalidateProjects },
  );
}