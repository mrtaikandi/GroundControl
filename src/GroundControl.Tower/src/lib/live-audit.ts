import type { LiveAuditRecord } from './sse';

export const maxLiveAuditRecords = 20;

export function prependLiveAuditRecord(records: LiveAuditRecord[], record: LiveAuditRecord, limit = maxLiveAuditRecords) {
  return [record, ...records.filter((candidate) => candidate.id !== record.id)].slice(0, limit);
}