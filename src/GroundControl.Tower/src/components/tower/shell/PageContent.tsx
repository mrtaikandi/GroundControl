import type { ComponentProps } from 'react';
import { cn } from '@/lib/utils';

export function PageContent({ className, ...props }: ComponentProps<'div'>) {
  return <div className={cn('px-page-h 2xl:px-8', className)} {...props} />;
}