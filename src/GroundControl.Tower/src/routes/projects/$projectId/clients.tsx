import { createColumnHelper, flexRender, getCoreRowModel, useReactTable } from '@tanstack/react-table';
import { createFileRoute, Link, useNavigate } from '@tanstack/react-router';
import { useMemo, useState } from 'react';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Badge } from '@/components/tower/data/Badge';
import { InlineCode } from '@/components/tower/data/InlineCode';
import { ScopeTag } from '@/components/tower/data/ScopeTag';
import { NewClientModal } from '@/components/tower/clients/NewClientModal';
import { ProjectPicker } from '@/components/tower/projects/ProjectPicker';
import { RevokeClientDialog } from '@/components/tower/clients/RevokeClientDialog';
import { useClients, type Client } from '@/queries/useClients';
import { useProjects } from '@/queries/useProjects';

const columnHelper = createColumnHelper<Client>();

export const Route = createFileRoute('/projects/$projectId/clients')({
  component: ClientsRoute,
});

function ClientsRoute() {
  const { projectId } = Route.useParams();
  const navigate = useNavigate();
  const projects = useProjects();
  const project = projects.data?.data.find((candidate) => candidate.id === projectId);
  const clients = useClients(projectId);
  const [clientToRevoke, setClientToRevoke] = useState<Client | null>(null);
  const data = clients.data?.data ?? [];
  const columns = useMemo(() => [
    columnHelper.accessor('name', { cell: (info) => <InlineCode>{info.getValue()}</InlineCode>, header: 'Name' }),
    columnHelper.display({ cell: (info) => <ScopeChips scopes={info.row.original.scopes} />, header: 'Scope context', id: 'scopes' }),
    columnHelper.accessor('isActive', { cell: (info) => <Badge variant={info.getValue() ? 'success' : 'critical'}>{info.getValue() ? 'active' : 'revoked'}</Badge>, header: 'Status' }),
    columnHelper.accessor('lastUsedAt', { cell: (info) => info.getValue() ? formatDate(info.getValue()!) : 'never', header: 'Last used' }),
    columnHelper.display({ cell: (info) => <div className="flex justify-end"><Button disabled={!info.row.original.isActive} onClick={() => setClientToRevoke(info.row.original)} size="sm" type="button" variant="ghost">Revoke</Button></div>, header: '', id: 'actions' }),
  ], []);
  const table = useReactTable({ columns, data, getCoreRowModel: getCoreRowModel() });

  return (
    <div className="grid gap-6">
      <div className="flex items-start justify-between gap-4">
        <div className="min-w-0">
          <Link className="text-[12.5px] text-fg-caption transition-colors hover:text-fg-body" params={{ projectId }} to="/projects/$projectId">
            ← {project?.name ?? 'project'}
          </Link>
          <div className="mt-2 flex flex-wrap items-center gap-3">
            <h1 className="text-[34px] font-bold leading-tight text-fg-heading">Clients</h1>
            <span aria-hidden="true" className="text-[20px] text-fg-caption">·</span>
            <ProjectPicker
              onChange={(nextId) => navigate({ params: { projectId: nextId }, to: '/projects/$projectId/clients' })}
              projects={projects.data?.data ?? []}
              selectedId={projectId}
            />
          </div>
          <p className="mt-2 text-[14.5px] text-fg-caption">Issue and manage the credentials your apps use to read this project's settings.</p>
        </div>
        <NewClientModal projectId={projectId} />
      </div>

      {clients.isLoading ? <Skeleton className="h-96" /> : (
        <div className="overflow-hidden rounded-xl border border-stroke-subtle bg-bg-surface">
          <Table>
            <TableHeader>
              {table.getHeaderGroups().map((headerGroup) => (
                <TableRow key={headerGroup.id}>{headerGroup.headers.map((header) => <TableHead key={header.id}>{header.isPlaceholder ? null : flexRender(header.column.columnDef.header, header.getContext())}</TableHead>)}</TableRow>
              ))}
            </TableHeader>
            <TableBody>
              {table.getRowModel().rows.map((row) => (
                <TableRow className={row.original.isActive ? undefined : 'opacity-60'} key={row.id}>{row.getVisibleCells().map((cell) => <TableCell key={cell.id}>{flexRender(cell.column.columnDef.cell, cell.getContext())}</TableCell>)}</TableRow>
              ))}
              {table.getRowModel().rows.length === 0 ? <TableRow><TableCell className="py-10 text-center text-fg-caption" colSpan={columns.length}>No client credentials found.</TableCell></TableRow> : null}
            </TableBody>
          </Table>
        </div>
      )}
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
