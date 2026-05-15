import { createColumnHelper, flexRender, getCoreRowModel, useReactTable, type ColumnSizingState } from '@tanstack/react-table';
import { createFileRoute } from '@tanstack/react-router';
import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { AuditDetailsDialog } from '@/components/tower/audit/AuditDetailsDialog';
import { AuditFilterPopover, type AuditFilters } from '@/components/tower/audit/AuditFilterPopover';
import { Badge } from '@/components/tower/data/Badge';
import { InlineCode } from '@/components/tower/data/InlineCode';
import { PageContent } from '@/components/tower/shell/PageContent';
import { PageHeader } from '@/components/tower/shell/PageHeader';
import { useAuditRecords, type AuditRecord } from '@/queries/useAuditRecords';
import { formatUserId } from '@/lib/user';

const columnHelper = createColumnHelper<AuditRecord>();

const entityTypeOptions = [
  { label: 'ConfigEntry', value: 'ConfigEntry' },
  { label: 'Snapshot', value: 'Snapshot' },
  { label: 'Client', value: 'Client' },
  { label: 'User', value: 'User' },
  { label: 'Group', value: 'Group' },
  { label: 'Variable', value: 'Variable' },
  { label: 'Template', value: 'Template' },
  { label: 'Token', value: 'PersonalAccessToken' },
] as const;

const DefaultRangeDays = 7;

function defaultFilters(): AuditFilters {
  const today = new Date();
  const from = new Date(today);
  from.setDate(from.getDate() - DefaultRangeDays);
  return { entityTypes: [], from: toDateInput(from), to: toDateInput(today) };
}

function toDateInput(value: Date): string {
  const year = value.getFullYear();
  const month = String(value.getMonth() + 1).padStart(2, '0');
  const day = String(value.getDate()).padStart(2, '0');
  return `${year}-${month}-${day}`;
}

export const Route = createFileRoute('/admin/audit')({
  component: AuditRoute,
});

function AuditRoute() {
  const [filters, setFilters] = useState<AuditFilters>(() => defaultFilters());
  const [selectedRecord, setSelectedRecord] = useState<AuditRecord | null>(null);
  const [columnSizing, setColumnSizing] = useState<ColumnSizingState>({});
  const audit = useAuditRecords({ entityTypes: filters.entityTypes, from: filters.from, to: filters.to });
  const rows = useMemo(() => audit.data?.pages.flatMap((page) => page.data) ?? [], [audit.data]);
  const observerRef = useRef<IntersectionObserver | null>(null);
  const tableContainerRef = useRef<HTMLDivElement>(null);
  const initializedSizingRef = useRef(false);

  useEffect(() => () => observerRef.current?.disconnect(), []);

  useEffect(() => {
    if (filters.entityTypes.length > 1 && audit.hasNextPage && !audit.isFetchingNextPage && rows.length < 20) {
      void audit.fetchNextPage();
    }
  }, [audit, filters.entityTypes.length, rows.length]);

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
    columnHelper.accessor('performedAt', { cell: (info) => <span className="font-mono text-[12px] text-fg-caption">{formatDate(info.getValue())}</span>, header: 'Timestamp', id: 'performedAt', minSize: 140, size: 200 }),
    columnHelper.accessor('performedBy', { cell: (info) => <InlineCode>{formatUserId(info.getValue())}</InlineCode>, header: 'Actor', id: 'performedBy', minSize: 120, size: 220 }),
    columnHelper.accessor('action', { cell: (info) => <InlineCode>{info.row.original.entityType}.{info.getValue()}</InlineCode>, header: 'Action', id: 'action', minSize: 160, size: 280 }),
    columnHelper.accessor('entityType', { cell: (info) => <Badge variant="info">{labelForEntityType(info.getValue())}</Badge>, header: 'Entity', id: 'entityType', minSize: 100, size: 160 }),
    columnHelper.display({
      cell: (info) => (
        <div className="flex justify-end opacity-0 transition-opacity group-hover:opacity-100 focus-within:opacity-100">
          <Button onClick={(event) => { event.stopPropagation(); setSelectedRecord(info.row.original); }} size="sm" type="button" variant="ghost">View changes</Button>
        </div>
      ),
      enableResizing: false,
      header: '',
      id: 'changes',
      size: 130,
    }),
  ], []);

  const table = useReactTable({
    columnResizeMode: 'onChange',
    columns,
    data: rows,
    defaultColumn: { minSize: 80, size: 160 },
    enableColumnResizing: true,
    getCoreRowModel: getCoreRowModel(),
    onColumnSizingChange: setColumnSizing,
    state: { columnSizing },
  });

  useEffect(() => {
    const container = tableContainerRef.current;
    if (!container || initializedSizingRef.current) {
      return;
    }

    const fitColumnsToContainer = () => {
      if (initializedSizingRef.current) {
        return;
      }

      const width = container.clientWidth;
      if (width === 0) {
        return;
      }

      const allColumns = table.getAllLeafColumns();
      const fixedTotal = allColumns.filter((column) => !column.getCanResize()).reduce((sum, column) => sum + column.getSize(), 0);
      const resizable = allColumns.filter((column) => column.getCanResize());
      const baseTotal = resizable.reduce((sum, column) => sum + column.getSize(), 0);
      const available = width - fixedTotal;

      if (baseTotal <= 0 || available <= baseTotal) {
        initializedSizingRef.current = true;
        return;
      }

      const scale = available / baseTotal;
      const next: ColumnSizingState = {};
      for (const column of resizable) {
        next[column.id] = Math.floor(column.getSize() * scale);
      }

      setColumnSizing(next);
      initializedSizingRef.current = true;
    };

    fitColumnsToContainer();
    const observer = new ResizeObserver(fitColumnsToContainer);
    observer.observe(container);
    return () => observer.disconnect();
  }, [table]);

  return (
    <>
      <PageHeader
        actions={(
          <div className="flex items-center gap-2">
            <AuditFilterPopover filters={filters} onApply={setFilters} options={entityTypeOptions} />
          </div>
        )}
        description="Track every change made in GroundControl."
        title="Audit"
      />

      <PageContent>
        <div className="grid gap-8 pt-8">
          <div className="overflow-hidden rounded-xl border border-stroke-subtle bg-bg-surface" ref={tableContainerRef}>
            <div className="overflow-x-auto">
              <Table className="min-w-full table-fixed" style={{ width: table.getTotalSize() }}>
                <TableHeader>
                  {table.getHeaderGroups().map((headerGroup) => (
                    <TableRow key={headerGroup.id}>
                      {headerGroup.headers.map((header) => (
                        <TableHead className="group/th relative" key={header.id} style={{ width: header.getSize() }}>
                          {header.isPlaceholder ? null : (
                            <div className="truncate">{flexRender(header.column.columnDef.header, header.getContext())}</div>
                          )}
                          {header.column.getCanResize() ? (
                            <div
                              aria-hidden="true"
                              className={`absolute -right-1 top-0 flex h-full w-2 cursor-col-resize touch-none select-none items-stretch justify-center ${header.column.getIsResizing() ? 'z-10' : ''}`}
                              onClick={(event) => event.stopPropagation()}
                              onMouseDown={header.getResizeHandler()}
                              onTouchStart={header.getResizeHandler()}
                            >
                              <span className={`w-px transition-colors ${header.column.getIsResizing() ? 'bg-stroke-field-focus' : 'bg-transparent group-hover/th:bg-stroke-subtle hover:bg-stroke-field-focus'}`} />
                            </div>
                          ) : null}
                        </TableHead>
                      ))}
                    </TableRow>
                  ))}
                </TableHeader>
                <TableBody>
                  {table.getRowModel().rows.map((row, index) => {
                    const isLastRow = index === table.getRowModel().rows.length - 1;

                    return (
                      <TableRow
                        className="group cursor-pointer"
                        key={row.id}
                        onClick={() => setSelectedRecord(row.original)}
                        ref={isLastRow ? lastRowRef : undefined}
                      >
                        {row.getVisibleCells().map((cell) => (
                          <TableCell key={cell.id} style={{ width: cell.column.getSize() }}>
                            <div className="truncate">{flexRender(cell.column.columnDef.cell, cell.getContext())}</div>
                          </TableCell>
                        ))}
                      </TableRow>
                    );
                  })}
                  {audit.isLoading ? <SkeletonRows colSpan={columns.length} /> : null}
                  {audit.isFetchingNextPage ? <SkeletonRows colSpan={columns.length} /> : null}
                  {!audit.isLoading && table.getRowModel().rows.length === 0 ? <TableRow><TableCell className="py-10 text-center text-fg-caption" colSpan={columns.length}>No audit records found.</TableCell></TableRow> : null}
                </TableBody>
              </Table>
            </div>
          </div>
        </div>
      </PageContent>

      <AuditDetailsDialog
        onOpenChange={(open) => { if (!open) setSelectedRecord(null); }}
        open={selectedRecord !== null}
        record={selectedRecord}
      />
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

function labelForEntityType(value: string) {
  return entityTypeOptions.find((option) => option.value === value)?.label ?? value;
}

function formatDate(value: string) {
  return new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value));
}
