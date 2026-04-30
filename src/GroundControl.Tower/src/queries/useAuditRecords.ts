import { useInfiniteQuery, type InfiniteData } from '@tanstack/react-query';
import { getAuditRecords } from '@/api/endpoints/audit';
import type { ApiQuery, ApiResponse } from '@/api/client';

export type AuditRecord = NonNullable<ApiResponse<'ListAuditRecordsHandler'>>['data'][number];
type AuditPage = ApiResponse<'ListAuditRecordsHandler'>;

export type AuditFilters = {
  enabled?: boolean;
  entityTypes: string[];
  from?: string;
  to?: string;
};

export function useAuditRecords(filters: AuditFilters) {
  return useInfiniteQuery<AuditPage, Error, InfiniteData<AuditPage>, ['audit', AuditFilters], string | undefined>({
    enabled: filters.enabled ?? true,
    getNextPageParam: (lastPage) => lastPage.nextCursor ?? undefined,
    initialPageParam: undefined as string | undefined,
    queryFn: async ({ pageParam }) => {
      const response = await getAuditRecords(buildQuery(filters, pageParam));

      if (filters.entityTypes.length <= 1) {
        return response;
      }

      return {
        ...response,
        data: response.data.filter((record) => filters.entityTypes.includes(record.entityType)),
      };
    },
    queryKey: ['audit', filters],
  });
}

function buildQuery(filters: AuditFilters, pageParam?: string): ApiQuery<'ListAuditRecordsHandler'> {
  return {
    after: pageParam,
    entityType: filters.entityTypes.length === 1 ? filters.entityTypes[0] : undefined,
    from: filters.from || undefined,
    limit: 50,
    to: filters.to || undefined,
  };
}