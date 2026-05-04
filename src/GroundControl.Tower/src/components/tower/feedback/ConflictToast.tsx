import { toast } from 'sonner';
import { Button } from '@/components/ui/button';

interface ConflictToastProps {
  latestVersion?: string;
  retryWithLatest?: (latestVersion: string) => void;
  toastId?: string | number;
}

export function ConflictToast({ latestVersion, retryWithLatest, toastId }: ConflictToastProps) {
  return (
    <div className="ui-surface-floating ui-text-body w-[360px] p-4">
      <div className="font-semibold text-fg-heading">Someone else changed this resource.</div>
      <div className="mt-1 text-fg-caption">Refresh to see the latest version.</div>
      {latestVersion && retryWithLatest ? (
        <Button
          className="mt-3 h-8"
          onClick={() => {
            retryWithLatest(latestVersion);
            toast.dismiss(toastId);
          }}
          size="sm"
          type="button"
        >
          Retry with latest
        </Button>
      ) : null}
    </div>
  );
}

export function showConflictToast(props: Omit<ConflictToastProps, 'toastId'>) {
  return toast.custom((toastId) => <ConflictToast {...props} toastId={toastId} />);
}