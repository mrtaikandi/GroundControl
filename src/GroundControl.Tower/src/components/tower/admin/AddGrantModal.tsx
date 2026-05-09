import { useEffect, useMemo, useState } from 'react';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle, DialogTrigger } from '@/components/ui/dialog';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { useGroups } from '@/queries/useGroups';
import { useRoles } from '@/queries/useRoles';
import { useAddGrant } from '@/queries/useUsers';

type AddGrantModalProps = {
  userId: string;
};

const roleOrder = ['Viewer', 'Editor', 'Publisher', 'Admin'];

export function AddGrantModal({ userId }: AddGrantModalProps) {
  const [open, setOpen] = useState(false);
  const [groupId, setGroupId] = useState('');
  const [roleId, setRoleId] = useState('');
  const groups = useGroups();
  const roles = useRoles();
  const addGrant = useAddGrant(userId);
  const groupOptions = groups.data?.data ?? [];
  const roleOptions = useMemo(() => [...(roles.data ?? [])].sort((left, right) => roleSortIndex(left.name) - roleSortIndex(right.name)), [roles.data]);

  useEffect(() => {
    if (!open) {
      return;
    }

    setGroupId((current) => current || groupOptions[0]?.id || '');
    setRoleId((current) => current || roleOptions[0]?.id || '');
  }, [groupOptions, open, roleOptions]);

  async function submit() {
    if (!groupId || !roleId) {
      return;
    }

    await addGrant.mutateAsync({ groupId, roleId });
    setOpen(false);
  }

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild><Button size="sm" type="button" variant="ghost">Add Grant</Button></DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Add Grant</DialogTitle>
          <DialogDescription>Grant this user a role within a group.</DialogDescription>
        </DialogHeader>
        <div className="grid gap-4">
          <label className="grid gap-1.5 text-[12px] font-medium text-fg-caption">
            Group
            <Select disabled={groups.isLoading || addGrant.isPending} onValueChange={setGroupId} value={groupId}>
              <SelectTrigger><SelectValue placeholder="Select group" /></SelectTrigger>
              <SelectContent>{groupOptions.map((group) => <SelectItem key={group.id} value={group.id}>{group.name}</SelectItem>)}</SelectContent>
            </Select>
          </label>
          <label className="grid gap-1.5 text-[12px] font-medium text-fg-caption">
            Role
            <Select disabled={roles.isLoading || addGrant.isPending} onValueChange={setRoleId} value={roleId}>
              <SelectTrigger><SelectValue placeholder="Select role" /></SelectTrigger>
              <SelectContent>{roleOptions.map((role) => <SelectItem key={role.id} value={role.id}>{role.name}</SelectItem>)}</SelectContent>
            </Select>
          </label>
        </div>
        <DialogFooter>
          <Button disabled={addGrant.isPending} onClick={() => setOpen(false)} type="button" variant="secondary">Cancel</Button>
          <Button disabled={!groupId || !roleId || addGrant.isPending} onClick={() => { void submit(); }} type="button">Add Grant</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

function roleSortIndex(name: string) {
  const index = roleOrder.indexOf(name);

  return index === -1 ? roleOrder.length : index;
}