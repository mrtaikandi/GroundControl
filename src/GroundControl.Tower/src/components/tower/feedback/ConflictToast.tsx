import { toast } from 'sonner';
import { Button } from '@/components/ui/button';

interface ConflictToastProps {
  latestVersion?: string;
  retryWithLatest?: (latestVersion: string) => void;
  toastId?: string | number;
}

export function ConflictToast({ latestVersion, retryWithLatest, toastId }: ConflictToastProps) {
  return (
    <div className="w-[360px] rounded-xl border border-stroke-subtle bg-bg-surface p-4 text-[13px] shadow-[0_18px_40px_-16px_rgba(0,0,40,.25)]">
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