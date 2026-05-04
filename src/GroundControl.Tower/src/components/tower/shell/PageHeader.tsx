import type { ReactNode } from 'react';
import { PageContent } from '@/components/tower/shell/PageContent';
import { cn } from '@/lib/utils';

interface PageHeaderProps {
  actions?: ReactNode;
  align?: 'start' | 'end';
  description?: ReactNode;
  descriptionClassName?: string;
  eyebrow?: ReactNode;
  eyebrowClassName?: string;
  title: ReactNode;
  titleClassName?: string;
}

export function PageHeader({
  actions,
  align = 'end',
  description,
  descriptionClassName,
  eyebrow,
  eyebrowClassName,
  title,
  titleClassName,
}: PageHeaderProps) {
  return (
    <div className="border-b border-stroke-divider">
      <PageContent className="pb-6 2xl:px-16">
        <div className={`flex flex-wrap justify-between gap-4 ${align === 'start' ? 'items-start' : 'items-end'}`}>
          <div className="min-w-0">
            {eyebrow ? <div className={cn('text-[11px] font-medium uppercase text-fg-caption', eyebrowClassName)}>{eyebrow}</div> : null}
            <h1 className={cn('text-[34px] font-bold leading-tight text-fg-heading', eyebrow ? 'mt-2' : '', titleClassName)}>{title}</h1>
            {description ? <p className={cn('mt-2 text-[14.5px] text-fg-caption', descriptionClassName)}>{description}</p> : null}
          </div>
          {actions ? <div className="shrink-0">{actions}</div> : null}
        </div>
      </PageContent>
    </div>
  );
}