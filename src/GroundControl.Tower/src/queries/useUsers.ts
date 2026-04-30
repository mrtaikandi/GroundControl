import { useMutation, useQuery } from '@tanstack/react-query';
import { setGroupMember } from '@/api/endpoints/groups';
import { getUser, getUsers, updateUser } from '@/api/endpoints/users';
import type { ApiResponse } from '@/api/client';
import { useConflictMutation } from '@/lib/mutations';
import { queryClient } from '@/lib/query-client';

export type User = NonNullable<ApiResponse<'ListUsersHandler'>>['data'][number];
export type UserDetail = ApiResponse<'GetUserHandler'>;
export type Grant = UserDetail['grants'][number];

export function usersQueryKey() {
  return ['users'] as const;
}

export function userGrantsQueryKey(userId: string) {
  return ['users', userId, 'grants'] as const;
}

export function useUsers() {
  return useQuery({
    queryFn: () => getUsers({ Limit: 100, SortField: 'username', SortOrder: 'asc' }),
    queryKey: usersQueryKey(),
  });
}

export function useUserGrants(userId: string | null) {
  return useQuery({
    enabled: !!userId,
    queryFn: () => getUser(userId!),
    queryKey: userId ? userGrantsQueryKey(userId) : ['users', 'none', 'grants'],
  });
}

export function useAddGrant(userId: string) {
  return useMutation({
    mutationFn: ({ groupId, roleId }: { groupId: string; roleId: string }) => setGroupMember(groupId, userId, { roleId }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: usersQueryKey() });
      void queryClient.invalidateQueries({ queryKey: userGrantsQueryKey(userId) });
    },
  });
}

export function useRemoveGrant(userId: string) {
  return useConflictMutation(
    ({ grant, user, version }: { grant: Grant; user: UserDetail; version: string }) => updateUser(userId, {
      email: user.email,
      grants: user.grants.filter((candidate) => !isSameGrant(candidate, grant)),
      isActive: user.isActive,
      username: user.username,
    }, version),
    {
      onSuccess: () => {
        void queryClient.invalidateQueries({ queryKey: usersQueryKey() });
        void queryClient.invalidateQueries({ queryKey: userGrantsQueryKey(userId) });
      },
    },
  );
}

function isSameGrant(left: Grant, right: Grant) {
  return (left.resource ?? null) === (right.resource ?? null) && left.roleId === right.roleId;
}