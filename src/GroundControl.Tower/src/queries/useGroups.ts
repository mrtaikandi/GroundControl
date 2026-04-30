import { useQuery } from '@tanstack/react-query';
import { getGroups } from '@/api/endpoints/groups';

export function useGroups() {
  return useQuery({
    queryFn: () => getGroups({ Limit: 100, SortField: 'name', SortOrder: 'asc' }),
    queryKey: ['groups'],
    staleTime: 30_000,
  });
}