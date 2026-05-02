import { createColumnHelper, flexRender, getCoreRowModel, useReactTable } from '@tanstack/react-table';
import { createFileRoute, Link } from '@tanstack/react-router';
import { useMemo, useState } from 'react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Skeleton } from '@/components/ui/skeleton';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Badge } from '@/components/tower/data/Badge';
import { InlineCode } from '@/components/tower/data/InlineCode';
import { ScopeTag } from '@/components/tower/data/ScopeTag';
import { useAllClients, type ClientWithProject } from '@/queries/useAllClients';
import { useProjects } from '@/queries/useProjects';

const columnHelper = createColumnHelper<ClientWithProject>();

export const Route = createFileRoute('/clients')({
  component: ClientsRoute,
});

function ClientsRoute() {
  const projects = useProjects();
  const allClients = useAllClients();
  const [search, setSearch] = useState('');
  const projectNames = useMemo(() => new Map((projects.data?.data ?? []).map((project) => [project.id, project.name])), [projects.data]);

  const filtered = useMemo(() => {
    const needle = search.trim().toLowerCase();
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
    columnHelper.accessor('name', { cell: (info) => <InlineCode>{info.getValue()}</InlineCode>, header: 'Name' }),
    columnHelper.display({
      cell: (info) => {
        const projectId = info.row.original.projectId;
        const name = projectNames.get(projectId) ?? projectId;
        return (
          <Link className="text-[12.5px] font-medium text-fg-link transition-colors hover:underline" params={{ projectId }} to="/projects/$projectId">
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
      cell: (info) => (
        <div className="flex justify-end">
          <Button asChild size="sm" type="button" variant="ghost">
            <Link params={{ projectId: info.row.original.projectId }} to="/projects/$projectId/clients">Manage</Link>
          </Button>
        </div>
      ),
      header: '',
      id: 'actions',
    }),
  ], [projectNames]);

  const table = useReactTable({ columns, data: filtered, getCoreRowModel: getCoreRowModel() });
  const totalActive = allClients.data.filter((client) => client.isActive).length;

  return (
    <div className="grid gap-6">
      <div>
        <h1 className="text-[34px] font-bold leading-tight text-fg-heading">Clients</h1>
        <p className="mt-2 text-[14.5px] text-fg-caption">All credentials issued across projects. {allClients.data.length} total · {totalActive} active.</p>
      </div>

      <div className="flex flex-wrap items-center gap-3">
        <Input
          className="h-9 max-w-xs"
          onChange={(event) => setSearch(event.target.value)}
          placeholder="Filter by client or project"
          value={search}
        />
      </div>

      {allClients.isLoading ? <Skeleton className="h-96" /> : (
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
              {table.getRowModel().rows.length === 0 ? <TableRow><TableCell className="py-10 text-center text-fg-caption" colSpan={columns.length}>No clients found.</TableCell></TableRow> : null}
            </TableBody>
          </Table>
        </div>
      )}
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
