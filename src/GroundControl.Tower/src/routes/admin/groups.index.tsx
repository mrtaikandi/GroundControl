import { createColumnHelper, flexRender, getCoreRowModel, useReactTable } from '@tanstack/react-table';
import { createFileRoute, useNavigate } from '@tanstack/react-router';
import { useMemo } from 'react';
import { Skeleton } from '@/components/ui/skeleton';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { InlineCode } from '@/components/tower/data/InlineCode';
import { useGroups, type Group } from '@/queries/useGroups';
import { useProjects } from '@/queries/useProjects';
import { useUsers } from '@/queries/useUsers';

const columnHelper = createColumnHelper<Group>();

export const Route = createFileRoute('/admin/groups/')({
  component: GroupsRoute,
});

function GroupsRoute() {
  const groups = useGroups();
  const users = useUsers();
  const projects = useProjects();
  const navigate = useNavigate();
  const data = groups.data?.data ?? [];
  const memberCountByGroup = useMemo(() => {
    const counts = new Map<string, number>();

    for (const user of users.data?.data ?? []) {
      for (const grant of user.grants) {
        if (grant.resource) {
          counts.set(grant.resource, (counts.get(grant.resource) ?? 0) + 1);
        }
      }
    }

    return counts;
  }, [users.data?.data]);
  const projectCountByGroup = useMemo(() => {
    const counts = new Map<string, number>();

    for (const project of projects.data?.data ?? []) {
      if (project.groupId) {
        counts.set(project.groupId, (counts.get(project.groupId) ?? 0) + 1);
      }
    }

    return counts;
  }, [projects.data?.data]);
  const columns = useMemo(() => [
    columnHelper.accessor('name', { cell: (info) => <InlineCode>{info.getValue()}</InlineCode>, header: 'Group' }),
    columnHelper.display({ cell: (info) => memberCountByGroup.get(info.row.original.id) ?? 0, header: 'Members', id: 'members' }),
    columnHelper.display({ cell: (info) => projectCountByGroup.get(info.row.original.id) ?? 0, header: 'Projects', id: 'projects' }),
    columnHelper.accessor('updatedAt', { cell: (info) => formatDate(info.getValue()), header: 'Updated' }),
  ], [memberCountByGroup, projectCountByGroup]);
  const table = useReactTable({ columns, data, getCoreRowModel: getCoreRowModel() });

  return (
    <div className="grid gap-8">
      <div>
        <div className="text-[11px] font-medium uppercase text-fg-caption">Admin</div>
        <h1 className="mt-2 text-[34px] font-bold leading-tight text-fg-heading">Groups & roles</h1>
        <p className="mt-2 text-[14.5px] text-fg-caption">Group people together and control what they can access.</p>
      </div>

      {groups.isLoading ? <Skeleton className="h-96" /> : (
        <div className="overflow-hidden rounded-xl border border-stroke-subtle bg-bg-surface">
          <Table>
            <TableHeader>
              {table.getHeaderGroups().map((headerGroup) => (
                <TableRow key={headerGroup.id}>{headerGroup.headers.map((header) => <TableHead key={header.id}>{header.isPlaceholder ? null : flexRender(header.column.columnDef.header, header.getContext())}</TableHead>)}</TableRow>
              ))}
            </TableHeader>
            <TableBody>
              {table.getRowModel().rows.map((row) => (
                <TableRow className="cursor-pointer" key={row.id} onClick={() => navigate({ params: { groupId: row.original.id }, to: '/admin/groups/$groupId' })}>{row.getVisibleCells().map((cell) => <TableCell key={cell.id}>{flexRender(cell.column.columnDef.cell, cell.getContext())}</TableCell>)}</TableRow>
              ))}
              {table.getRowModel().rows.length === 0 ? <TableRow><TableCell className="py-10 text-center text-fg-caption" colSpan={columns.length}>No groups found.</TableCell></TableRow> : null}
            </TableBody>
          </Table>
        </div>
      )}
    </div>
  );
}

function formatDate(value: string) {
  return new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value));
}
