import { X } from 'lucide-react';

interface ActiveFilterChipsProps {
  onRemoveSearch: () => void;
  search: string | undefined;
}

export function ActiveFilterChips({ onRemoveSearch, search }: ActiveFilterChipsProps) {
  if (!search) {
    return null;
  }

  return (
    <div className="flex flex-wrap items-center gap-2 px-1">
      <Chip label="Search" onRemove={onRemoveSearch} value={search} />
    </div>
  );
}

interface ChipProps {
  label: string;
  onRemove: () => void;
  value: string;
}

function Chip({ label, onRemove, value }: ChipProps) {
  return (
    <span className="inline-flex items-center gap-1.5 rounded-full bg-primary-100 px-2.5 py-1 text-[12px] text-primary-text">
      <span>{label}: <span className="font-semibold">"{value}"</span></span>
      <button
        aria-label={`Remove ${label.toLowerCase()} filter`}
        className="rounded-full p-0.5 transition-colors hover:bg-primary-200"
        onClick={onRemove}
        type="button"
      >
        <X aria-hidden="true" className="size-3" />
      </button>
    </span>
  );
}