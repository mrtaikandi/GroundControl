import { createFileRoute } from '@tanstack/react-router';
import { Braces, FolderTree, List } from 'lucide-react';
import { SegmentedControl } from '@/components/tower/data/SegmentedControl';
import { ConfigFlatView } from '@/components/tower/config/ConfigFlatView';
import { ConfigJsonView } from '@/components/tower/config/ConfigJsonView';
import { ConfigTreeView } from '@/components/tower/config/ConfigTreeView';
import { TemplateAttachmentsBar } from '@/components/tower/projects/TemplateAttachmentsBar';
import { useTweaksStore } from '@/store/tweaks';

export const Route = createFileRoute('/projects/$projectId/config')({
  component: ConfigRoute,
});

function ConfigRoute() {
  const { projectId } = Route.useParams();
  const configViewMode = useTweaksStore((state) => state.configViewMode);
  const setConfigViewMode = useTweaksStore((state) => state.setConfigViewMode);

  return (
    <div className="grid gap-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h2 className="text-[19px] font-semibold text-fg-heading">Configuration</h2>
          <p className="mt-1 text-[12.5px] text-fg-caption">Manage the entries that resolve into the next snapshot.</p>
        </div>
        <SegmentedControl
          onChange={setConfigViewMode}
          options={[{ icon: List, label: 'Flat', value: 'flat' }, { icon: FolderTree, label: 'Tree', value: 'tree' }, { icon: Braces, label: 'JSON', value: 'json' }]}
          value={configViewMode}
        />
      </div>

      <TemplateAttachmentsBar projectId={projectId} />

      {configViewMode === 'tree' ? <ConfigTreeView projectId={projectId} /> : configViewMode === 'json' ? <ConfigJsonView projectId={projectId} /> : <ConfigFlatView projectId={projectId} />}
    </div>
  );
}
