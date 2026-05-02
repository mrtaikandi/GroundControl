import { useEffect, useMemo, useState } from 'react';
import { JsonDiff } from '@/components/tower/code/JsonDiff';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Skeleton } from '@/components/ui/skeleton';
import { Textarea } from '@/components/ui/textarea';
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from '@/components/ui/tooltip';
import { buildResolvedDocument } from '@/lib/resolve-config';
import { snapshotToResolvedDocument } from '@/lib/snapshot-document';
import { deepEqual, diffDocuments, summarize, type ChangeSummary } from '@/lib/snapshot-diff';
import { useResolvedConfig } from '@/queries/useResolvedConfig';
import { usePublishSnapshot, useSnapshotDetail } from '@/queries/useSnapshots';
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
  const masked = useTweaksStore((state) => state.sensitiveMasked);
  const activeSnapshot = useSnapshotDetail(projectId, activeSnapshotId);
  const resolvedConfig = useResolvedConfig(projectId, {});
  const publishSnapshot = usePublishSnapshot(projectId);
  const before = useMemo(() => snapshotToResolvedDocument(activeSnapshot.data, {}, { maskSensitive: masked }), [activeSnapshot.data, masked]);
  const after = useMemo(() => buildResolvedDocument(resolvedConfig.data ?? [], { maskSensitive: masked }), [masked, resolvedConfig.data]);
  const loading = activeSnapshot.isLoading || resolvedConfig.isLoading;
  const hasChanges = !loading && !deepEqual(before, after);
  const summary = useMemo(() => summarizeChanges(before, after), [after, before]);

  useEffect(() => {
    if (open) {
      setStep('diff');
      setComment('');
    }
  }, [open]);

  async function publish() {
    await publishSnapshot.mutateAsync({ description: comment.trim() || null, version: '0' });
    onOpenChange(false);
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="w-[min(calc(100vw-32px),980px)]">
        <DialogHeader>
          <DialogTitle>{step === 'diff' ? 'Publish new snapshot' : 'Confirm publish'}</DialogTitle>
          <DialogDescription>{step === 'diff' ? 'Review the resolved configuration diff before creating an immutable snapshot.' : 'Add an optional comment and publish the snapshot.'}</DialogDescription>
        </DialogHeader>

        {step === 'diff' ? (
          loading ? <Skeleton className="h-[520px]" /> : <JsonDiff after={after} before={before} className="max-h-[560px] border border-stroke-subtle bg-bg-container" mode="split" />
        ) : (
          <div className="grid gap-4">
            <div className="grid gap-3 rounded-xl border border-stroke-subtle bg-bg-container p-4 md:grid-cols-4">
              <Metric label="Entries" value={flattenSize(after).toString()} />
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
              <TooltipProvider>
                <Tooltip>
                  <TooltipTrigger asChild>
                    <span><Button disabled={!hasChanges} onClick={() => setStep('confirm')} type="button">Continue</Button></span>
                  </TooltipTrigger>
                  {!hasChanges ? <TooltipContent>No changes to publish</TooltipContent> : null}
                </Tooltip>
              </TooltipProvider>
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

function flattenSize(document: Record<string, unknown>): number {
  return countLeaves(document);
}

function countLeaves(value: unknown): number {
  if (typeof value !== 'object' || value === null || Array.isArray(value)) {
    return 1;
  }

  let total = 0;
  for (const child of Object.values(value as Record<string, unknown>)) {
    total += countLeaves(child);
  }

  return total;
}