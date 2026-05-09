import { ChevronLeft, ChevronRight } from 'lucide-react';
import { createFileRoute, Link, useNavigate } from '@tanstack/react-router';
import { useMemo } from 'react';
import { ActiveFilterChips } from '@/components/tower/projects/ActiveFilterChips';
import { ProjectRow } from '@/components/tower/projects/ProjectRow';
import { ProjectsFilterPopover } from '@/components/tower/projects/ProjectsFilterPopover';
import { PageContent } from '@/components/tower/shell/PageContent';
import { PageHeader } from '@/components/tower/shell/PageHeader';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { useGroups } from '@/queries/useGroups';
import { useProjects } from '@/queries/useProjects';

const PageSize = 25;

type GroupSearch = {
  after?: string;
  before?: string;
  search?: string;
};

export const Route = createFileRoute('/projects/group/$groupId')({
  validateSearch: (search): GroupSearch => ({
    after: readOptionalString(search.after),
    before: readOptionalString(search.before),
    search: readOptionalString(search.search),
  }),
  component: ProjectsGroupRoute,
});

function ProjectsGroupRoute() {
  const navigate = useNavigate({ from: Route.fullPath });
  const { groupId } = Route.useParams();
  const routeSearch = Route.useSearch();
  const groups = useGroups();
  const group = useMemo(() => (groups.data?.data ?? []).find((entry) => entry.id === groupId), [groups.data?.data, groupId]);
  const projects = useProjects({
    After: routeSearch.after,
    Before: routeSearch.before,
    GroupId: groupId,
    Limit: PageSize,
    Search: routeSearch.search,
    SortField: 'name',
    SortOrder: 'asc',
  });

  function applySearch(value: string | undefined) {
    void navigate({
      replace: true,
      search: () => ({ after: undefined, before: undefined, search: value }),
    });
  }

  function goNext() {
    if (!projects.data?.nextCursor) {
      return;
    }

    void navigate({
      search: (current) => ({ ...current, after: projects.data?.nextCursor ?? undefined, before: undefined }),
    });
  }

  function goPrevious() {
    if (!projects.data?.previousCursor) {
      return;
    }

    void navigate({
      search: (current) => ({ ...current, after: undefined, before: projects.data?.previousCursor ?? undefined }),
    });
  }

  const totalCount = Number(projects.data?.totalCount ?? 0);
  const items = projects.data?.data ?? [];
  const groupName = group?.name ?? 'Group';

  return (
    <>
      <PageHeader
        actions={(
          <div className="flex items-center gap-2">
            <ProjectsFilterPopover appliedSearch={routeSearch.search} onApply={applySearch} />
            <Button asChild variant="secondary">
              <Link to="/projects">All projects</Link>
            </Button>
          </div>
        )}
        description={group?.description ?? undefined}
        eyebrow={(
          <Link className="text-fg-caption hover:underline" to="/projects">Projects</Link>
        )}
        title={groupName}
      />

      <PageContent>
        <div className="grid gap-6 pt-8">
          <ActiveFilterChips onRemoveSearch={() => applySearch(undefined)} search={routeSearch.search} />

          {projects.isLoading ? (
            <div className="grid gap-2">{Array.from({ length: 5 }, (_, index) => <Skeleton className="h-16" key={index} />)}</div>
          ) : null}

          {projects.isError ? (
            <div className="rounded-xl border border-stroke-divider bg-bg-surface p-6 text-fg-caption">Projects could not be loaded.</div>
          ) : null}

          {!projects.isLoading && !projects.isError && items.length === 0 ? (
            <div className="rounded-xl border border-stroke-divider bg-bg-surface p-8 text-center text-[13px] text-fg-caption">
              No matching projects in {groupName}.
            </div>
          ) : null}

          {items.length > 0 ? (
            <div className="overflow-hidden rounded-[10px] border border-stroke-divider bg-bg-surface">
              <ul className="grid divide-y divide-stroke-subtle">
                {items.map((project) => (
                  <li key={project.id}>
                    <ProjectRow project={project} />
                  </li>
                ))}
              </ul>
            </div>
          ) : null}

          {!projects.isLoading && items.length > 0 ? (
            <div className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-stroke-divider bg-bg-surface px-4 py-3">
              <span className="text-[12.5px] text-fg-caption">{totalCount} {totalCount === 1 ? 'project' : 'projects'}</span>
              <div className="flex items-center gap-2">
                <Button disabled={!projects.data?.previousCursor} onClick={goPrevious} size="sm" type="button" variant="secondary">
                  <ChevronLeft aria-hidden="true" className="size-3.5" />
                  Previous
                </Button>
                <Button disabled={!projects.data?.nextCursor} onClick={goNext} size="sm" type="button" variant="secondary">
                  Next
                  <ChevronRight aria-hidden="true" className="size-3.5" />
                </Button>
              </div>
            </div>
          ) : null}
        </div>
      </PageContent>
    </>
  );
}

function readOptionalString(value: unknown) {
  return typeof value === 'string' && value.trim().length > 0 ? value : undefined;
}