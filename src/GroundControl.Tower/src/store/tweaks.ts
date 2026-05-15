import { create } from 'zustand';
import { persist } from 'zustand/middleware';

export type Theme = 'light' | 'dark';
export type ConfigViewMode = 'list' | 'tree' | 'json';
export type SnapshotViewMode = 'compare' | 'json';
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
      configViewMode: 'list',
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
      snapshotViewMode: 'compare',
      theme: 'light',
    }),
    {
      name: 'tower.tweaks',
      // v1: configViewMode 'flat' renamed to 'list'.
      // v2: snapshotViewMode 'diff'|'json-diff' collapsed into 'compare'.
      version: 2,
      migrate: (persistedState, version) => {
        if (version < 1 && persistedState && typeof persistedState === 'object' && (persistedState as { configViewMode?: string }).configViewMode === 'flat') {
          (persistedState as { configViewMode: ConfigViewMode }).configViewMode = 'list';
        }

        if (version < 2 && persistedState && typeof persistedState === 'object') {
          const legacy = (persistedState as { snapshotViewMode?: string }).snapshotViewMode;
          if (legacy === 'diff' || legacy === 'json-diff') {
            (persistedState as { snapshotViewMode: SnapshotViewMode }).snapshotViewMode = 'compare';
          }
        }

        return persistedState as TweaksState;
      },
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
