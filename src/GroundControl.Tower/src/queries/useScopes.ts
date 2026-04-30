import { useQuery } from '@tanstack/react-query';
import { getScopes } from '@/api/endpoints/scopes';

export function useScopes() {
  return useQuery({
    queryFn: () => getScopes({ Limit: 100, SortField: 'dimension', SortOrder: 'asc' }),
    queryKey: ['scopes'],
    staleTime: 30_000,
  });
}