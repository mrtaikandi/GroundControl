import { AlertDialog, AlertDialogAction, AlertDialogCancel, AlertDialogContent, AlertDialogDescription, AlertDialogFooter, AlertDialogHeader, AlertDialogTitle } from '@/components/ui/alert-dialog';
import { InlineCode } from '@/components/tower/data/InlineCode';
import { useDeleteEntry, type ConfigEntry } from '@/queries/useConfigEntries';

interface DeleteEntryDialogProps {
  entry?: ConfigEntry;
  onOpenChange: (open: boolean) => void;
  open: boolean;
  projectId: string;
}

export function DeleteEntryDialog({ entry, onOpenChange, open, projectId }: DeleteEntryDialogProps) {
  const deleteEntry = useDeleteEntry(projectId);

  async function confirmDelete() {
    if (!entry) {
      return;
    }

    await deleteEntry.mutateAsync({ id: entry.id, version: entry.version.toString() });
    onOpenChange(false);
  }

  return (
    <AlertDialog open={open} onOpenChange={onOpenChange}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>Delete entry</AlertDialogTitle>
          <AlertDialogDescription>Delete <InlineCode>{entry?.key ?? 'entry'}</InlineCode> from this project. This action cannot be undone.</AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel>Cancel</AlertDialogCancel>
          <AlertDialogAction disabled={deleteEntry.isPending} onClick={(event) => { event.preventDefault(); void confirmDelete(); }}>Delete</AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}