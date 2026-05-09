import { SearchFilterPopover } from '@/components/tower/data/SearchFilterPopover';

interface ProjectsFilterPopoverProps {
  appliedSearch: string | undefined;
  onApply: (search: string | undefined) => void;
}

export function ProjectsFilterPopover({ appliedSearch, onApply }: ProjectsFilterPopoverProps) {
  return (
    <SearchFilterPopover
      appliedSearch={appliedSearch}
      ariaLabel="Filter projects"
      onApply={onApply}
      placeholder="Project name or description"
    />
  );
}
