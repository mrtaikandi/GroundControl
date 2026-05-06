import { Columns2, Rows2 } from 'lucide-react';
import { SegmentedControl } from '@/components/tower/data/SegmentedControl';
import { useTweaksStore, type DiffLayout } from '@/store/tweaks';

interface DiffLayoutToggleProps {
  size?: 'md' | 'sm';
}

const options = [
  { icon: Rows2, label: 'Inline', value: 'inline' },
  { icon: Columns2, label: 'Split', value: 'split' },
] as const satisfies ReadonlyArray<{ icon: typeof Rows2; label: string; value: DiffLayout }>;

export function DiffLayoutToggle({ size = 'md' }: DiffLayoutToggleProps) {
  const layout = useTweaksStore((state) => state.diffLayout);
  const setLayout = useTweaksStore((state) => state.setDiffLayout);

  return <SegmentedControl onChange={setLayout} options={[...options]} size={size} value={layout} />;
}
