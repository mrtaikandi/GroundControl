import { createColumnHelper, flexRender, getCoreRowModel, useReactTable } from '@tanstack/react-table';
import { createFileRoute, Link } from '@tanstack/react-router';
import { ExternalLink, Pencil } from 'lucide-react';
import { useMemo, useState } from 'react';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Badge } from '@/components/tower/data/Badge';
import { SearchFilterPopover } from '@/components/tower/data/SearchFilterPopover';
import { ScopeTag } from '@/components/tower/data/ScopeTag';
import { EditClientModal } from '@/components/tower/clients/EditClientModal';
import { NewClientModal } from '@/components/tower/clients/NewClientModal';
import { PageContent } from '@/components/tower/shell/PageContent';
import { PageHeader } from '@/components/tower/shell/PageHeader';
import { useAllClients, type ClientWithProject } from '@/queries/useAllClients';
import { useProjects } from '@/queries/useProjects';

const columnHelper = createColumnHelper<ClientWithProject>();

export const Route = createFileRoute('/clients')({
  component: ClientsRoute,
});

function ClientsRoute() {
  const projects = useProjects();
  const allClients = useAllClients();
  const [search, setSearch] = useState<string | undefined>(undefined);
  const [editingClient, setEditingClient] = useState<ClientWithProject | null>(null);
  const projectNames = useMemo(() => new Map((projects.data?.data ?? []).map((project) => [project.id, project.name])), [projects.data]);

  const filtered = useMemo(() => {
    const needle = search?.trim().toLowerCase();
    if (!needle) {
      return allClients.data;
    }

    return allClients.data.filter((client) => {
      const name = client.name?.toLowerCase() ?? '';
      const projectName = projectNames.get(client.projectId)?.toLowerCase() ?? '';
      return name.includes(needle) || projectName.includes(needle);
    });
  }, [allClients.data, projectNames, search]);

  const columns = useMemo(() => [
    columnHelper.accessor('name', {
      cell: (info) => (
        <button
          className="text-left text-[13px] font-medium text-fg-body transition-colors hover:text-fg-link hover:underline"
          onClick={() => setEditingClient(info.row.original)}
          type="button"
        >
          {info.getValue()}
        </button>
      ),
      header: 'Name',
    }),
    columnHelper.display({
      cell: (info) => {
        const projectId = info.row.original.projectId;
        const name = projectNames.get(projectId) ?? projectId;
        return (
          <Link className="text-[12.5px] font-medium text-fg-link transition-colors hover:underline [overflow-wrap:anywhere]" params={{ projectId }} to="/projects/$projectId">
            {name}
          </Link>
        );
      },
      header: 'Project',
      id: 'project',
    }),
    columnHelper.display({ cell: (info) => <ScopeChips scopes={info.row.original.scopes} />, header: 'Scope context', id: 'scopes' }),
    columnHelper.accessor('isActive', { cell: (info) => <Badge variant={info.getValue() ? 'success' : 'critical'}>{info.getValue() ? 'active' : 'revoked'}</Badge>, header: 'Status' }),
    columnHelper.accessor('lastUsedAt', { cell: (info) => info.getValue() ? formatDate(info.getValue()!) : 'never', header: 'Last used' }),
    columnHelper.display({
      cell: (info) => {
        const targetProjectName = projectNames.get(info.row.original.projectId) ?? info.row.original.projectId;
        return (
          <div className="flex justify-end gap-1">
            <Button aria-label={`Edit ${info.row.original.name}`} className="size-8 rounded-full p-0" onClick={() => setEditingClient(info.row.original)} size="sm" title="Edit client" type="button" variant="ghost">
              <Pencil aria-hidden="true" className="size-3.5" strokeWidth={1.8} />
            </Button>
            <Button asChild aria-label={`Manage clients in ${targetProjectName}`} className="size-8 rounded-full p-0" size="sm" type="button" variant="ghost">
              <Link params={{ projectId: info.row.original.projectId }} title={`Manage clients in ${targetProjectName}`} to="/projects/$projectId/clients">
                <ExternalLink aria-hidden="true" className="size-3.5" strokeWidth={1.8} />
              </Link>
            </Button>
          </div>
        );
      },
      header: '',
      id: 'actions',
    }),
  ], [projectNames]);

  const table = useReactTable({ columns, data: filtered, getCoreRowModel: getCoreRowModel() });
  const totalActive = allClients.data.filter((client) => client.isActive).length;

  return (
    <>
      <PageHeader
        actions={(
          <div className="flex items-center gap-2">
            <SearchFilterPopover
              appliedSearch={search}
              ariaLabel="Filter clients"
              onApply={setSearch}
              placeholder="Client name or project"
            />
            <NewClientModal />
          </div>
        )}
        description={`All credentials issued across projects. ${allClients.data.length} total · ${totalActive} active.`}
        title="Clients"
      />

      <PageContent>
        <div className="grid gap-6 pt-8">
          {allClients.isLoading ? <Skeleton className="h-96" /> : (
            <div className="overflow-hidden rounded-xl border border-stroke-subtle bg-bg-surface">
              <div className="overflow-x-auto">
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
                    {table.getRowModel().rows.length === 0 ? <TableRow><TableCell className="py-10 text-center text-fg-caption" colSpan={columns.length}>No clients found.</TableCell></TableRow> : null}
                  </TableBody>
                </Table>
              </div>
            </div>
          )}
        </div>
      </PageContent>

      <EditClientModal
        client={editingClient}
        onOpenChange={(open) => { if (!open) setEditingClient(null); }}
        open={editingClient !== null}
        projectId={editingClient?.projectId ?? ''}
      />
    </>
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
