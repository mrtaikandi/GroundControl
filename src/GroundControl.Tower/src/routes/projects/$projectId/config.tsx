import { createFileRoute, Link, useNavigate } from '@tanstack/react-router';
import { Braces, List, FolderTree } from 'lucide-react';
import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { SegmentedControl } from '@/components/tower/data/SegmentedControl';
import { ConfigFlatView } from '@/components/tower/config/ConfigFlatView';
import { ConfigJsonView } from '@/components/tower/config/ConfigJsonView';
import { ConfigTreeView } from '@/components/tower/config/ConfigTreeView';
import { ProjectPicker } from '@/components/tower/projects/ProjectPicker';
import { TemplateAttachmentsBar } from '@/components/tower/projects/TemplateAttachmentsBar';
import { PublishModal } from '@/components/tower/snapshots/PublishModal';
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
  const [publishing, setPublishing] = useState(false);

  return (
    <div className="grid gap-6">
      <div className="flex items-start justify-between gap-4">
        <div className="min-w-0">
          <Link className="text-[12.5px] text-fg-caption transition-colors hover:text-fg-body" params={{ projectId }} to="/projects/$projectId">
            ← {project?.name ?? 'project'}
          </Link>
          <div className="mt-2 flex flex-wrap items-center gap-3">
            <h1 className="text-[34px] font-bold leading-tight text-fg-heading">Configuration</h1>
            <span aria-hidden="true" className="text-[20px] text-fg-caption">·</span>
            <ProjectPicker
              onChange={(nextId) => navigate({ params: { projectId: nextId }, to: '/projects/$projectId/config' })}
              projects={projects.data?.data ?? []}
              selectedId={projectId}
            />
          </div>
          <p className="mt-2 text-[14.5px] text-fg-caption">Manage project configurations.</p>
        </div>
        <div className="flex flex-wrap items-center justify-end gap-3">
          <SegmentedControl onChange={setConfigViewMode} options={[{ icon: List, label: 'Flat', value: 'flat' }, { icon: FolderTree, label: 'Tree', value: 'tree' }, { icon: Braces, label: 'JSON', value: 'json' }]} value={configViewMode} />
          <Button onClick={() => setPublishing(true)} type="button">Publish snapshot</Button>
        </div>
      </div>

      <TemplateAttachmentsBar projectId={projectId} />

      {configViewMode === 'tree' ? <ConfigTreeView projectId={projectId} /> : configViewMode === 'json' ? <ConfigJsonView projectId={projectId} /> : <ConfigFlatView projectId={projectId} />}

      <PublishModal activeSnapshotId={activeSnapshotId} onOpenChange={setPublishing} open={publishing} projectId={projectId} />
    </div>
  );
}

