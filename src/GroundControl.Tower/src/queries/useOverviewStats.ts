import { useQueries } from '@tanstack/react-query';
import { getProjects } from '@/api/endpoints/projects';
import { getSnapshots } from '@/api/endpoints/snapshots';

const oneDayInMs = 24 * 60 * 60 * 1000;

export function useOverviewStats() {
  const [projects] = useQueries({
    queries: [
      {
        queryFn: () => getProjects({ Limit: 100, SortField: 'updatedAt', SortOrder: 'desc' }),
        queryKey: ['stats', 'projects'],
      },
    ],
  });
  const projectIds = projects.data?.data.map((project) => project.id) ?? [];
  const snapshotQueries = useQueries({
    queries: projectIds.map((projectId) => ({
      enabled: projects.isSuccess,
      queryFn: () => getSnapshots(projectId, { Limit: 100, SortField: 'publishedAt', SortOrder: 'desc' }),
      queryKey: ['stats', 'snapshots', projectId],
    })),
  });
  const since = Date.now() - oneDayInMs;
  const snapshotsToday = snapshotQueries.reduce((count, query) => count + (query.data?.data.filter((snapshot) => new Date(snapshot.publishedAt).getTime() >= since).length ?? 0), 0);

  return {
    activeProjects: Number(projects.data?.totalCount ?? projects.data?.data.length ?? 0),
    isFetching: projects.isFetching || snapshotQueries.some((query) => query.isFetching),
    isLoading: projects.isLoading || snapshotQueries.some((query) => query.isLoading),
    projects: projects.data?.data ?? [],
    snapshotsToday,
  };
}