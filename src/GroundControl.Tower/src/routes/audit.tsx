import { createColumnHelper, flexRender, getCoreRowModel, useReactTable, type Row } from '@tanstack/react-table';
import { createFileRoute } from '@tanstack/react-router';
import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Skeleton } from '@/components/ui/skeleton';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { JsonDiff } from '@/components/tower/code/JsonDiff';
import { Badge } from '@/components/tower/data/Badge';
import { FilterChip } from '@/components/tower/data/FilterChip';
import { InlineCode } from '@/components/tower/data/InlineCode';
import { useAuditRecords, type AuditRecord } from '@/queries/useAuditRecords';
import { formatUserId } from '@/lib/user';

const columnHelper = createColumnHelper<AuditRecord>();

export const Route = createFileRoute('/audit')({
  component: AuditRoute,
});

function AuditRoute() {
  const [entityTypes, setEntityTypes] = useState<string[]>([]);
  const [from, setFrom] = useState('');
  const [to, setTo] = useState('');
  const [expandedIds, setExpandedIds] = useState<Set<string>>(() => new Set());
  const dateRangeInvalid = !!from && !!to && from > to;
  const audit = useAuditRecords({ enabled: !dateRangeInvalid, entityTypes, from, to });
  const rows = useMemo(() => audit.data?.pages.flatMap((page) => page.data) ?? [], [audit.data]);
  const observerRef = useRef<IntersectionObserver | null>(null);

  useEffect(() => () => observerRef.current?.disconnect(), []);

  useEffect(() => {
    setExpandedIds(new Set());
  }, [entityTypes, from, to]);

  useEffect(() => {
    if (entityTypes.length > 1 && audit.hasNextPage && !audit.isFetchingNextPage && rows.length < 20) {
      void audit.fetchNextPage();
    }
  }, [audit, entityTypes.length, rows.length]);

  const lastRowRef = useCallback((node: HTMLTableRowElement | null) => {
    if (audit.isFetchingNextPage) {
      return;
    }

    observerRef.current?.disconnect();
    observerRef.current = new IntersectionObserver((entries) => {
      if (entries[0]?.isIntersecting && audit.hasNextPage) {
        void audit.fetchNextPage();
      }
    });

    if (node) {
      observerRef.current.observe(node);
    }
  }, [audit]);
  const columns = useMemo(() => [
    columnHelper.accessor('performedAt', { cell: (info) => <span className="font-mono text-[12px] text-fg-caption">{formatDate(info.getValue())}</span>, header: 'Timestamp' }),
    columnHelper.accessor('performedBy', { cell: (info) => <InlineCode>{formatUserId(info.getValue())}</InlineCode>, header: 'Actor' }),
    columnHelper.accessor('action', { cell: (info) => <InlineCode>{info.row.original.entityType}.{info.getValue()}</InlineCode>, header: 'Action' }),
    columnHelper.accessor('entityType', { cell: (info) => <Badge variant="info">{labelForEntityType(info.getValue())}</Badge>, header: 'Entity' }),
    columnHelper.accessor('entityId', { cell: (info) => <InlineCode>{info.getValue()}</InlineCode>, header: 'Entity ID' }),
    columnHelper.display({ cell: (info) => <Button onClick={() => toggleExpanded(setExpandedIds, info.row.original.id)} size="sm" type="button" variant="ghost">{expandedIds.has(info.row.original.id) ? 'Hide changes' : 'View changes'}</Button>, header: '', id: 'changes' }),
  ], [expandedIds]);
  const table = useReactTable({ columns, data: rows, getCoreRowModel: getCoreRowModel() });

  return (
    <div className="grid gap-8">
      <div>
        <h1 className="text-[34px] font-bold leading-tight text-fg-heading">Audit trail</h1>
        <p className="mt-2 text-[14.5px] text-fg-caption">Track every change made in GroundControl.</p>
      </div>

      <div className="grid gap-4">
        <div className="flex flex-wrap gap-2">
          {entityTypeOptions.map((option) => <FilterChip key={option.value} label={option.label} onToggle={() => toggleEntityType(setEntityTypes, option.value)} selected={entityTypes.includes(option.value)} />)}
        </div>
        <div className="grid gap-3 sm:grid-cols-[180px_180px]">
          <label className="grid gap-1.5 text-[12px] font-medium text-fg-caption">From<Input onChange={(event) => setFrom(event.target.value)} type="date" value={from} /></label>
          <label className="grid gap-1.5 text-[12px] font-medium text-fg-caption">To<Input onChange={(event) => setTo(event.target.value)} type="date" value={to} /></label>
        </div>
        {dateRangeInvalid ? <div className="text-[12px] text-badge-critical-fg">From date must be before or equal to To date.</div> : null}
      </div>

      <div className="overflow-hidden rounded-xl border border-stroke-subtle bg-bg-surface">
        <Table>
          <TableHeader>
            {table.getHeaderGroups().map((headerGroup) => (
              <TableRow key={headerGroup.id}>{headerGroup.headers.map((header) => <TableHead key={header.id}>{header.isPlaceholder ? null : flexRender(header.column.columnDef.header, header.getContext())}</TableHead>)}</TableRow>
            ))}
          </TableHeader>
          <TableBody>
            {table.getRowModel().rows.map((row, index) => {
              const isLastRow = index === table.getRowModel().rows.length - 1;

              return (
                <FragmentRows expanded={expandedIds.has(row.original.id)} key={row.id} refCallback={isLastRow ? lastRowRef : undefined} row={row} />
              );
            })}
            {audit.isLoading ? <SkeletonRows colSpan={columns.length} /> : null}
            {audit.isFetchingNextPage ? <SkeletonRows colSpan={columns.length} /> : null}
            {!audit.isLoading && table.getRowModel().rows.length === 0 ? <TableRow><TableCell className="py-10 text-center text-fg-caption" colSpan={columns.length}>No audit records found.</TableCell></TableRow> : null}
          </TableBody>
        </Table>
      </div>
    </div>
  );
}

function FragmentRows({ expanded, refCallback, row }: { expanded: boolean; refCallback?: (node: HTMLTableRowElement | null) => void; row: Row<AuditRecord> }) {
  return (
    <>
      <TableRow ref={refCallback}>{row.getVisibleCells().map((cell) => <TableCell key={cell.id}>{flexRender(cell.column.columnDef.cell, cell.getContext())}</TableCell>)}</TableRow>
      {expanded ? <TableRow><TableCell className="bg-bg-container p-4" colSpan={row.getVisibleCells().length}><JsonDiff after={changesToObject(row.original.changes, 'newValue')} before={changesToObject(row.original.changes, 'oldValue')} /></TableCell></TableRow> : null}
    </>
  );
}

function SkeletonRows({ colSpan }: { colSpan: number }) {
  return (
    <>
      <TableRow><TableCell colSpan={colSpan}><Skeleton className="h-10" /></TableCell></TableRow>
      <TableRow><TableCell colSpan={colSpan}><Skeleton className="h-10" /></TableCell></TableRow>
      <TableRow><TableCell colSpan={colSpan}><Skeleton className="h-10" /></TableCell></TableRow>
    </>
  );
}

function toggleEntityType(setEntityTypes: React.Dispatch<React.SetStateAction<string[]>>, value: string) {
  setEntityTypes((current) => current.includes(value) ? current.filter((entry) => entry !== value) : [...current, value]);
}

function toggleExpanded(setExpandedIds: React.Dispatch<React.SetStateAction<Set<string>>>, id: string) {
  setExpandedIds((current) => {
    const next = new Set(current);

    if (next.has(id)) {
      next.delete(id);
    } else {
      next.add(id);
    }

    return next;
  });
}

function changesToObject(changes: AuditRecord['changes'], valueKey: 'newValue' | 'oldValue') {
  return Object.fromEntries(changes.map((change) => [change.field, change[valueKey] ?? null]));
}

function labelForEntityType(value: string) {
  return entityTypeOptions.find((option) => option.value === value)?.label ?? value;
}

function formatDate(value: string) {
  return new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value));
}

const entityTypeOptions = [
  { label: 'ConfigEntry', value: 'ConfigEntry' },
  { label: 'Snapshot', value: 'Snapshot' },
  { label: 'Client', value: 'Client' },
  { label: 'User', value: 'User' },
  { label: 'Group', value: 'Group' },
  { label: 'Variable', value: 'Variable' },
  { label: 'Template', value: 'Template' },
  { label: 'Token', value: 'PersonalAccessToken' },
];
