import { useEffect, useState } from 'react';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { InlineCode } from '@/components/tower/data/InlineCode';

interface PATRevealModalProps {
  onConfirm: () => void;
  open: boolean;
  rawToken: string;
}

export function PATRevealModal({ onConfirm, open, rawToken }: PATRevealModalProps) {
  const [copied, setCopied] = useState(false);
  const [copyFailed, setCopyFailed] = useState(false);
  const [confirmed, setConfirmed] = useState(false);

  useEffect(() => {
    if (open) {
      setCopied(false);
      setCopyFailed(false);
      setConfirmed(false);
    }
  }, [open]);

  useEffect(() => {
    if (!open || confirmed) {
      return;
    }

    function beforeUnload(event: BeforeUnloadEvent) {
      event.preventDefault();
      event.returnValue = '';
    }

    window.addEventListener('beforeunload', beforeUnload);

    return () => window.removeEventListener('beforeunload', beforeUnload);
  }, [confirmed, open]);

  async function copyToken() {
    try {
      await navigator.clipboard.writeText(rawToken);
      setCopied(true);
      setCopyFailed(false);
      window.setTimeout(() => setCopied(false), 2_000);
    } catch {
      setCopyFailed(true);
    }
  }

  return (
    <Dialog open={open} onOpenChange={(nextOpen) => { if (!nextOpen && confirmed) { onConfirm(); } }}>
      <DialogContent className="w-[min(calc(100vw-32px),680px)]" onEscapeKeyDown={(event) => { if (!confirmed) { event.preventDefault(); } }} onPointerDownOutside={(event) => { if (!confirmed) { event.preventDefault(); } }} showCloseButton={false}>
        <DialogHeader>
          <DialogTitle>Client Credential Created</DialogTitle>
          <DialogDescription>This is the only time you will see this token. Copy it now.</DialogDescription>
        </DialogHeader>
        <div className="grid gap-4">
          <div className="select-text overflow-auto rounded-xl border border-stroke-subtle bg-bg-container p-4 font-mono text-[12.5px]">
            <InlineCode>{rawToken}</InlineCode>
          </div>
          <Button onClick={() => void copyToken()} type="button" variant="secondary">{copied ? 'Copied!' : 'Copy token'}</Button>
          {copyFailed ? <div className="text-[12px] text-badge-critical-fg">Copy failed. Select the token and copy it manually.</div> : null}
        </div>
        <DialogFooter className="items-center justify-between gap-3 sm:justify-between">
          <label className="flex items-center gap-2 text-[13px] text-fg-body">
            <input checked={confirmed} className="size-4 accent-[var(--tower-stroke-field-focus)]" onChange={(event) => setConfirmed(event.target.checked)} type="checkbox" />
            I have copied this token
          </label>
          <Button disabled={!confirmed} onClick={onConfirm} type="button">Done</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}