import { useQuery } from '@tanstack/react-query';
import { getConfigEntries } from '@/api/endpoints/config-entries';
import { resolveConfigEntries } from '@/lib/resolve-config';
import type { ConfigEntry } from './useConfigEntries';

export function useResolvedConfig(projectId: string, scopes: Record<string, string>) {
  return useQuery({
    enabled: !!projectId,
    queryFn: async () => {
      const entries: ConfigEntry[] = [];
      let after: string | undefined;

      do {
        const page = await getConfigEntries({ After: after, Limit: 100, OwnerId: projectId, OwnerType: 1, SortField: 'key', SortOrder: 'asc', decrypt: false });

        entries.push(...page.data);
        after = page.nextCursor ?? undefined;
      } while (after);

      return resolveConfigEntries(entries, scopes);
    },
    queryKey: ['projects', projectId, 'resolved-config', scopes],
  });
}