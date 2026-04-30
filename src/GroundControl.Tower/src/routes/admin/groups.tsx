import { createColumnHelper, flexRender, getCoreRowModel, useReactTable } from '@tanstack/react-table';
import { createFileRoute } from '@tanstack/react-router';
import { useMemo, useState } from 'react';
import { Skeleton } from '@/components/ui/skeleton';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { AssignRoleModal } from '@/components/tower/admin/AssignRoleModal';
import { Badge } from '@/components/tower/data/Badge';
import { InlineCode } from '@/components/tower/data/InlineCode';
import { useGroupMembers, useGroups, type Group, type GroupMember } from '@/queries/useGroups';
import { useProjects } from '@/queries/useProjects';
import { useRoles, type Role } from '@/queries/useRoles';
import { useUsers } from '@/queries/useUsers';

const columnHelper = createColumnHelper<Group>();

export const Route = createFileRoute('/admin/groups')({
  component: GroupsRoute,
});

function GroupsRoute() {
  const groups = useGroups();
  const users = useUsers();
  const projects = useProjects();
  const roles = useRoles();
  const [selectedGroupId, setSelectedGroupId] = useState<string | null>(null);
  const data = groups.data?.data ?? [];
  const selectedGroup = selectedGroupId ? data.find((group) => group.id === selectedGroupId) ?? data[0] : data[0];
  const roleById = useMemo(() => new Map((roles.data ?? []).map((role) => [role.id, role])), [roles.data]);
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
        <p className="mt-2 text-[14.5px] text-fg-caption">Membership is assigned through PUT /api/groups/{'{id}'}/members/{'{userId}'}.</p>
      </div>

      <div className="grid gap-6 xl:grid-cols-[minmax(0,1fr)_460px]">
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
                  <TableRow className={row.original.id === selectedGroup?.id ? 'bg-bg-selected' : undefined} key={row.id} onClick={() => setSelectedGroupId(row.original.id)}>{row.getVisibleCells().map((cell) => <TableCell key={cell.id}>{flexRender(cell.column.columnDef.cell, cell.getContext())}</TableCell>)}</TableRow>
                ))}
                {table.getRowModel().rows.length === 0 ? <TableRow><TableCell className="py-10 text-center text-fg-caption" colSpan={columns.length}>No groups found.</TableCell></TableRow> : null}
              </TableBody>
            </Table>
          </div>
        )}

        <GroupDetailPanel group={selectedGroup ?? null} roleById={roleById} roles={roles.data ?? []} />
      </div>
    </div>
  );
}

function GroupDetailPanel({ group, roleById, roles }: { group: Group | null; roleById: Map<string, Role>; roles: Role[] }) {
  const members = useGroupMembers(group?.id ?? null);
  const memberIds = useMemo(() => new Set((members.data ?? []).map((member) => member.id)), [members.data]);

  if (!group) {
    return <div className="rounded-xl border border-stroke-subtle bg-bg-container p-6 text-fg-caption">Select a group to inspect members.</div>;
  }

  return (
    <div className="grid gap-6 rounded-xl border border-stroke-subtle bg-bg-container p-6">
      <div className="flex items-start justify-between gap-4">
        <div>
          <div className="text-[11px] font-medium uppercase text-fg-caption">Group detail</div>
          <h2 className="mt-2 text-[22px] font-semibold text-fg-heading">{group.name}</h2>
          <p className="mt-1 text-[13px] text-fg-caption">{group.description ?? 'No description'}</p>
        </div>
        <AssignRoleModal existingUserIds={memberIds} groupId={group.id} mode="add" />
      </div>

      <div className="grid gap-3">
        <div className="text-[12px] font-medium uppercase text-fg-caption">Members</div>
        {members.isLoading ? <Skeleton className="h-28" /> : (members.data ?? []).map((member) => <MemberRow groupId={group.id} key={member.id} member={member} roleById={roleById} />)}
        {!members.isLoading && (members.data ?? []).length === 0 ? <div className="rounded-lg border border-dashed border-stroke-subtle p-6 text-center text-[13px] text-fg-caption">No members assigned.</div> : null}
      </div>

      <RolesReference roles={roles} />
    </div>
  );
}

function MemberRow({ groupId, member, roleById }: { groupId: string; member: GroupMember; roleById: Map<string, Role> }) {
  const grant = member.grants.find((candidate) => candidate.resource === groupId);
  const role = grant ? roleById.get(grant.roleId) : undefined;

  return (
    <div className="flex items-center justify-between gap-3 rounded-lg border border-stroke-subtle bg-bg-surface p-4">
      <div className="min-w-0">
        <InlineCode>{member.username}</InlineCode>
        <div className="mt-1 truncate text-[12px] text-fg-caption">{member.email}</div>
      </div>
      <div className="flex items-center gap-2">
        <RoleBadge roleName={role?.name ?? 'Viewer'}>{role?.name ?? 'Viewer'}</RoleBadge>
        <AssignRoleModal groupId={groupId} member={member} mode="edit" roleId={grant?.roleId} />
      </div>
    </div>
  );
}

function RolesReference({ roles }: { roles: Role[] }) {
  const roleByName = new Map(roles.map((role) => [role.name, role]));

  return (
    <div className="grid gap-3">
      <div className="text-[12px] font-medium uppercase text-fg-caption">Built-in roles</div>
      <div className="overflow-hidden rounded-lg border border-stroke-subtle bg-bg-surface">
        <Table>
          <TableHeader><TableRow><TableHead>Role</TableHead><TableHead>Summary</TableHead><TableHead>Permissions</TableHead></TableRow></TableHeader>
          <TableBody>
            {roleReference.map((reference) => {
              const role = roleByName.get(reference.name);
              const permissions = role?.permissions ?? reference.permissions;

              return (
                <TableRow key={reference.name}>
                  <TableCell><RoleBadge roleName={reference.name}>{reference.name}</RoleBadge></TableCell>
                  <TableCell className="text-[13px] text-fg-caption">{role?.description ?? reference.summary}</TableCell>
                  <TableCell><div className="flex flex-wrap gap-1.5">{permissions.map((permission) => <InlineCode key={permission}>{permission}</InlineCode>)}</div></TableCell>
                </TableRow>
              );
            })}
          </TableBody>
        </Table>
      </div>
    </div>
  );
}

function RoleBadge({ children, roleName }: { children: React.ReactNode; roleName: string }) {
  return <Badge variant={roleVariant(roleName)}>{children}</Badge>;
}

function roleVariant(roleName: string) {
  switch (roleName.toLowerCase()) {
    case 'admin':
      return 'critical';
    case 'publisher':
      return 'warning';
    case 'editor':
      return 'info';
    default:
      return 'neutral';
  }
}

function formatDate(value: string) {
  return new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value));
}

const roleReference = [
  { name: 'Viewer', permissions: ['config:read', 'projects:read'], summary: 'Read-only access to project configuration.' },
  { name: 'Editor', permissions: ['config:read', 'config:write', 'projects:read'], summary: 'Can read and edit configuration data.' },
  { name: 'Publisher', permissions: ['config:write', 'snapshots:publish'], summary: 'Can publish snapshots after editing configuration.' },
  { name: 'Admin', permissions: ['admin:*'], summary: 'Full administrative access to users, groups, roles, and tokens.' },
];
