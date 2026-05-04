import { createColumnHelper, flexRender, getCoreRowModel, useReactTable } from '@tanstack/react-table';
import { createFileRoute } from '@tanstack/react-router';
import { useMemo, useState } from 'react';
import { AlertDialog, AlertDialogAction, AlertDialogCancel, AlertDialogContent, AlertDialogDescription, AlertDialogFooter, AlertDialogHeader, AlertDialogTitle } from '@/components/ui/alert-dialog';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { AddGrantModal } from '@/components/tower/admin/AddGrantModal';
import { NewUserModal } from '@/components/tower/admin/NewUserModal';
import { Badge } from '@/components/tower/data/Badge';
import { InlineCode } from '@/components/tower/data/InlineCode';
import { PageHeader } from '@/components/tower/shell/PageHeader';
import { PageContent } from '@/components/tower/shell/PageContent';
import { ScopeTag } from '@/components/tower/data/ScopeTag';
import { useGroups } from '@/queries/useGroups';
import { useRoles } from '@/queries/useRoles';
import { useDeleteUser, useRemoveGrant, useUserGrants, useUsers, type Grant, type User, type UserDetail } from '@/queries/useUsers';

const columnHelper = createColumnHelper<User>();

export const Route = createFileRoute('/admin/users')({
  component: UsersRoute,
});

function UsersRoute() {
  const users = useUsers();
  const groups = useGroups();
  const roles = useRoles();
  const [selectedUserId, setSelectedUserId] = useState<string | null>(null);
  const data = users.data?.data ?? [];
  const selectedUser = selectedUserId ? data.find((user) => user.id === selectedUserId) ?? data[0] : data[0];
  const groupById = useMemo(() => new Map((groups.data?.data ?? []).map((group) => [group.id, group])), [groups.data?.data]);
  const roleById = useMemo(() => new Map((roles.data ?? []).map((role) => [role.id, role])), [roles.data]);
  const columns = useMemo(() => [
    columnHelper.display({
      cell: (info) => <UserIdentity user={info.row.original} />,
      header: 'User',
      id: 'user',
    }),
    columnHelper.accessor('email', { cell: (info) => info.getValue(), header: 'Email' }),
    columnHelper.display({ cell: () => <span className="text-fg-caption">not tracked</span>, header: 'Last login', id: 'last-login' }),
  ], []);
  const table = useReactTable({ columns, data, getCoreRowModel: getCoreRowModel() });

  return (
    <>
      <PageHeader actions={<NewUserModal />} align="start" description="Manage the people who can sign in and use GroundControl." title="Users" />

      <PageContent>
        <div className="grid gap-8 pt-8">
          <div className="grid gap-6 xl:grid-cols-[minmax(0,1fr)_460px]">
            {users.isLoading ? <Skeleton className="h-96" /> : (
              <div className="overflow-hidden rounded-xl border border-stroke-subtle bg-bg-surface">
                <Table>
                  <TableHeader>
                    {table.getHeaderGroups().map((headerGroup) => (
                      <TableRow key={headerGroup.id}>{headerGroup.headers.map((header) => <TableHead key={header.id}>{header.isPlaceholder ? null : flexRender(header.column.columnDef.header, header.getContext())}</TableHead>)}</TableRow>
                    ))}
                  </TableHeader>
                  <TableBody>
                    {table.getRowModel().rows.map((row) => (
                      <TableRow className={row.original.id === selectedUser?.id ? 'bg-bg-selected' : undefined} key={row.id} onClick={() => setSelectedUserId(row.original.id)}>{row.getVisibleCells().map((cell) => <TableCell key={cell.id}>{flexRender(cell.column.columnDef.cell, cell.getContext())}</TableCell>)}</TableRow>
                    ))}
                    {table.getRowModel().rows.length === 0 ? <TableRow><TableCell className="py-10 text-center text-fg-caption" colSpan={columns.length}>No users found.</TableCell></TableRow> : null}
                  </TableBody>
                </Table>
              </div>
            )}

            <GrantDetailPanel groupById={groupById} onDeleted={() => setSelectedUserId(null)} roleById={roleById} user={selectedUser ?? null} />
          </div>
        </div>
      </PageContent>
    </>
  );
}

function UserIdentity({ user }: { user: User }) {
  const initials = user.username.slice(0, 2).toUpperCase();

  return (
    <div className="flex items-center gap-3">
      <div className="grid size-8 place-items-center rounded-full bg-bg-container text-[11px] font-semibold text-fg-heading">{initials}</div>
      <div className="min-w-0">
        <InlineCode>{user.username}</InlineCode>
        <div className="mt-1 truncate text-[12px] text-fg-caption">{user.email}</div>
      </div>
    </div>
  );
}

function GrantDetailPanel({ groupById, onDeleted, roleById, user }: { groupById: Map<string, { name: string }>; onDeleted: () => void; roleById: Map<string, { name: string }>; user: User | null }) {
  const grants = useUserGrants(user?.id ?? null);
  const [grantToRemove, setGrantToRemove] = useState<Grant | null>(null);
  const [deleteOpen, setDeleteOpen] = useState(false);

  if (!user) {
    return <div className="rounded-xl border border-stroke-subtle bg-bg-container p-6 text-fg-caption">Select a user to inspect grants.</div>;
  }

  const userDetail = grants.data;
  const grantList = userDetail?.grants ?? user.grants;

  return (
    <div className="rounded-xl border border-stroke-subtle bg-bg-container p-6">
      <div className="flex items-start justify-between gap-4">
        <div>
          <div className="text-[11px] font-medium uppercase text-fg-caption">User details</div>
          <h2 className="mt-2 text-[22px] font-semibold text-fg-heading">{user.username}</h2>
        </div>
        <div className="flex items-center gap-2">
          <AddGrantModal userId={user.id} />
          <Button onClick={() => setDeleteOpen(true)} size="sm" type="button" variant="ghost">Delete</Button>
        </div>
      </div>

      <div className="mt-6 grid gap-3">
        {grants.isLoading ? <Skeleton className="h-28" /> : grantList.map((grant) => <GrantRow grant={grant} groupById={groupById} key={`${grant.resource ?? 'global'}-${grant.roleId}`} onRemove={() => setGrantToRemove(grant)} roleById={roleById} />)}
        {!grants.isLoading && grantList.length === 0 ? <div className="rounded-lg border border-dashed border-stroke-subtle p-6 text-center text-[13px] text-fg-caption">No grants assigned.</div> : null}
      </div>

      <RemoveGrantDialog grant={grantToRemove} groupById={groupById} onOpenChange={(open) => { if (!open) { setGrantToRemove(null); } }} open={grantToRemove !== null} roleById={roleById} user={userDetail ?? null} />
      <DeleteUserDialog onDeleted={onDeleted} onOpenChange={setDeleteOpen} open={deleteOpen} user={userDetail ?? user} />
    </div>
  );
}

function DeleteUserDialog({ onDeleted, onOpenChange, open, user }: { onDeleted: () => void; onOpenChange: (open: boolean) => void; open: boolean; user: { id: string; username: string; version: string | number } }) {
  const deleteUserMutation = useDeleteUser(user.id);

  async function confirmDelete() {
    await deleteUserMutation.mutateAsync({ version: user.version.toString() });
    onOpenChange(false);
    onDeleted();
  }

  return (
    <AlertDialog open={open} onOpenChange={onOpenChange}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>Delete {user.username}?</AlertDialogTitle>
          <AlertDialogDescription>This permanently removes the user and revokes all of their grants. This cannot be undone.</AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel disabled={deleteUserMutation.isPending}>Cancel</AlertDialogCancel>
          <AlertDialogAction disabled={deleteUserMutation.isPending} onClick={(event) => { event.preventDefault(); void confirmDelete(); }}>Delete user</AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}

function GrantRow({ grant, groupById, onRemove, roleById }: { grant: Grant; groupById: Map<string, { name: string }>; onRemove: () => void; roleById: Map<string, { name: string }> }) {
  const roleName = roleById.get(grant.roleId)?.name ?? grant.roleId;
  const groupName = grant.resource ? groupById.get(grant.resource)?.name ?? grant.resource : 'global';
  const conditions = Object.entries(grant.conditions ?? {});

  return (
    <div className="rounded-lg border border-stroke-subtle bg-bg-surface p-4">
      <div className="flex items-start justify-between gap-3">
        <div className="grid gap-2">
          <div className="flex flex-wrap items-center gap-2"><InlineCode>{groupName}</InlineCode><RoleBadge roleName={roleName}>{roleName}</RoleBadge></div>
          {conditions.length > 0 ? <div className="flex flex-wrap gap-1.5">{conditions.map(([dimension, values]) => <ScopeTag dimension={dimension} key={dimension} value={values.join('|')} />)}</div> : <span className="text-[12px] text-fg-caption">No additional conditions</span>}
        </div>
        <Button onClick={onRemove} size="sm" type="button" variant="ghost">Remove</Button>
      </div>
    </div>
  );
}

function RemoveGrantDialog({ grant, groupById, onOpenChange, open, roleById, user }: { grant: Grant | null; groupById: Map<string, { name: string }>; onOpenChange: (open: boolean) => void; open: boolean; roleById: Map<string, { name: string }>; user: UserDetail | null }) {
  const removeGrant = useRemoveGrant(user?.id ?? '');
  const roleName = grant ? roleById.get(grant.roleId)?.name ?? grant.roleId : 'grant';
  const groupName = grant?.resource ? groupById.get(grant.resource)?.name ?? grant.resource : 'global';

  async function confirmRemove() {
    if (!grant || !user) {
      return;
    }

    await removeGrant.mutateAsync({ grant, user, version: user.version.toString() });
    onOpenChange(false);
  }

  return (
    <AlertDialog open={open} onOpenChange={onOpenChange}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>Remove {roleName}?</AlertDialogTitle>
          <AlertDialogDescription>Remove this grant from {groupName}. The user will immediately lose the permissions it provides.</AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel disabled={removeGrant.isPending}>Cancel</AlertDialogCancel>
          <AlertDialogAction disabled={!grant || !user || removeGrant.isPending} onClick={(event) => { event.preventDefault(); void confirmRemove(); }}>Remove</AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
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
