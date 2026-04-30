import { JsonPreview } from '@/components/tower/code/JsonPreview';
import { Skeleton } from '@/components/ui/skeleton';
import { snapshotToDocument } from '@/lib/snapshot-document';
import type { SnapshotDetail } from '@/queries/useSnapshots';

interface SnapshotJsonViewProps {
  isLoading?: boolean;
  snapshot?: SnapshotDetail;
}

export function SnapshotJsonView({ isLoading = false, snapshot }: SnapshotJsonViewProps) {
  if (isLoading) {
    return <Skeleton className="h-[520px]" />;
  }

  return <JsonPreview className="min-h-[520px] border border-stroke-subtle bg-bg-container" maxHeight="620px" value={snapshotToDocument(snapshot)} />;
}