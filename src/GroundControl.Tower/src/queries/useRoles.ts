import { useQuery } from '@tanstack/react-query';
import { getRoles } from '@/api/endpoints/roles';
import type { ApiResponse } from '@/api/client';

export type Role = ApiResponse<'ListRolesHandler'>[number];

export function useRoles() {
  return useQuery({
    queryFn: getRoles,
    queryKey: ['roles'],
    staleTime: 30_000,
  });
}