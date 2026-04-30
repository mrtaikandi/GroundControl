import { useTweaksStore, type Accent, type ConfigViewMode, type Density, type SnapshotViewMode, type Theme } from '@/store/tweaks';
import { Settings2, X } from 'lucide-react';
import { useState, type ReactNode } from 'react';

interface Option<TValue extends string> {
  label: string;
  value: TValue;
}

const themeOptions: Option<Theme>[] = [
  { label: 'Light', value: 'light' },
  { label: 'Dark', value: 'dark' },
];

const accentOptions: Array<Option<Accent> & { className: string }> = [
  { className: 'bg-stroke-field-focus', label: 'Violet', value: 'violet' },
  { className: 'bg-badge-success-fg', label: 'Green', value: 'success' },
  { className: 'bg-badge-warning-fg', label: 'Amber', value: 'warning' },
  { className: 'bg-badge-critical-fg', label: 'Red', value: 'critical' },
];

const densityOptions: Option<Density>[] = [
  { label: 'Comfortable', value: 'comfortable' },
  { label: 'Compact', value: 'compact' },
];

const sensitiveOptions: Option<'masked' | 'shown'>[] = [
  { label: 'Masked', value: 'masked' },
  { label: 'Shown', value: 'shown' },
];

const configViewOptions: Option<ConfigViewMode>[] = [
  { label: 'Flat', value: 'flat' },
  { label: 'Tree', value: 'tree' },
  { label: 'JSON', value: 'json' },
];

const snapshotViewOptions: Option<SnapshotViewMode>[] = [
  { label: 'Diff', value: 'diff' },
  { label: 'JSON', value: 'json' },
  { label: 'JSON diff', value: 'json-diff' },
];

export function TweaksPanel() {
  const [open, setOpen] = useState(false);
  const accent = useTweaksStore((state) => state.accent);
  const configViewMode = useTweaksStore((state) => state.configViewMode);
  const density = useTweaksStore((state) => state.density);
  const driftBannerVisible = useTweaksStore((state) => state.driftBannerVisible);
  const sensitiveMasked = useTweaksStore((state) => state.sensitiveMasked);
  const setAccent = useTweaksStore((state) => state.setAccent);
  const setConfigViewMode = useTweaksStore((state) => state.setConfigViewMode);
  const setDensity = useTweaksStore((state) => state.setDensity);
  const setDriftBannerVisible = useTweaksStore((state) => state.setDriftBannerVisible);
  const setSensitiveMasked = useTweaksStore((state) => state.setSensitiveMasked);
  const setSnapshotViewMode = useTweaksStore((state) => state.setSnapshotViewMode);
  const setTheme = useTweaksStore((state) => state.setTheme);
  const snapshotViewMode = useTweaksStore((state) => state.snapshotViewMode);
  const theme = useTweaksStore((state) => state.theme);

  return (
    <div className="fixed bottom-5 right-5 z-50 flex flex-col items-end gap-3">
      {open ? (
        <section className="w-[340px] rounded-2xl border border-stroke-subtle bg-bg-surface p-4 text-[12.5px] shadow-[0_18px_40px_-16px_rgba(0,0,40,.25)]">
          <div className="mb-4 flex items-center justify-between gap-3">
            <div>
              <div className="text-[13px] font-semibold text-fg-heading">Tweaks</div>
              <div className="text-[11.5px] text-fg-caption">tower.tweaks</div>
            </div>
            <button className="grid size-8 place-items-center rounded-lg text-fg-icon-subtle hover:bg-bg-container hover:text-fg-body" onClick={() => setOpen(false)} type="button">
              <span className="sr-only">Close tweaks</span>
              <X aria-hidden="true" className="size-4" strokeWidth={1.8} />
            </button>
          </div>

          <div className="space-y-3">
            <ControlRow label="Accent">
              <div className="flex gap-1.5">
                {accentOptions.map((option) => (
                  <button
                    className={`grid size-7 place-items-center rounded-full border ${option.value === accent ? 'border-stroke-field-focus' : 'border-stroke-subtle'}`}
                    key={option.value}
                    onClick={() => setAccent(option.value)}
                    type="button"
                  >
                    <span className="sr-only">{option.label}</span>
                    <span aria-hidden="true" className={`size-4 rounded-full ${option.className}`} />
                  </button>
                ))}
              </div>
            </ControlRow>
            <ControlRow label="Theme">
              <SegmentedControl onChange={setTheme} options={themeOptions} value={theme} />
            </ControlRow>
            <ControlRow label="Density">
              <SegmentedControl onChange={setDensity} options={densityOptions} value={density} />
            </ControlRow>
            <ControlRow label="Sensitive values">
              <SegmentedControl onChange={(value) => setSensitiveMasked(value === 'masked')} options={sensitiveOptions} value={sensitiveMasked ? 'masked' : 'shown'} />
            </ControlRow>
            <ControlRow label="Drift banner">
              <button
                className={`h-7 rounded-full px-3 text-[12px] font-medium ${driftBannerVisible ? 'bg-bg-chip-selected text-fg-chip-selected' : 'bg-bg-container text-fg-caption'}`}
                onClick={() => setDriftBannerVisible(!driftBannerVisible)}
                type="button"
              >
                {driftBannerVisible ? 'On' : 'Off'}
              </button>
            </ControlRow>
            <ControlRow label="Config view mode">
              <SegmentedControl onChange={setConfigViewMode} options={configViewOptions} value={configViewMode} />
            </ControlRow>
            <ControlRow label="Snapshot view mode">
              <SegmentedControl onChange={setSnapshotViewMode} options={snapshotViewOptions} value={snapshotViewMode} />
            </ControlRow>
          </div>
        </section>
      ) : null}

      <button className="grid size-10 place-items-center rounded-full border border-stroke-subtle bg-bg-surface text-fg-body shadow-[0_18px_40px_-16px_rgba(0,0,40,.25)] hover:bg-bg-container" onClick={() => setOpen(!open)} type="button">
        <span className="sr-only">Open tweaks</span>
        <Settings2 aria-hidden="true" className="size-4" strokeWidth={1.8} />
      </button>
    </div>
  );
}

function ControlRow({ children, label }: { children: ReactNode; label: string }) {
  return (
    <div className="flex items-center justify-between gap-4">
      <div className="text-[12px] font-medium text-fg-body">{label}</div>
      {children}
    </div>
  );
}

function SegmentedControl<TValue extends string>({ onChange, options, value }: { onChange: (value: TValue) => void; options: Option<TValue>[]; value: TValue }) {
  return (
    <div className="inline-flex rounded-full bg-bg-container p-0.5">
      {options.map((option) => (
        <button
          className={`h-7 rounded-full px-3 text-[12px] transition-colors ${option.value === value ? 'bg-bg-surface text-fg-heading' : 'text-fg-caption hover:text-fg-body'}`}
          key={option.value}
          onClick={() => onChange(option.value)}
          type="button"
        >
          {option.label}
        </button>
      ))}
    </div>
  );
}