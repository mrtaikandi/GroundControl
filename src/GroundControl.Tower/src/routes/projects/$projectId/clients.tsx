import { createColumnHelper, flexRender, getCoreRowModel, useReactTable } from '@tanstack/react-table';
import { createFileRoute } from '@tanstack/react-router';
import { useMemo, useState } from 'react';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Badge } from '@/components/tower/data/Badge';
import { ScopeTag } from '@/components/tower/data/ScopeTag';
import { NewClientModal } from '@/components/tower/clients/NewClientModal';
import { RevokeClientDialog } from '@/components/tower/clients/RevokeClientDialog';
import { useClients, type Client } from '@/queries/useClients';

const columnHelper = createColumnHelper<Client>();

export const Route = createFileRoute('/projects/$projectId/clients')({
  component: ClientsRoute,
});

function ClientsRoute() {
  const { projectId } = Route.useParams();
  const clients = useClients(projectId);
  const [clientToRevoke, setClientToRevoke] = useState<Client | null>(null);
  const data = clients.data?.data ?? [];
  const columns = useMemo(() => [
    columnHelper.accessor('name', { cell: (info) => info.getValue(), header: 'Name' }),
    columnHelper.display({ cell: (info) => <ScopeChips scopes={info.row.original.scopes} />, header: 'Scope Context', id: 'scopes' }),
    columnHelper.accessor('isActive', { cell: (info) => <Badge variant={info.getValue() ? 'success' : 'critical'}>{info.getValue() ? 'active' : 'revoked'}</Badge>, header: 'Status' }),
    columnHelper.accessor('lastUsedAt', { cell: (info) => info.getValue() ? formatDate(info.getValue()!) : 'never', header: 'Last Used' }),
    columnHelper.display({ cell: (info) => <div className="flex justify-end"><Button disabled={!info.row.original.isActive} onClick={() => setClientToRevoke(info.row.original)} size="sm" type="button" variant="ghost">Revoke</Button></div>, header: '', id: 'actions' }),
  ], []);
  const table = useReactTable({ columns, data, getCoreRowModel: getCoreRowModel() });

  return (
    <div className="grid gap-4">
      <div className="flex flex-wrap items-center justify-end">
        <NewClientModal projectId={projectId} />
      </div>

      <div className="overflow-hidden rounded-xl border border-stroke-subtle bg-bg-surface">
        <div className="overflow-x-auto">
          <Table>
            <TableHeader>
              {table.getHeaderGroups().map((headerGroup) => (
                <TableRow key={headerGroup.id}>{headerGroup.headers.map((header) => <TableHead key={header.id}>{header.isPlaceholder ? null : flexRender(header.column.columnDef.header, header.getContext())}</TableHead>)}</TableRow>
              ))}
            </TableHeader>
            <TableBody>
              {clients.isLoading ? (
                Array.from({ length: 4 }).map((_, rowIndex) => (
                  <TableRow key={`skeleton-${rowIndex}`}>
                    {columns.map((_column, cellIndex) => (
                      <TableCell key={cellIndex}><Skeleton className="h-4 w-3/4" /></TableCell>
                    ))}
                  </TableRow>
                ))
              ) : (
                <>
                  {table.getRowModel().rows.map((row) => (
                    <TableRow className={row.original.isActive ? undefined : 'opacity-60'} key={row.id}>{row.getVisibleCells().map((cell) => <TableCell key={cell.id}>{flexRender(cell.column.columnDef.cell, cell.getContext())}</TableCell>)}</TableRow>
                  ))}
                  {table.getRowModel().rows.length === 0 ? <TableRow><TableCell className="py-10 text-center text-fg-caption" colSpan={columns.length}>No client credentials found.</TableCell></TableRow> : null}
                </>
              )}
            </TableBody>
          </Table>
        </div>
      </div>
      <RevokeClientDialog client={clientToRevoke} onOpenChange={(open) => { if (!open) { setClientToRevoke(null); } }} open={clientToRevoke !== null} projectId={projectId} />
    </div>
  );
}

function ScopeChips({ scopes }: { scopes: Record<string, string> }) {
  const entries = Object.entries(scopes);

  if (entries.length === 0) {
    return <span className="text-fg-caption">default</span>;
  }

  return <div className="flex flex-wrap gap-1.5">{entries.map(([dimension, value]) => <ScopeTag dimension={dimension} key={dimension} value={value} />)}</div>;
}

function formatDate(value: string) {
  return new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value));
}
