import { create } from 'zustand';
import { persist } from 'zustand/middleware';

export type Theme = 'light' | 'dark';
export type ConfigViewMode = 'flat' | 'tree' | 'json';
export type SnapshotViewMode = 'diff' | 'json' | 'json-diff';
export type DiffLayout = 'inline' | 'split';

interface TweaksState {
  applyToDocument: () => void;
  configViewMode: ConfigViewMode;
  diffLayout: DiffLayout;
  diffLineWrap: boolean;
  driftBannerVisible: boolean;
  sensitiveMasked: boolean;
  setConfigViewMode: (mode: ConfigViewMode) => void;
  setDiffLayout: (layout: DiffLayout) => void;
  setDiffLineWrap: (wrap: boolean) => void;
  setDriftBannerVisible: (visible: boolean) => void;
  setSensitiveMasked: (masked: boolean) => void;
  setSnapshotViewMode: (mode: SnapshotViewMode) => void;
  setTheme: (theme: Theme) => void;
  snapshotViewMode: SnapshotViewMode;
  theme: Theme;
}

export const useTweaksStore = create<TweaksState>()(
  persist(
    (set, get) => ({
      applyToDocument: () => applyToDocument(get().theme),
      configViewMode: 'flat',
      diffLayout: 'inline',
      diffLineWrap: true,
      driftBannerVisible: true,
      sensitiveMasked: true,
      setConfigViewMode: (configViewMode) => set({ configViewMode }),
      setDiffLayout: (diffLayout) => set({ diffLayout }),
      setDiffLineWrap: (diffLineWrap) => set({ diffLineWrap }),
      setDriftBannerVisible: (driftBannerVisible) => set({ driftBannerVisible }),
      setSensitiveMasked: (sensitiveMasked) => set({ sensitiveMasked }),
      setSnapshotViewMode: (snapshotViewMode) => set({ snapshotViewMode }),
      setTheme: (theme) => {
        set({ theme });
        applyToDocument(theme);
      },
      snapshotViewMode: 'diff',
      theme: 'light',
    }),
    {
      name: 'tower.tweaks',
      onRehydrateStorage: () => (state) => {
        state?.applyToDocument();
      },
    },
  ),
);

export function applyToDocument(theme: Theme) {
  if (typeof document === 'undefined') {
    return;
  }

  document.documentElement.dataset.towerTheme = theme;
}
