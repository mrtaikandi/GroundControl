import { createFileRoute, useNavigate } from '@tanstack/react-router';
import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { SegmentedControl } from '@/components/tower/data/SegmentedControl';
import { ConfigFlatView } from '@/components/tower/config/ConfigFlatView';
import { ConfigJsonView } from '@/components/tower/config/ConfigJsonView';
import { ConfigTreeView } from '@/components/tower/config/ConfigTreeView';
import { ProjectPicker } from '@/components/tower/projects/ProjectPicker';
import { PublishModal } from '@/components/tower/snapshots/PublishModal';
import { useEffectiveEntries } from '@/queries/useEffectiveEntries';
import { useProjects } from '@/queries/useProjects';
import { useTweaksStore } from '@/store/tweaks';

export const Route = createFileRoute('/projects/$projectId/config')({
  component: ConfigRoute,
});

function ConfigRoute() {
  const { projectId } = Route.useParams();
  const navigate = useNavigate();
  const configViewMode = useTweaksStore((state) => state.configViewMode);
  const setConfigViewMode = useTweaksStore((state) => state.setConfigViewMode);
  const projects = useProjects();
  const project = projects.data?.data.find((candidate) => candidate.id === projectId);
  const activeSnapshotId = project?.activeSnapshotId || undefined;
  const effective = useEffectiveEntries(projectId);
  const [publishing, setPublishing] = useState(false);

  const summary = effective.isLoading
    ? null
    : buildSummary(effective.ownCount, effective.inheritedCount, effective.attachedTemplates.length, effective.overrideCount);

  return (
    <div className="grid gap-6">
      <div className="flex items-start justify-between gap-4">
        <div className="min-w-0">
          <div className="flex flex-wrap items-center gap-3">
            <h1 className="text-[34px] font-bold leading-tight text-fg-heading">Configuration</h1>
            <span aria-hidden="true" className="text-[20px] text-fg-caption">·</span>
            <ProjectPicker
              onChange={(nextId) => navigate({ params: { projectId: nextId }, to: '/projects/$projectId/config' })}
              projects={projects.data?.data ?? []}
              selectedId={projectId}
            />
          </div>
          {summary ? <p className="mt-1.5 text-[12.5px] text-fg-caption">{summary}</p> : null}
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

function buildSummary(ownCount: number, inheritedCount: number, templateCount: number, overrideCount: number): string {
  const parts: string[] = [];
  parts.push(`${ownCount} own ${ownCount === 1 ? 'entry' : 'entries'}`);

  if (templateCount > 0) {
    parts.push(`${inheritedCount} inherited from ${templateCount} ${templateCount === 1 ? 'template' : 'templates'}`);
  }

  if (overrideCount > 0) {
    parts.push(`${overrideCount} ${overrideCount === 1 ? 'override' : 'overrides'}`);
  }

  return parts.join(' · ');
}
