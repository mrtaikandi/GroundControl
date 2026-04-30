import { useEffect, useMemo, useState } from 'react';
import { JsonDiff } from '@/components/tower/code/JsonDiff';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Skeleton } from '@/components/ui/skeleton';
import { Textarea } from '@/components/ui/textarea';
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from '@/components/ui/tooltip';
import { buildResolvedDocument } from '@/lib/resolve-config';
import { snapshotToResolvedDocument } from '@/lib/snapshot-document';
import { useResolvedConfig } from '@/queries/useResolvedConfig';
import { usePublishSnapshot, useSnapshotDetail } from '@/queries/useSnapshots';
import { useTweaksStore } from '@/store/tweaks';

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
              <Metric label="Entries" value={flattenDocument(after).size.toString()} />
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

export function deepEqual(left: unknown, right: unknown) {
  return JSON.stringify(left) === JSON.stringify(right);
}

export function summarizeChanges(before: Record<string, unknown>, after: Record<string, unknown>) {
  const beforeFlat = flattenDocument(before);
  const afterFlat = flattenDocument(after);
  let additions = 0;
  let modifications = 0;
  let deletions = 0;

  for (const [key, value] of afterFlat) {
    if (!beforeFlat.has(key)) {
      additions += 1;
    } else if (!deepEqual(beforeFlat.get(key), value)) {
      modifications += 1;
    }
  }

  for (const key of beforeFlat.keys()) {
    if (!afterFlat.has(key)) {
      deletions += 1;
    }
  }

  return { additions, deletions, modifications };
}

function flattenDocument(value: unknown, prefix = ''): Map<string, unknown> {
  if (!isRecord(value)) {
    return new Map([[prefix, value]]);
  }

  const flattened = new Map<string, unknown>();

  for (const [key, child] of Object.entries(value)) {
    const path = prefix ? `${prefix}.${key}` : key;

    if (isRecord(child)) {
      for (const [childKey, childValue] of flattenDocument(child, path)) {
        flattened.set(childKey, childValue);
      }
    } else {
      flattened.set(path, child);
    }
  }

  return flattened;
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}