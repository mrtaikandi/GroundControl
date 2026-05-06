import { describe, expect, it } from 'vitest';
import { prependLiveAuditRecord } from './live-audit';
import type { LiveAuditRecord } from './sse';

describe('prependLiveAuditRecord', () => {
  it('keeps the live audit list capped at 20 items', () => {
    const existing = Array.from({ length: 20 }, (_, index) => createRecord(`record-${index}`));
    const next = prependLiveAuditRecord(existing, createRecord('record-new'));

    expect(next).toHaveLength(20);
    expect(next[0]?.id).toBe('record-new');
    expect(next.at(-1)?.id).toBe('record-18');
  });

  it('moves duplicate records to the front', () => {
    const existing = [createRecord('record-1'), createRecord('record-2')];
    const next = prependLiveAuditRecord(existing, { ...existing[1]!, action: 'Updated' });

    expect(next).toHaveLength(2);
    expect(next[0]?.id).toBe('record-2');
    expect(next[0]?.action).toBe('Updated');
  });
});

function createRecord(id: string): LiveAuditRecord {
  return {
    action: 'Created',
    changes: [],
    entityId: id,
    entityType: 'Project',
    id,
    performedAt: new Date().toISOString(),
    performedBy: '00000000-0000-0000-0000-000000000000',
  };
}