import { createContext, useContext, type ReactNode } from 'react';
import { useTweaksStore } from '@/store/tweaks';

interface SensitiveContextValue {
  masked: boolean;
}

const SensitiveContext = createContext<SensitiveContextValue | undefined>(undefined);

export function SensitiveProvider({ children }: { children: ReactNode }) {
  const masked = useTweaksStore((state) => state.sensitiveMasked);

  return <SensitiveContext.Provider value={{ masked }}>{children}</SensitiveContext.Provider>;
}

export function useSensitive() {
  const context = useContext(SensitiveContext);

  if (!context) {
    throw new Error('useSensitive must be used inside SensitiveProvider.');
  }

  return context;
}