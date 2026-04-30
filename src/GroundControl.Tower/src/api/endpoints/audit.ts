import { apiFetch, type ApiQuery, type ApiResponse } from '../client';

export function getAuditRecords(query?: ApiQuery<'ListAuditRecordsHandler'>) {
  return apiFetch<ApiResponse<'ListAuditRecordsHandler'>>('/api/audit-records', { query });
}

export function getAuditRecord(id: string) {
  return apiFetch<ApiResponse<'GetAuditRecordHandler'>>(`/api/audit-records/${encodeURIComponent(id)}`);
}