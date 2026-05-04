import { createColumnHelper, flexRender, getCoreRowModel, getSortedRowModel, useReactTable, type SortingState } from '@tanstack/react-table';
import { Layers3 } from 'lucide-react';
import { useMemo, useState } from 'react';
import { Badge } from '@/components/tower/data/Badge';
import { InlineCode } from '@/components/tower/data/InlineCode';
import { SensitiveValue } from '@/components/tower/code/SensitiveValue';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Skeleton } from '@/components/ui/skeleton';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from '@/components/ui/tooltip';
import { type ConfigEntry } from '@/queries/useConfigEntries';
import { useEffectiveEntries, type EffectiveEntry } from '@/queries/useEffectiveEntries';
import { DeleteEntryDialog } from './DeleteEntryDialog';
import { EntryModal } from './EntryModal';

const columnHelper = createColumnHelper<EffectiveEntry>();

interface ConfigFlatViewProps {
  projectId: string;
}

export function ConfigFlatView({ projectId }: ConfigFlatViewProps) {
  const effective = useEffectiveEntries(projectId);
  const [sorting, setSorting] = useState<SortingState>([]);
  const [search, setSearch] = useState('');
  const [editingEntry, setEditingEntry] = useState<ConfigEntry | undefined>();
  const [deletingEntry, setDeletingEntry] = useState<ConfigEntry | undefined>();
  const [creating, setCreating] = useState(false);
  const data = useMemo(
    () => effective.entries.filter((item) => item.entry.key.toLowerCase().includes(search.toLowerCase())),
    [effective.entries, search],
  );
  const columns = useMemo(() => [
    columnHelper.accessor((row) => row.entry.key, { cell: (info) => <InlineCode>{info.getValue()}</InlineCode>, header: 'Key', id: 'key' }),
    columnHelper.accessor((row) => row.entry.valueType, { cell: (info) => <Badge variant="neutral">{info.getValue()}</Badge>, header: 'Type', id: 'valueType' }),
    columnHelper.display({ cell: (info) => <SensitiveValue isSensitive={info.row.original.entry.isSensitive} value={defaultValue(info.row.original.entry)} />, header: 'Default value', id: 'defaultValue' }),
    columnHelper.display({ cell: (info) => <Badge variant="info">{scopeCount(info.row.original.entry)} scopes</Badge>, header: 'Scopes', id: 'scopes' }),
    columnHelper.display({ cell: (info) => <OwnerBadge source={info.row.original.source} />, header: 'Owner', id: 'owner' }),
    columnHelper.accessor((row) => row.entry.updatedAt, { cell: (info) => relativeDate(info.getValue()), header: 'Updated', id: 'updatedAt' }),
    columnHelper.display({
      cell: (info) => {
        const item = info.row.original;
        if (item.source.kind === 'template') {
          return null;
        }

        return (
          <div className="flex justify-end gap-1 opacity-0 transition-opacity group-hover:opacity-100">
            <Button onClick={(event) => { event.stopPropagation(); setEditingEntry(item.entry); }} size="sm" type="button" variant="ghost">Edit</Button>
            <Button onClick={(event) => { event.stopPropagation(); setDeletingEntry(item.entry); }} size="sm" type="button" variant="ghost">Delete</Button>
          </div>
        );
      },
      header: '',
      id: 'actions',
    }),
  ], []);
  const table = useReactTable({ columns, data, getCoreRowModel: getCoreRowModel(), getSortedRowModel: getSortedRowModel(), onSortingChange: setSorting, state: { sorting } });

  if (effective.isLoading) {
    return <Skeleton className="h-96" />;
  }

  return (
    <TooltipProvider>
      <div className="grid gap-4">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <Input className="max-w-sm" onChange={(event) => setSearch(event.target.value)} placeholder="Filter entries…" value={search} />
          <Button onClick={() => setCreating(true)} type="button">New entry</Button>
        </div>

        <div className="overflow-hidden rounded-xl border border-stroke-subtle bg-bg-surface">
          <Table>
            <TableHeader>
              {table.getHeaderGroups().map((headerGroup) => (
                <TableRow key={headerGroup.id}>
                  {headerGroup.headers.map((header) => <TableHead key={header.id}>{header.isPlaceholder ? null : flexRender(header.column.columnDef.header, header.getContext())}</TableHead>)}
                </TableRow>
              ))}
            </TableHeader>
            <TableBody>
              {table.getRowModel().rows.map((row) => {
                const isInherited = row.original.source.kind === 'template';

                return (
                  <TableRow
                    className={isInherited ? 'group cursor-default' : 'group cursor-pointer'}
                    key={row.id}
                    onClick={isInherited ? undefined : () => setEditingEntry(row.original.entry)}
                  >
                    {row.getVisibleCells().map((cell) => <TableCell key={cell.id}>{flexRender(cell.column.columnDef.cell, cell.getContext())}</TableCell>)}
                  </TableRow>
                );
              })}
              {table.getRowModel().rows.length === 0 ? <TableRow><TableCell className="py-10 text-center text-fg-caption" colSpan={columns.length}>No entries found.</TableCell></TableRow> : null}
            </TableBody>
          </Table>
        </div>

        <EntryModal mode="create" onOpenChange={setCreating} open={creating} projectId={projectId} />
        <EntryModal entry={editingEntry} mode="edit" onOpenChange={(open) => !open && setEditingEntry(undefined)} open={Boolean(editingEntry)} projectId={projectId} />
        <DeleteEntryDialog entry={deletingEntry} onOpenChange={(open) => !open && setDeletingEntry(undefined)} open={Boolean(deletingEntry)} projectId={projectId} />
      </div>
    </TooltipProvider>
  );
}

function OwnerBadge({ source }: { source: EffectiveEntry['source'] }) {
  if (source.kind === 'project') {
    return <Badge variant="neutral">project</Badge>;
  }

  if (source.kind === 'template') {
    return (
      <Tooltip>
        <TooltipTrigger asChild>
          <span><Badge className="gap-1.5" variant="info"><Layers3 aria-hidden="true" className="size-3.5" strokeWidth={1.8} />{source.templateName}</Badge></span>
        </TooltipTrigger>
        <TooltipContent>Inherited from {source.templateName} template</TooltipContent>
      </Tooltip>
    );
  }

  return (
    <Tooltip>
      <TooltipTrigger asChild>
        <span><Badge variant="warning">overrides · {source.templateName}</Badge></span>
      </TooltipTrigger>
      <TooltipContent>Project entry overrides {source.templateName}</TooltipContent>
    </Tooltip>
  );
}

function defaultValue(entry: ConfigEntry): string {
  return entry.values.find((value) => !value.scopes || Object.keys(value.scopes).length === 0)?.value ?? '';
}

function scopeCount(entry: ConfigEntry): number {
  return entry.values.filter((value) => value.scopes && Object.keys(value.scopes).length > 0).length;
}

function relativeDate(value: string): string {
  const formatter = new Intl.RelativeTimeFormat(undefined, { numeric: 'auto' });
  const days = Math.round((new Date(value).getTime() - Date.now()) / 86_400_000);

  return formatter.format(days, 'day');
}
