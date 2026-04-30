import { AlertDialog, AlertDialogAction, AlertDialogCancel, AlertDialogContent, AlertDialogDescription, AlertDialogFooter, AlertDialogHeader, AlertDialogTitle } from '@/components/ui/alert-dialog';
import { useRevokeClient, type Client } from '@/queries/useClients';

type RevokeClientDialogProps = {
  client: Client | null;
  onOpenChange: (open: boolean) => void;
  open: boolean;
  projectId: string;
};

export function RevokeClientDialog({ client, onOpenChange, open, projectId }: RevokeClientDialogProps) {
  const revokeClient = useRevokeClient(projectId);

  async function confirmRevoke() {
    if (!client) {
      return;
    }

    await revokeClient.mutateAsync({ id: client.id, version: client.version.toString() });
    onOpenChange(false);
  }

  return (
    <AlertDialog open={open} onOpenChange={onOpenChange}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>Revoke {client?.name ?? 'client'}?</AlertDialogTitle>
          <AlertDialogDescription>Clients using this credential will immediately lose access. This cannot be undone.</AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel disabled={revokeClient.isPending}>Cancel</AlertDialogCancel>
          <AlertDialogAction disabled={!client || revokeClient.isPending} onClick={(event) => { event.preventDefault(); void confirmRevoke(); }}>Revoke</AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}