import { AlertTriangle, X } from 'lucide-react';
import { useTweaksStore } from '@/store/tweaks';

export function DriftBanner() {
  const driftBannerVisible = useTweaksStore((state) => state.driftBannerVisible);
  const setDriftBannerVisible = useTweaksStore((state) => state.setDriftBannerVisible);

  if (!driftBannerVisible) {
    return null;
  }

  return (
    <div aria-live="polite" className="mb-6 flex items-center gap-3 rounded-lg border border-badge-warning-fg bg-badge-warning-bg px-4 py-3 text-[13px] text-badge-warning-fg" role="alert">
      <AlertTriangle aria-hidden="true" className="size-4 shrink-0" strokeWidth={1.8} />
      <div className="min-w-0 flex-1">Snapshot drift detected — the active snapshot does not match the current entry state. Publish a new snapshot to bring clients in sync.</div>
      <button className="grid size-7 shrink-0 place-items-center rounded-lg hover:bg-bg-surface/50" onClick={() => setDriftBannerVisible(false)} type="button">
        <span className="sr-only">Dismiss drift banner</span>
        <X aria-hidden="true" className="size-4" strokeWidth={1.8} />
      </button>
    </div>
  );
}