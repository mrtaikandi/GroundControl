import { useEffect, useMemo, useState } from 'react';
import { useSensitive } from '@/lib/sensitive';
import { useTweaksStore } from '@/store/tweaks';
import { cn } from '@/lib/utils';
import { highlightJson } from './shiki-theme';

interface JsonPreviewProps {
  className?: string;
  maxHeight?: string;
  sensitive?: boolean;
  value: unknown;
}

export function JsonPreview({ className, maxHeight = 'none', sensitive = false, value }: JsonPreviewProps) {
  const { masked } = useSensitive();
  const theme = useTweaksStore((state) => state.theme);
  const [html, setHtml] = useState('');
  const source = useMemo(() => JSON.stringify(sensitive && masked ? maskValue(value) : value, null, 2), [masked, sensitive, value]);

  useEffect(() => {
    let cancelled = false;

    void highlightJson(source, theme).then((nextHtml) => {
      if (!cancelled) {
        setHtml(nextHtml);
      }
    });

    return () => {
      cancelled = true;
    };
  }, [source, theme]);

  return (
    <div className={cn('overflow-auto rounded-xl bg-bg-surface p-6 font-mono text-[12.5px]', className)} style={{ maxHeight }}>
      <div className="[&_pre]:!m-0 [&_pre]:!bg-transparent [&_pre]:!p-0" dangerouslySetInnerHTML={{ __html: html }} />
    </div>
  );
}

function maskValue(value: unknown): unknown {
  if (value === null || typeof value !== 'object') {
    return '••••••••';
  }

  if (Array.isArray(value)) {
    return value.map(maskValue);
  }

  return Object.fromEntries(Object.entries(value).map(([key, childValue]) => [key, maskValue(childValue)]));
}