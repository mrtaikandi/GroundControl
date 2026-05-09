import { Download } from 'lucide-react';
import { useEffect, useMemo, useState } from 'react';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { JsonPreview } from '@/components/tower/code/JsonPreview';
import { SegmentedControl } from '@/components/tower/data/SegmentedControl';
import { useSensitive } from '@/lib/sensitive';
import { entriesToResolvedDocument } from '@/lib/snapshot-document';
import { useConfigEntries } from '@/queries/useConfigEntries';
import type { ConfigOwner } from '@/queries/useEffectiveEntries';
import { useScopes } from '@/queries/useScopes';
import { useSnapshotPreview } from '@/queries/useSnapshots';

interface ConfigJsonViewProps {
  owner: ConfigOwner;
}

export function ConfigJsonView({ owner }: ConfigJsonViewProps) {
  const scopes = useScopes();
  const scopeDefinitions = useMemo(() => scopes.data?.data.filter((scope) => scope.allowedValues.length > 0) ?? [], [scopes.data?.data]);
  const [selectedScopes, setSelectedScopes] = useState<Record<string, string>>({});
  const projectPreview = useSnapshotPreview(owner.kind === 'project' ? owner.id : '', { enabled: owner.kind === 'project' });
  const templateEntries = useConfigEntries(owner.kind === 'template' ? owner.id : '', 0);
  const { masked } = useSensitive();

  const sourceEntries = owner.kind === 'project' ? projectPreview.data?.entries : templateEntries.data?.data;
  const sourceLoading = owner.kind === 'project' ? projectPreview.isLoading : templateEntries.isLoading;
  const entryCount = sourceEntries?.length ?? 0;

  const resolvedDocument = useMemo(
    () => entriesToResolvedDocument(sourceEntries, selectedScopes, { maskSensitive: masked }),
    [masked, selectedScopes, sourceEntries],
  );

  useEffect(() => {
    setSelectedScopes((current) => {
      const nextEntries = scopeDefinitions.map((scope) => [scope.dimension, current[scope.dimension] ?? scope.allowedValues[0]!] as const);
      const next = Object.fromEntries(nextEntries);

      return shallowEqual(current, next) ? current : next;
    });
  }, [scopeDefinitions]);

  function selectScope(dimension: string, value: string) {
    setSelectedScopes((current) => ({ ...current, [dimension]: value }));
  }

  function exportDocument() {
    const blob = new Blob([JSON.stringify(resolvedDocument, null, 2)], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');

    link.href = url;
    link.download = `resolved-config-${owner.kind}-${owner.id}.json`;
    link.click();
    window.setTimeout(() => URL.revokeObjectURL(url), 100);
  }

  if (scopes.isLoading) {
    return <Skeleton className="h-[520px]" />;
  }

  return (
    <div className="grid gap-4">
      <div className="flex flex-wrap items-start justify-between gap-4 rounded-xl border border-stroke-subtle bg-bg-container p-4">
        <div className="flex flex-wrap gap-4">
          {scopeDefinitions.length === 0 ? <div className="text-[12px] text-fg-caption">No scope dimensions available.</div> : null}
          {scopeDefinitions.map((scope) => (
            <div className="grid gap-1.5" key={scope.id}>
              <div className="font-mono text-[11px] uppercase text-fg-caption">{scope.dimension}</div>
              <SegmentedControl onChange={(value) => selectScope(scope.dimension, value)} options={scope.allowedValues.map((value) => ({ label: value, value }))} size="sm" value={selectedScopes[scope.dimension] ?? scope.allowedValues[0]!} />
            </div>
          ))}
        </div>
        <div className="flex flex-wrap items-center justify-end gap-3">
          <Button disabled={sourceLoading} onClick={exportDocument} type="button" variant="secondary">
            <Download aria-hidden="true" className="size-3.5" />
            Export
          </Button>
        </div>
      </div>

      {sourceLoading ? (
        <Skeleton className="h-[460px]" />
      ) : (
        <div className="relative">
          <div className="absolute right-4 top-4 z-10 rounded-full bg-bg-container px-2.5 py-1 font-mono text-[10.5px] uppercase text-fg-caption">read-only preview</div>
          <JsonPreview className="min-h-[460px] border border-stroke-subtle bg-bg-container pt-12" maxHeight="620px" value={resolvedDocument} />
        </div>
      )}

      <div className="font-mono text-[11.5px] text-fg-caption">
        {entryCount} entries resolved · sensitive values {masked ? 'masked' : 'shown'} · change scope dimensions above to preview what different clients see.
      </div>
    </div>
  );
}

function shallowEqual(left: Record<string, string>, right: Record<string, string>) {
  const leftEntries = Object.entries(left);
  const rightEntries = Object.entries(right);

  return leftEntries.length === rightEntries.length && leftEntries.every(([key, value]) => right[key] === value);
}
