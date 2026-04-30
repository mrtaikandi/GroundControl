import { useMutation, useQuery } from '@tanstack/react-query';
import { createTemplate, deleteTemplate, getTemplates, updateTemplate } from '@/api/endpoints/templates';
import type { ApiRequestBody, ApiResponse } from '@/api/client';
import { useConflictMutation } from '@/lib/mutations';
import { queryClient } from '@/lib/query-client';

export type Template = NonNullable<ApiResponse<'ListTemplatesHandler'>>['data'][number];
export type CreateTemplateRequest = ApiRequestBody<'CreateTemplateHandler'>;
export type UpdateTemplateRequest = ApiRequestBody<'UpdateTemplateHandler'>;

export const templatesQueryKey = ['templates'] as const;

export function useTemplates() {
  return useQuery({
    queryFn: () => getTemplates({ Limit: 100, SortField: 'name', SortOrder: 'asc' }),
    queryKey: templatesQueryKey,
  });
}

export function useCreateTemplate() {
  return useMutation({
    mutationFn: (body: CreateTemplateRequest) => createTemplate(body),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: templatesQueryKey }),
  });
}

export function useUpdateTemplate() {
  return useConflictMutation<{ body: UpdateTemplateRequest; id: string }, Template>(
    (variables) => updateTemplate(variables.id, variables.body, variables.version),
    { onSuccess: () => queryClient.invalidateQueries({ queryKey: templatesQueryKey }) },
  );
}

export function useDeleteTemplate() {
  return useConflictMutation<{ id: string }, void>(
    (variables) => deleteTemplate(variables.id, variables.version),
    { onSuccess: () => queryClient.invalidateQueries({ queryKey: templatesQueryKey }) },
  );
}