import { RefreshCcw } from 'lucide-react';
import { useEffect, useMemo, useState } from 'react';
import { DiffLayoutToggle } from '@/components/tower/code/DiffLayoutToggle';
import { JsonDiff } from '@/components/tower/code/JsonDiff';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Skeleton } from '@/components/ui/skeleton';
import { Textarea } from '@/components/ui/textarea';
import { ApiError } from '@/api/client';
import { entriesToResolvedDocument } from '@/lib/snapshot-document';
import { deepEqual, diffDocuments, summarize, type ChangeSummary } from '@/lib/snapshot-diff';
import { usePublishSnapshot, useSnapshotDetail, useSnapshotPreview } from '@/queries/useSnapshots';
import { useTweaksStore } from '@/store/tweaks';

export { deepEqual };

interface PublishModalProps {
  activeSnapshotId?: string;
  onOpenChange: (open: boolean) => void;
  open: boolean;
  projectId: string;
}

type PublishStep = 'diff' | 'confirm';

export function PublishModal({ activeSnapshotId, onOpenChange, open, projectId }: PublishModalProps) {
  const [step, setStep] = useState<PublishStep>('diff');
  const [comment, setComment] = useState('');
  const [staleBanner, setStaleBanner] = useState(false);
  const masked = useTweaksStore((state) => state.sensitiveMasked);
  const diffLayout = useTweaksStore((state) => state.diffLayout);
  const activeSnapshot = useSnapshotDetail(projectId, activeSnapshotId);
  const preview = useSnapshotPreview(projectId, { enabled: open });
  const publishSnapshot = usePublishSnapshot(projectId);
  const before = useMemo(() => entriesToResolvedDocument(activeSnapshot.data?.entries, {}, { maskSensitive: masked }), [activeSnapshot.data?.entries, masked]);
  const after = useMemo(() => entriesToResolvedDocument(preview.data?.entries, {}, { maskSensitive: masked }), [preview.data?.entries, masked]);
  const loading = activeSnapshot.isLoading || preview.isLoading || preview.isFetching;
  const hasChanges = !loading && !deepEqual(before, after);
  const summary = useMemo(() => summarizeChanges(before, after), [after, before]);
  const previewError = preview.isError ? toErrorMessage(preview.error) : null;

  useEffect(() => {
    if (open) {
      setStep('diff');
      setComment('');
      setStaleBanner(false);
    }
  }, [open]);

  async function publish() {
    try {
      await publishSnapshot.mutateAsync({
        description: comment.trim() || null,
        expectedHash: preview.data?.diffHash ?? null,
        version: '0',
      });
      onOpenChange(false);
    } catch (error) {
      if (error instanceof ApiError && error.status === 409) {
        setStep('diff');
        setStaleBanner(true);
      }
    }
  }

  async function refreshPreview() {
    setStaleBanner(false);
    await preview.refetch();
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="w-[min(calc(100vw-32px),980px)]">
        <DialogHeader>
          <DialogTitle>{step === 'diff' ? 'Publish new snapshot' : 'Confirm publish'}</DialogTitle>
          <DialogDescription>{step === 'diff' ? 'Review the resolved configuration diff before creating an immutable snapshot.' : 'Add an optional comment and publish the snapshot.'}</DialogDescription>
        </DialogHeader>

        {staleBanner ? <StaleBanner onRefresh={() => void refreshPreview()} pending={preview.isFetching} /> : null}

        {step === 'diff' ? (
          previewError ? (
            <div className="rounded-xl border border-stroke-subtle bg-badge-critical-bg px-4 py-3 text-[12.5px] text-badge-critical-fg">
              {previewError}
            </div>
          ) : loading ? (
            <Skeleton className="h-[520px]" />
          ) : (
            <div className="grid gap-2">
              <div className="flex justify-end">
                <DiffLayoutToggle />
              </div>
              <JsonDiff after={after} before={before} className="max-h-[560px] overflow-y-auto border border-stroke-subtle bg-bg-container" mode={diffLayout} />
            </div>
          )
        ) : (
          <div className="grid gap-4">
            <div className="grid gap-3 rounded-xl border border-stroke-subtle bg-bg-container p-4 md:grid-cols-4">
              <Metric label="Entries" value={(preview.data?.entries.length ?? 0).toString()} />
              <Metric label="Additions" value={summary.additions.toString()} />
              <Metric label="Modifications" value={summary.modifications.toString()} />
              <Metric label="Deletions" value={summary.deletions.toString()} />
            </div>
            <div className="grid gap-1.5">
              <label className="text-[12px] font-medium text-fg-body" htmlFor="publish-comment">Comment</label>
              <Textarea id="publish-comment" maxLength={500} onChange={(event) => setComment(event.target.value)} placeholder="What changed in this publish?" value={comment} />
              <div className="text-right font-mono text-[11px] text-fg-caption">{comment.length}/500</div>
            </div>
          </div>
        )}

        <DialogFooter>
          {step === 'diff' ? (
            <>
              <Button onClick={() => onOpenChange(false)} type="button" variant="secondary">Cancel</Button>
              <Button disabled={!hasChanges || !preview.data || staleBanner} onClick={() => setStep('confirm')} type="button">Continue</Button>
            </>
          ) : (
            <>
              <Button onClick={() => setStep('diff')} type="button" variant="ghost">Back</Button>
              <Button disabled={publishSnapshot.isPending} onClick={() => void publish()} type="button">{publishSnapshot.isPending ? 'Publishing…' : 'Publish snapshot'}</Button>
            </>
          )}
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

function StaleBanner({ onRefresh, pending }: { onRefresh: () => void; pending: boolean }) {
  return (
    <div className="flex flex-wrap items-center gap-3 rounded-xl border border-stroke-subtle bg-badge-warning-bg px-4 py-3">
      <span className="flex-1 text-[12.5px] text-badge-warning-fg">
        Configuration changed while you were reviewing. Refresh the diff before publishing.
      </span>
      <Button disabled={pending} onClick={onRefresh} size="sm" type="button" variant="secondary">
        <RefreshCcw aria-hidden="true" className="size-3.5" strokeWidth={1.8} />
        {pending ? 'Refreshing…' : 'Refresh'}
      </Button>
    </div>
  );
}

function Metric({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-lg bg-bg-surface px-3 py-2">
      <div className="text-[11.5px] text-fg-caption">{label}</div>
      <div className="mt-1 font-mono text-[15px] font-semibold text-fg-heading">{value}</div>
    </div>
  );
}

export function summarizeChanges(before: Record<string, unknown>, after: Record<string, unknown>): ChangeSummary {
  return summarize(diffDocuments(before, after));
}

function toErrorMessage(error: unknown): string {
  if (error instanceof ApiError) {
    const detail = readProblemDetail(error.body);
    if (detail) {
      return detail;
    }

    return `Preview failed (${error.status}).`;
  }

  return 'Preview failed.';
}

function readProblemDetail(body: unknown): string | undefined {
  if (typeof body !== 'object' || body === null) {
    return undefined;
  }

  const detail = (body as { detail?: unknown }).detail;
  return typeof detail === 'string' ? detail : undefined;
}
