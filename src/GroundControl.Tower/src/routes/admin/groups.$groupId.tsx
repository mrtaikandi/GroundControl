import { PageContent } from '@/components/tower/shell/PageContent';
import { createFileRoute, Link } from '@tanstack/react-router';
import { ArrowLeft } from 'lucide-react';
import { useMemo } from 'react';
import { Skeleton } from '@/components/ui/skeleton';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { AssignRoleModal } from '@/components/tower/admin/AssignRoleModal';
import { Badge } from '@/components/tower/data/Badge';
import { InlineCode } from '@/components/tower/data/InlineCode';
import { useGroupMembers, useGroups, type GroupMember } from '@/queries/useGroups';
import { useRoles, type Role } from '@/queries/useRoles';

export const Route = createFileRoute('/admin/groups/$groupId')({
  component: GroupDetailRoute,
});

function GroupDetailRoute() {
  const { groupId } = Route.useParams();
  const groups = useGroups();
  const members = useGroupMembers(groupId);
  const roles = useRoles();
  const group = groups.data?.data.find((candidate) => candidate.id === groupId);
  const roleById = useMemo(() => new Map((roles.data ?? []).map((role) => [role.id, role])), [roles.data]);
  const memberIds = useMemo(() => new Set((members.data ?? []).map((member) => member.id)), [members.data]);

  return (
    <PageContent>
      <div className="grid gap-8">
      <div>
        <Link className="inline-flex items-center gap-1.5 text-[12px] font-medium text-fg-caption hover:text-fg-body" to="/admin/groups">
          <ArrowLeft className="size-3.5" />
          Back to Groups & roles
        </Link>
        <div className="mt-3 text-[11px] font-medium uppercase text-fg-caption">Group</div>
        <h1 className="mt-2 text-[34px] font-bold leading-tight text-fg-heading">{group?.name ?? (groups.isLoading ? 'Loading…' : 'Group not found')}</h1>
        <p className="mt-2 text-[14.5px] text-fg-caption">{group?.description ?? 'No description'}</p>
      </div>

      {!groups.isLoading && !group ? (
        <div className="rounded-xl border border-stroke-subtle bg-bg-container p-6 text-fg-caption">This group no longer exists.</div>
      ) : (
        <div className="grid gap-6 rounded-xl border border-stroke-subtle bg-bg-container p-6">
          <div className="flex items-start justify-between gap-4">
            <div>
              <div className="text-[12px] font-medium uppercase text-fg-caption">Members</div>
              <p className="mt-1 text-[13px] text-fg-caption">Users with a role on this group.</p>
            </div>
            <AssignRoleModal existingUserIds={memberIds} groupId={groupId} mode="add" />
          </div>

          <div className="grid gap-3">
            {members.isLoading ? <Skeleton className="h-28" /> : (members.data ?? []).map((member) => <MemberRow groupId={groupId} key={member.id} member={member} roleById={roleById} />)}
            {!members.isLoading && (members.data ?? []).length === 0 ? <div className="rounded-lg border border-dashed border-stroke-subtle p-6 text-center text-[13px] text-fg-caption">No members assigned.</div> : null}
          </div>

          <RolesReference roles={roles.data ?? []} />
        </div>
      )}
      </div>
    </PageContent>
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

const roleReference = [
  { name: 'Viewer', permissions: ['config:read', 'projects:read'], summary: 'Read-only access to project configuration.' },
  { name: 'Editor', permissions: ['config:read', 'config:write', 'projects:read'], summary: 'Can read and edit configuration data.' },
  { name: 'Publisher', permissions: ['config:write', 'snapshots:publish'], summary: 'Can publish snapshots after editing configuration.' },
  { name: 'Admin', permissions: ['admin:*'], summary: 'Full administrative access to users, groups, roles, and tokens.' },
];
