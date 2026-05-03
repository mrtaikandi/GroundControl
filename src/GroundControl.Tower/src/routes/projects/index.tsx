import { ChevronLeft, ChevronRight, Search, X } from 'lucide-react';
import { createFileRoute, Link, useNavigate } from '@tanstack/react-router';
import { useDeferredValue, useMemo } from 'react';
import { Badge } from '@/components/tower/data/Badge';
import { InlineCode } from '@/components/tower/data/InlineCode';
import { NewProjectModal } from '@/components/tower/projects/NewProjectModal';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Skeleton } from '@/components/ui/skeleton';
import { useGroups } from '@/queries/useGroups';
import { useProjects } from '@/queries/useProjects';

const AllGroupsValue = 'all-groups';
const PageSize = 12;
const DefaultSortValue = 'name-asc';

const SortOptions = {
  'name-asc': { label: 'Name (A-Z)', sortField: 'name', sortOrder: 'asc' },
  'name-desc': { label: 'Name (Z-A)', sortField: 'name', sortOrder: 'desc' },
  'updated-desc': { label: 'Recently updated', sortField: 'updatedAt', sortOrder: 'desc' },
  'updated-asc': { label: 'Least recently updated', sortField: 'updatedAt', sortOrder: 'asc' },
} as const;

type PageState = {
  after?: string;
  before?: string;
  index: number;
};

type SortValue = keyof typeof SortOptions;
type ProjectsSearch = {
  after?: string;
  before?: string;
  group?: string;
  page: number;
  query: string;
  sort: SortValue;
};

export const DefaultProjectsSearch: ProjectsSearch = {
  page: 0,
  query: '',
  sort: DefaultSortValue,
};

export const Route = createFileRoute('/projects/')({
  validateSearch: (search): ProjectsSearch => ({
    after: readOptionalString(search.after),
    before: readOptionalString(search.before),
    group: readOptionalString(search.group),
    page: readPageIndex(search.page),
    query: readOptionalString(search.query) ?? DefaultProjectsSearch.query,
    sort: readSortValue(search.sort),
  }),
  component: ProjectsRoute,
});

function ProjectsRoute() {
  const navigate = useNavigate({ from: Route.fullPath });
  const routeSearch = Route.useSearch();
  const searchText = routeSearch.query;
  const selectedGroupId = routeSearch.group ?? AllGroupsValue;
  const selectedSort = routeSearch.sort;
  const page: PageState = useMemo(() => ({ after: routeSearch.after, before: routeSearch.before, index: routeSearch.page }), [routeSearch.after, routeSearch.before, routeSearch.page]);
  const deferredSearch = useDeferredValue(searchText.trim());
  const groupFilter = routeSearch.group;
  const sort = SortOptions[selectedSort];
  const projects = useProjects({
    After: page.after,
    Before: page.before,
    GroupId: groupFilter,
    Limit: PageSize,
    Search: deferredSearch || undefined,
    SortField: sort.sortField,
    SortOrder: sort.sortOrder,
  });
  const groups = useGroups();
  const groupNames = useMemo(() => new Map((groups.data?.data ?? []).map((group) => [group.id, group.name])), [groups.data?.data]);
  const projectItems = projects.data?.data ?? [];
  const totalCount = Number(projects.data?.totalCount ?? 0);
  const hasFilters = !!deferredSearch || !!groupFilter;
  const showingFrom = totalCount === 0 ? 0 : page.index * PageSize + 1;
  const showingTo = totalCount === 0 ? 0 : page.index * PageSize + projectItems.length;
  const summaryText = projects.isLoading ? 'Loading projects...' : `${showingFrom}-${showingTo} of ${totalCount} project${totalCount === 1 ? '' : 's'}`;
  const pendingFilterChange = searchText.trim() !== deferredSearch;

  function clearFilters() {
    void navigate({
      replace: true,
      search: (current) => normalizeSearch({
        ...current,
        after: undefined,
        before: undefined,
        group: undefined,
        page: 0,
        query: '',
      }),
    });
  }

  function goToNextPage() {
    if (!projects.data?.nextCursor) {
      return;
    }

    void navigate({
      search: (current) => normalizeSearch({
        ...current,
        after: projects.data?.nextCursor ?? undefined,
        before: undefined,
        page: current.page + 1,
      }),
    });
  }

  function goToPreviousPage() {
    if (!projects.data?.previousCursor) {
      return;
    }

    void navigate({
      search: (current) => normalizeSearch({
        ...current,
        after: undefined,
        before: projects.data?.previousCursor ?? undefined,
        page: Math.max(0, current.page - 1),
      }),
    });
  }

  function updateQuery(query: string) {
    void navigate({
      replace: true,
      search: (current) => normalizeSearch({
        ...current,
        after: undefined,
        before: undefined,
        page: 0,
        query,
      }),
    });
  }

  function updateGroup(group: string) {
    void navigate({
      replace: true,
      search: (current) => normalizeSearch({
        ...current,
        after: undefined,
        before: undefined,
        group: group === AllGroupsValue ? undefined : group,
        page: 0,
      }),
    });
  }

  function updateSort(sortValue: string) {
    if (!isSortValue(sortValue)) {
      return;
    }

    void navigate({
      replace: true,
      search: (current) => normalizeSearch({
        ...current,
        after: undefined,
        before: undefined,
        page: 0,
        sort: sortValue,
      }),
    });
  }

  return (
    <div className="grid gap-8">
      <div className="flex items-start justify-between gap-4">
        <div>
          <h1 className="text-[34px] font-bold leading-tight text-fg-heading">Projects</h1>
          <p className="mt-2 text-[14.5px] text-fg-caption">Manage your configuration projects</p>
        </div>
        <NewProjectModal />
      </div>

      <div className="rounded-xl border border-stroke-subtle bg-bg-surface p-4">
        <div className="grid gap-3 lg:grid-cols-[minmax(0,1fr)_220px_220px_auto]">
          <div>
            <div className="relative">
              <Search aria-hidden="true" className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-fg-icon-subtle" />
              <Input
                aria-label="Search projects"
                className="pl-9"
                onChange={(event) => updateQuery(event.target.value)}
                placeholder="Search project names"
                value={searchText}
              />
            </div>
          </div>

          <div>
            <Select onValueChange={updateGroup} value={selectedGroupId}>
              <SelectTrigger aria-label="Filter projects by group">
                <SelectValue placeholder="All groups" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value={AllGroupsValue}>All groups</SelectItem>
                {(groups.data?.data ?? []).map((group) => (
                  <SelectItem key={group.id} value={group.id}>{group.name}</SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <div>
            <Select onValueChange={updateSort} value={selectedSort}>
              <SelectTrigger aria-label="Sort projects">
                <SelectValue placeholder="Sort projects" />
              </SelectTrigger>
              <SelectContent>
                {Object.entries(SortOptions).map(([value, option]) => (
                  <SelectItem key={value} value={value}>{option.label}</SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <div className="flex items-end justify-start lg:justify-end">
            <Button disabled={!hasFilters && !searchText} onClick={clearFilters} type="button" variant="ghost">
              <X aria-hidden="true" className="size-3.5" />
              Clear filters
            </Button>
          </div>
        </div>

        <div className="mt-3 flex flex-wrap items-center justify-end gap-3 border-t border-stroke-subtle/70 pt-3 text-[12.5px] text-fg-caption">
          <span>{pendingFilterChange ? 'Updating results...' : summaryText}</span>
        </div>
      </div>

      {projects.isLoading ? <ProjectSkeletonList /> : null}
      {projects.isError ? <div className="rounded-xl border border-stroke-subtle bg-bg-surface p-6 text-fg-caption">Projects could not be loaded.</div> : null}
      {!projects.isLoading && !projects.isError && projectItems.length === 0 ? <EmptyProjects hasFilters={hasFilters} onClearFilters={clearFilters} /> : null}
      {projectItems.length > 0 ? (
        <ul className="grid gap-3">
          {projectItems.map((project) => (
            <li key={project.id}>
              <Link
                className="block rounded-xl border border-stroke-subtle bg-bg-surface p-5 transition-colors hover:border-stroke-divider hover:bg-bg-container focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-stroke-field-focus"
                params={{ projectId: project.id }}
                to="/projects/$projectId"
              >
                <div className="flex flex-wrap items-center gap-3">
                  <h2 className="text-[15px] font-semibold text-fg-heading">
                    <InlineCode>{project.name}</InlineCode>
                  </h2>
                  <Badge variant="neutral">{project.groupId ? groupNames.get(project.groupId) ?? 'group pending' : 'ungrouped'}</Badge>
                  <Badge variant={project.activeSnapshotId ? 'info' : 'neutral'}>
                    {project.activeSnapshotId ? 'active snapshot' : 'no active snapshot'}
                  </Badge>
                </div>
                <div className="mt-2 flex flex-wrap items-end justify-between gap-3">
                  <p className="min-w-0 flex-1 text-[13px] text-fg-body">{project.description || 'No description provided.'}</p>
                  <span className="shrink-0 text-[12.5px] text-fg-caption">Updated {formatDate(project.updatedAt)}</span>
                </div>
              </Link>
            </li>
          ))}
        </ul>
      ) : null}

      {!projects.isLoading && !projects.isError && (projectItems.length > 0 || hasFilters) ? (
        <div className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-stroke-subtle bg-bg-surface px-4 py-3">
          <div>
            <div className="text-[12.5px] text-fg-caption">{summaryText}</div>
          </div>
          <div className="flex items-center gap-2">
            <Button disabled={!projects.data?.previousCursor} onClick={goToPreviousPage} size="sm" type="button" variant="secondary">
              <ChevronLeft aria-hidden="true" className="size-3.5" />
              Previous
            </Button>
            <Button disabled={!projects.data?.nextCursor} onClick={goToNextPage} size="sm" type="button" variant="secondary">
              Next
              <ChevronRight aria-hidden="true" className="size-3.5" />
            </Button>
          </div>
        </div>
      ) : null}
    </div>
  );
}

function ProjectSkeletonList() {
  return (
    <div className="grid gap-3">
      {Array.from({ length: 5 }, (_, index) => <Skeleton className="h-24" key={index} />)}
    </div>
  );
}

function EmptyProjects({ hasFilters, onClearFilters }: { hasFilters: boolean; onClearFilters: () => void }) {
  return (
    <div className="rounded-xl border border-stroke-subtle bg-bg-surface p-8 text-center">
      <h2 className="text-[19px] font-semibold text-fg-heading">{hasFilters ? 'No matching projects' : 'No projects yet'}</h2>
      <p className="mx-auto mt-2 max-w-md text-[13px] text-fg-caption">
        {hasFilters ? 'Try clearing the current search or group filter to see more projects.' : 'Create the first project to start collecting entries, scopes, variables, templates, and snapshots.'}
      </p>
      <div className="mt-5 flex justify-center gap-3">
        {hasFilters ? <Button onClick={onClearFilters} type="button" variant="secondary">Clear filters</Button> : null}
        <NewProjectModal />
      </div>
    </div>
  );
}

function formatDate(value: string) {
  return new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value));
}

function isSortValue(value: unknown): value is SortValue {
  return typeof value === 'string' && value in SortOptions;
}

function normalizeSearch(search: ProjectsSearch): ProjectsSearch {
  return {
    after: readOptionalString(search.after),
    before: readOptionalString(search.before),
    group: readOptionalString(search.group),
    page: search.page > 0 ? search.page : DefaultProjectsSearch.page,
    query: readOptionalString(search.query) ?? DefaultProjectsSearch.query,
    sort: search.sort,
  };
}

function readOptionalString(value: unknown) {
  return typeof value === 'string' && value.trim().length > 0 ? value : undefined;
}

function readPageIndex(value: unknown) {
  if (typeof value === 'number' && Number.isInteger(value) && value >= 0) {
    return value;
  }

  if (typeof value === 'string') {
    const parsed = Number.parseInt(value, 10);
    return Number.isInteger(parsed) && parsed >= 0 ? parsed : 0;
  }

  return 0;
}

function readSortValue(value: unknown): SortValue {
  return isSortValue(value) ? value : DefaultSortValue;
}
