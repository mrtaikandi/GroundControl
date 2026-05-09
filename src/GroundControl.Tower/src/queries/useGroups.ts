import { useMutation, useQuery } from '@tanstack/react-query';
import { getGroup, getGroupMembers, getGroups, setGroupMember } from '@/api/endpoints/groups';
import type { ApiResponse } from '@/api/client';
import { queryClient } from '@/lib/query-client';

export type Group = NonNullable<ApiResponse<'ListGroupsHandler'>>['data'][number];
export type GroupMember = ApiResponse<'ListGroupMembersHandler'>[number];

export function groupsQueryKey() {
  return ['groups'] as const;
}

export function groupMembersQueryKey(groupId: string) {
  return ['groups', groupId, 'members'] as const;
}

export function useGroups() {
  return useQuery({
    queryFn: () => getGroups({ Limit: 100, SortField: 'name', SortOrder: 'asc' }),
    queryKey: groupsQueryKey(),
    staleTime: 30_000,
  });
}

export function groupQueryKey(groupId: string) {
  return ['groups', groupId] as const;
}

export function useGroup(groupId: string) {
  return useQuery({
    queryFn: () => getGroup(groupId),
    queryKey: groupQueryKey(groupId),
    staleTime: 30_000,
  });
}

export function useGroupMembers(groupId: string | null) {
  return useQuery({
    enabled: !!groupId,
    queryFn: () => getGroupMembers(groupId!),
    queryKey: groupId ? groupMembersQueryKey(groupId) : ['groups', 'none', 'members'],
  });
}

export function useAssignGroupRole(groupId: string) {
  return useMutation({
    mutationFn: ({ roleId, userId }: { roleId: string; userId: string }) => setGroupMember(groupId, userId, { roleId }),
    onSuccess: (_data, variables) => {
      void queryClient.invalidateQueries({ queryKey: groupMembersQueryKey(groupId) });
      void queryClient.invalidateQueries({ queryKey: ['users'] });
      void queryClient.invalidateQueries({ queryKey: ['users', variables.userId, 'grants'] });
    },
  });
}