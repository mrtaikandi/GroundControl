import { useEffect, useMemo, useState } from 'react';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle, DialogTrigger } from '@/components/ui/dialog';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { useAssignGroupRole, type GroupMember } from '@/queries/useGroups';
import { useRoles } from '@/queries/useRoles';
import { useUsers } from '@/queries/useUsers';

type AssignRoleModalProps = {
  groupId: string;
} & ({
  existingUserIds: Set<string>;
  mode: 'add';
} | {
  member?: GroupMember;
  mode: 'edit';
  roleId?: string;
});

const roleOrder = ['Viewer', 'Editor', 'Publisher', 'Admin'];

export function AssignRoleModal(props: AssignRoleModalProps) {
  const [open, setOpen] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [selectedUserId, setSelectedUserId] = useState('');
  const [selectedRoleId, setSelectedRoleId] = useState('');
  const users = useUsers();
  const roles = useRoles();
  const assignRole = useAssignGroupRole(props.groupId);
  const member = props.mode === 'edit' ? props.member : undefined;
  const roleId = props.mode === 'edit' ? props.roleId : undefined;
  const userOptions = useMemo(() => {
    const allUsers = users.data?.data ?? [];

    if (member) {
      return [member];
    }

    return props.mode === 'add' ? allUsers.filter((user) => !props.existingUserIds.has(user.id)) : [];
  }, [member, props.mode, props.mode === 'add' ? props.existingUserIds : undefined, users.data?.data]);
  const roleOptions = useMemo(() => [...(roles.data ?? [])].sort((left, right) => roleSortIndex(left.name) - roleSortIndex(right.name)), [roles.data]);

  useEffect(() => {
    if (!open) {
      return;
    }

    setSelectedUserId(member?.id ?? userOptions[0]?.id ?? '');
    setSelectedRoleId(roleId ?? roleOptions[0]?.id ?? '');
    setError(null);
  }, [member?.id, open, roleId, roleOptions, userOptions]);

  async function submit() {
    if (!selectedUserId || !selectedRoleId) {
      return;
    }

    try {
      await assignRole.mutateAsync({ roleId: selectedRoleId, userId: selectedUserId });
      setOpen(false);
    } catch (assignError) {
      setError(assignError instanceof Error ? assignError.message : 'Unable to assign role.');
    }
  }

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild><Button size="sm" type="button" variant="ghost">{props.mode === 'edit' ? 'Change role' : 'Assign role'}</Button></DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{props.mode === 'edit' ? 'Change role' : 'Assign role'}</DialogTitle>
          <DialogDescription>{props.mode === 'edit' ? 'Update this member\'s group role.' : 'Select a user and role for this group.'}</DialogDescription>
        </DialogHeader>
        <div className="grid gap-4">
          <label className="grid gap-1.5 text-[12px] font-medium text-fg-caption">
            Member
            <Select disabled={!!member || users.isLoading || assignRole.isPending} onValueChange={setSelectedUserId} value={selectedUserId}>
              <SelectTrigger><SelectValue placeholder="Select member" /></SelectTrigger>
              <SelectContent>{userOptions.map((user) => <SelectItem key={user.id} value={user.id}>{user.username}</SelectItem>)}</SelectContent>
            </Select>
          </label>
          <label className="grid gap-1.5 text-[12px] font-medium text-fg-caption">
            Role
            <Select disabled={roles.isLoading || assignRole.isPending} onValueChange={setSelectedRoleId} value={selectedRoleId}>
              <SelectTrigger><SelectValue placeholder="Select role" /></SelectTrigger>
              <SelectContent>{roleOptions.map((role) => <SelectItem key={role.id} value={role.id}>{role.name}</SelectItem>)}</SelectContent>
            </Select>
          </label>
        </div>
        {error ? <div className="rounded-lg border border-badge-critical-bg bg-badge-critical-bg/20 px-3 py-2 text-[12px] text-badge-critical-fg">{error}</div> : null}
        <DialogFooter>
          <Button disabled={assignRole.isPending} onClick={() => setOpen(false)} type="button" variant="secondary">Cancel</Button>
          <Button disabled={!selectedUserId || !selectedRoleId || assignRole.isPending} onClick={() => { void submit(); }} type="button">Assign role</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

function roleSortIndex(name: string) {
  const index = roleOrder.indexOf(name);

  return index === -1 ? roleOrder.length : index;
}