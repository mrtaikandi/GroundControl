import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { JsonDiff } from '@/components/tower/code/JsonDiff';
import { Badge } from '@/components/tower/data/Badge';
import { formatDateTime } from '@/lib/date-time';
import { formatUserId } from '@/lib/user';
import type { AuditRecord } from '@/queries/useAuditRecords';

interface AuditDetailsDialogProps {
  onOpenChange: (open: boolean) => void;
  open: boolean;
  record: AuditRecord | null;
}

export function AuditDetailsDialog({ onOpenChange, open, record }: AuditDetailsDialogProps) {
  return (
    <Dialog onOpenChange={onOpenChange} open={open}>
      <DialogContent className="w-[min(calc(100vw-32px),960px)]" showMaximizeButton>
        <DialogHeader>
          <DialogTitle>Audit details</DialogTitle>
          <DialogDescription>{record ? `${record.entityType}.${record.action}` : 'Loading…'}</DialogDescription>
        </DialogHeader>
        {record ? (
          <div className="grid gap-4">
            <dl className="grid grid-cols-1 gap-3 sm:grid-cols-2">
              <Field label="Timestamp"><span className="font-mono text-[12.5px] text-fg-body">{formatDateTime(record.performedAt)}</span></Field>
              <Field label="Actor">{formatUserId(record.performedBy)}</Field>
              <Field label="Entity"><Badge variant="info">{record.entityType}</Badge></Field>
              <Field label="Entity ID">{record.entityId}</Field>
            </dl>
            <div className="grid gap-2">
              <div className="text-[11px] font-medium uppercase tracking-wide text-fg-caption">Changes</div>
              {record.changes.length === 0 ? (
                <div className="rounded-lg border border-dashed border-stroke-subtle bg-bg-container px-4 py-6 text-center text-[12.5px] text-fg-caption">No field-level changes recorded.</div>
              ) : (
                <JsonDiff after={changesToObject(record.changes, 'newValue')} before={changesToObject(record.changes, 'oldValue')} />
              )}
            </div>
          </div>
        ) : null}
      </DialogContent>
    </Dialog>
  );
}

function Field({ children, label }: { children: React.ReactNode; label: string }) {
  return (
    <div className="grid gap-1">
      <dt className="text-[11px] font-medium uppercase tracking-wide text-fg-caption">{label}</dt>
      <dd>{children}</dd>
    </div>
  );
}

function changesToObject(changes: AuditRecord['changes'], valueKey: 'newValue' | 'oldValue') {
  return Object.fromEntries(changes.map((change) => [change.field, change[valueKey] ?? null]));
}
