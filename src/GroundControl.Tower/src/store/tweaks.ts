import { create } from 'zustand';
import { persist } from 'zustand/middleware';

export type Theme = 'light' | 'dark';
export type Accent = 'violet' | 'success' | 'warning' | 'critical';
export type Density = 'comfortable' | 'compact';
export type ConfigViewMode = 'flat' | 'tree' | 'json';
export type SnapshotViewMode = 'diff' | 'json' | 'json-diff';

interface TweaksState {
  accent: Accent;
  applyToDocument: () => void;
  configViewMode: ConfigViewMode;
  density: Density;
  driftBannerVisible: boolean;
  sensitiveMasked: boolean;
  setAccent: (accent: Accent) => void;
  setConfigViewMode: (mode: ConfigViewMode) => void;
  setDensity: (density: Density) => void;
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
      accent: 'violet',
      applyToDocument: () => applyToDocument(get().theme, get().density, get().accent),
      configViewMode: 'flat',
      density: 'comfortable',
      driftBannerVisible: true,
      sensitiveMasked: true,
      setAccent: (accent) => {
        set({ accent });
        applyToDocument(get().theme, get().density, accent);
      },
      setConfigViewMode: (configViewMode) => set({ configViewMode }),
      setDensity: (density) => {
        set({ density });
        applyToDocument(get().theme, density, get().accent);
      },
      setDriftBannerVisible: (driftBannerVisible) => set({ driftBannerVisible }),
      setSensitiveMasked: (sensitiveMasked) => set({ sensitiveMasked }),
      setSnapshotViewMode: (snapshotViewMode) => set({ snapshotViewMode }),
      setTheme: (theme) => {
        set({ theme });
        applyToDocument(theme, get().density, get().accent);
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

export function applyToDocument(theme: Theme, density: Density, accent: Accent) {
  if (typeof document === 'undefined') {
    return;
  }

  document.documentElement.dataset.towerTheme = theme;
  document.documentElement.dataset.density = density;
  document.documentElement.dataset.accent = accent;
}