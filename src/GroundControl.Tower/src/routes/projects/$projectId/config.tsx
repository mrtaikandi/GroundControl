import { createFileRoute } from '@tanstack/react-router';
import { SegmentedControl } from '@/components/tower/data/SegmentedControl';
import { ConfigFlatView } from '@/components/tower/config/ConfigFlatView';
import { useTweaksStore } from '@/store/tweaks';

export const Route = createFileRoute('/projects/$projectId/config')({
  component: ConfigRoute,
});

function ConfigRoute() {
  const { projectId } = Route.useParams();
  const configViewMode = useTweaksStore((state) => state.configViewMode);
  const setConfigViewMode = useTweaksStore((state) => state.setConfigViewMode);

  return (
    <div className="grid gap-8">
      <div className="flex items-start justify-between gap-4">
        <div>
          <div className="text-[11px] font-medium uppercase text-fg-caption">GET /client/config</div>
          <h1 className="mt-2 text-[34px] font-bold leading-tight text-fg-heading">Configuration</h1>
          <p className="mt-2 text-[14.5px] text-fg-caption">Browse and edit entries for this project</p>
        </div>
        <SegmentedControl onChange={setConfigViewMode} options={[{ label: 'Flat', value: 'flat' }, { label: 'Tree', value: 'tree' }, { label: 'JSON', value: 'json' }]} value={configViewMode} />
      </div>

      <ConfigFlatView projectId={projectId} />
    </div>
  );
}
