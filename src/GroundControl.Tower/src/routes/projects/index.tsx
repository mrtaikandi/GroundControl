import { createFileRoute, useNavigate } from '@tanstack/react-router';
import { ActiveFilterChips } from '@/components/tower/projects/ActiveFilterChips';
import { NewProjectModal } from '@/components/tower/projects/NewProjectModal';
import { OtherProjectsSection } from '@/components/tower/projects/OtherProjectsSection';
import { ProjectGroupSection } from '@/components/tower/projects/ProjectGroupSection';
import { ProjectsFilterPopover } from '@/components/tower/projects/ProjectsFilterPopover';
import { PageContent } from '@/components/tower/shell/PageContent';
import { PageHeader } from '@/components/tower/shell/PageHeader';
import { Skeleton } from '@/components/ui/skeleton';
import { useGroupedProjects } from '@/queries/useGroupedProjects';

const PerGroup = 4;

type ProjectsSearch = {
  search?: string;
};

export const DefaultProjectsSearch: ProjectsSearch = {};

export const Route = createFileRoute('/projects/')({
  validateSearch: (search): ProjectsSearch => ({
    search: readOptionalString(search.search),
  }),
  component: ProjectsRoute,
});

function ProjectsRoute() {
  const navigate = useNavigate({ from: Route.fullPath });
  const { search } = Route.useSearch();
  const grouped = useGroupedProjects({ Search: search, PerGroup });

  function applySearch(value: string | undefined) {
    void navigate({
      replace: true,
      search: () => ({ search: value }),
    });
  }

  const groups = grouped.data?.groups ?? [];
  const ungrouped = grouped.data?.ungrouped ?? null;
  const ungroupedCount = ungrouped ? Number(ungrouped.totalCount) : 0;
  const hasResults = groups.length > 0 || ungroupedCount > 0;

  return (
    <>
      <PageHeader
        actions={(
          <div className="flex items-center gap-2">
            <ProjectsFilterPopover appliedSearch={search} onApply={applySearch} />
            <NewProjectModal />
          </div>
        )}
        description="Manage your configuration projects, organised by group."
        title="Projects"
      />

      <PageContent>
        <div className="grid gap-8 pt-8">
          <ActiveFilterChips onRemoveSearch={() => applySearch(undefined)} search={search} />

          {grouped.isLoading ? (
            <div className="grid gap-8">
              {Array.from({ length: 2 }, (_, sectionIndex) => (
                <div className="grid gap-2.5" key={sectionIndex}>
                  <Skeleton className="h-5 w-40" />
                  <div className="grid gap-2 rounded-[10px] border border-stroke-divider bg-bg-surface p-4">
                    {Array.from({ length: 3 }, (_, rowIndex) => <Skeleton className="h-10" key={rowIndex} />)}
                  </div>
                </div>
              ))}
            </div>
          ) : null}

          {grouped.isError ? (
            <div className="rounded-xl border border-stroke-divider bg-bg-surface p-6 text-fg-caption">Projects could not be loaded.</div>
          ) : null}

          {!grouped.isLoading && !grouped.isError && !hasResults ? (
            <EmptyState hasFilter={!!search} onClearFilter={() => applySearch(undefined)} />
          ) : null}

          {groups.map((group) => (
            <ProjectGroupSection
              description={group.description}
              groupId={group.id}
              initialNextCursor={group.nextCursor}
              initialProjects={group.projects}
              key={`${group.id}:${search ?? ''}`}
              name={group.name}
              search={search}
              totalCount={Number(group.totalCount)}
            />
          ))}

          {ungrouped && ungroupedCount > 0 ? (
            <OtherProjectsSection
              initialNextCursor={ungrouped.nextCursor}
              initialProjects={ungrouped.projects}
              key={`ungrouped:${search ?? ''}`}
              search={search}
              totalCount={ungroupedCount}
            />
          ) : null}
        </div>
      </PageContent>
    </>
  );
}

interface EmptyStateProps {
  hasFilter: boolean;
  onClearFilter: () => void;
}

function EmptyState({ hasFilter, onClearFilter }: EmptyStateProps) {
  if (hasFilter) {
    return (
      <div className="rounded-xl border border-stroke-divider bg-bg-surface p-8 text-center">
        <h2 className="text-[19px] font-semibold text-fg-heading">No matching projects</h2>
        <p className="mx-auto mt-2 max-w-md text-[13px] text-fg-caption">No project matches the current search. Try a different term or clear the filter.</p>
        <div className="mt-5 flex justify-center gap-3">
          <button
            className="inline-flex h-8 items-center rounded-full border border-input bg-background px-3 text-[12.5px] font-semibold text-fg-body transition-colors hover:bg-bg-container"
            onClick={onClearFilter}
            type="button"
          >
            Clear filter
          </button>
        </div>
      </div>
    );
  }

  return (
    <div className="rounded-xl border border-stroke-divider bg-bg-surface p-8 text-center">
      <h2 className="text-[19px] font-semibold text-fg-heading">No projects yet</h2>
      <p className="mx-auto mt-2 max-w-md text-[13px] text-fg-caption">Create the first project to start collecting entries, scopes, variables, templates, and snapshots.</p>
      <div className="mt-5 flex justify-center">
        <NewProjectModal />
      </div>
    </div>
  );
}

function readOptionalString(value: unknown) {
  return typeof value === 'string' && value.trim().length > 0 ? value : undefined;
}