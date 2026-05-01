import { useMutation, useQuery } from '@tanstack/react-query';
import { addProjectTemplate, createProject, getProjects, removeProjectTemplate } from '@/api/endpoints/projects';
import type { ApiRequestBody } from '@/api/client';
import { useConflictMutation } from '@/lib/mutations';
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