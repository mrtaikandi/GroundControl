import { Badge } from '@/components/tower/data/Badge';
import { InlineCode } from '@/components/tower/data/InlineCode';
import { useSensitive } from '@/lib/sensitive';
import { cn } from '@/lib/utils';

interface SensitiveValueProps {
  className?: string;
  isSensitive: boolean;
  value: string;
}

export function SensitiveValue({ className, isSensitive, value }: SensitiveValueProps) {
  const { masked } = useSensitive();

  if (isSensitive && masked) {
    return (
      <span className={cn('inline-flex items-center gap-2', className)}>
        <span className="font-mono text-[12.5px] text-syntax-sensitive">••••••••</span>
        <Badge variant="neutral">sensitive</Badge>
      </span>
    );
  }

  if (!value) {
    return null;
  }

  return <InlineCode className={className}>{value}</InlineCode>;
}