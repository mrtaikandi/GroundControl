import { createFileRoute } from '@tanstack/react-router';
import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { SegmentedControl } from '@/components/tower/data/SegmentedControl';
import { ConfigFlatView } from '@/components/tower/config/ConfigFlatView';
import { ConfigJsonView } from '@/components/tower/config/ConfigJsonView';
import { ConfigTreeView } from '@/components/tower/config/ConfigTreeView';
import { PublishModal } from '@/components/tower/snapshots/PublishModal';
import { useProjects } from '@/queries/useProjects';
import { useTweaksStore } from '@/store/tweaks';

export const Route = createFileRoute('/projects/$projectId/config')({
  component: ConfigRoute,
});

function ConfigRoute() {
  const { projectId } = Route.useParams();
  const configViewMode = useTweaksStore((state) => state.configViewMode);
  const setConfigViewMode = useTweaksStore((state) => state.setConfigViewMode);
  const projects = useProjects();
  const project = projects.data?.data.find((candidate) => candidate.id === projectId);
  const activeSnapshotId = project?.activeSnapshotId || undefined;
  const [publishing, setPublishing] = useState(false);

  return (
    <div className="grid gap-8">
      <div className="flex items-start justify-between gap-4">
        <div>
          <h1 className="text-[34px] font-bold leading-tight text-fg-heading">Configuration</h1>
          <p className="mt-2 text-[14.5px] text-fg-caption">Browse and edit entries for this project</p>
        </div>
        <div className="flex flex-wrap items-center justify-end gap-3">
          <SegmentedControl onChange={setConfigViewMode} options={[{ label: 'Flat', value: 'flat' }, { label: 'Tree', value: 'tree' }, { label: 'JSON', value: 'json' }]} value={configViewMode} />
          <Button onClick={() => setPublishing(true)} type="button">Publish snapshot</Button>
        </div>
      </div>

      {configViewMode === 'tree' ? <ConfigTreeView projectId={projectId} /> : configViewMode === 'json' ? <ConfigJsonView projectId={projectId} /> : <ConfigFlatView projectId={projectId} />}

      <PublishModal activeSnapshotId={activeSnapshotId} onOpenChange={setPublishing} open={publishing} projectId={projectId} />
    </div>
  );
}
