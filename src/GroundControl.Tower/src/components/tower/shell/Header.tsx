import { StatusDot } from '@/components/tower/data/StatusDot';
import { liveActivityAtom } from '@/lib/atoms';
import { useAtomValue } from 'jotai';
import { Bell, Search } from 'lucide-react';

export function Header() {
  const liveActivity = useAtomValue(liveActivityAtom);
  const dotStatus = liveActivity.isConnected ? 'live' : liveActivity.lastEventAt ? 'warning' : 'offline';

  return (
    <header className="flex h-14 items-center justify-between gap-5 border-b border-stroke-subtle bg-bg-surface px-6">
      <label className="flex h-9 w-full max-w-[540px] items-center gap-2 rounded-lg border border-stroke-field-initial bg-bg-surface px-3 text-fg-caption focus-within:border-stroke-field-focus">
        <Search aria-hidden="true" className="size-4 text-fg-icon-subtle" strokeWidth={1.8} />
        <span className="sr-only">Search</span>
        <input
          className="min-w-0 flex-1 border-0 bg-transparent text-[13px] text-fg-body outline-none placeholder:text-fg-caption"
          placeholder="Search projects, keys, snapshots…"
          type="search"
        />
        <kbd className="rounded-md border border-stroke-subtle bg-bg-container px-1.5 py-0.5 font-mono text-[11px] text-fg-caption">⌘K</kbd>
      </label>

      <div className="flex items-center gap-3">
        <div className="flex h-8 items-center gap-2 rounded-full border border-stroke-subtle bg-bg-container px-3 text-[12.5px] text-fg-caption">
          <StatusDot pulse={liveActivity.isConnected} status={dotStatus} />
          <span>{formatCount(liveActivity.clientCount)} clients · {formatRate(liveActivity.eventsPerSecond)} ev/s</span>
        </div>
        <button className="grid size-8 place-items-center rounded-lg text-fg-icon-subtle transition-colors hover:bg-bg-container hover:text-fg-body" type="button">
          <span className="sr-only">Notifications</span>
          <Bell aria-hidden="true" className="size-4" strokeWidth={1.8} />
        </button>
        <div className="grid size-7 place-items-center rounded-full bg-bg-chip-selected text-[12px] font-semibold text-fg-chip-selected">U</div>
      </div>
    </header>
  );
}

function formatCount(value: number) {
  return new Intl.NumberFormat(undefined, { maximumFractionDigits: 0 }).format(value);
}

function formatRate(value: number) {
  return new Intl.NumberFormat(undefined, { maximumFractionDigits: value < 10 ? 1 : 0 }).format(value);
}