import { ChevronDown, ChevronRight, ChevronsDown, ChevronsUp, Layers3, Folder, FolderOpen, Hash, Lock, Pencil, Plus } from 'lucide-react';
import { useEffect, useMemo, useState } from 'react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Skeleton } from '@/components/ui/skeleton';
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from '@/components/ui/tooltip';
import { Badge } from '@/components/tower/data/Badge';
import { InlineCode } from '@/components/tower/data/InlineCode';
import { SensitiveValue } from '@/components/tower/code/SensitiveValue';
import { type ConfigEntry } from '@/queries/useConfigEntries';
import { useOwnedEntries, type ConfigOwner, type EffectiveEntry, type EntrySource } from '@/queries/useEffectiveEntries';
import { useProjects } from '@/queries/useProjects';
import { useTemplates } from '@/queries/useTemplates';
import { buildKeyTree, type TreeNode } from '@/lib/key-tree';
import { cn } from '@/lib/utils';
import { DeleteEntryDialog } from './DeleteEntryDialog';
import { EntryModal } from './EntryModal';
import { EntryValue } from './EntryValue';
import { scopedValueKey, useEntryReveal } from './use-entry-reveal';

interface ConfigTreeViewProps {
  owner: ConfigOwner;
}

export function ConfigTreeView({ owner }: ConfigTreeViewProps) {
  const effective = useOwnedEntries(owner);
  const projects = useProjects();
  const templates = useTemplates();
  const ownerType = owner.kind === 'project' ? 1 : 0;
  const ownerName = owner.kind === 'project'
    ? projects.data?.data.find((candidate) => candidate.id === owner.id)?.name ?? ''
    : templates.data?.data.find((candidate) => candidate.id === owner.id)?.name ?? '';
  const [collapsed, setCollapsed] = useState<Set<string>>(() => new Set());
  const [selectedEntryId, setSelectedEntryId] = useState<string | null>(null);
  const [editingEntry, setEditingEntry] = useState<ConfigEntry | undefined>();
  const [deletingEntry, setDeletingEntry] = useState<ConfigEntry | undefined>();
  const [creating, setCreating] = useState(false);
  const [filter, setFilter] = useState('');
  const sourceById = useMemo(() => new Map(effective.entries.map((item) => [item.entry.id, item.source])), [effective.entries]);
  const itemById = useMemo(() => new Map(effective.entries.map((item) => [item.entry.id, item])), [effective.entries]);
  const filteredEntries = useMemo(() => {
    const needle = filter.trim().toLowerCase();

    if (!needle) {
      return effective.entries;
    }

    return effective.entries.filter((item) => item.entry.key.toLowerCase().includes(needle));
  }, [effective.entries, filter]);
  const tree = useMemo(() => buildKeyTree(filteredEntries.map((item) => item.entry)), [filteredEntries]);
  const allPrefixes = useMemo(() => collectPrefixes(tree), [tree]);
  const selectedItem = selectedEntryId ? itemById.get(selectedEntryId) ?? null : null;

  useEffect(() => {
    if (filter.trim()) {
      setCollapsed(new Set());
    }
  }, [filter]);

  useEffect(() => {
    setSelectedEntryId(null);
  }, [owner.id]);

  useEffect(() => {
    if (selectedEntryId !== null || tree.length === 0) {
      return;
    }

    const firstId = firstEntryId(tree);

    if (firstId) {
      setSelectedEntryId(firstId);
    }
  }, [selectedEntryId, tree]);

  return (
    <TooltipProvider>
      <div className="grid gap-6 xl:grid-cols-[minmax(0,1fr)_minmax(320px,600px)]">
        <div className="grid items-center gap-3 self-start xl:col-span-2 sm:grid-cols-[minmax(0,1fr)_auto]">
          <div className="flex flex-wrap items-center gap-3">
            <Input className="w-full sm:flex-1 sm:max-w-sm" onChange={(event) => setFilter(event.target.value)} placeholder="Filter…" value={filter} />
            <div className="flex justify-end gap-1">
              <Button onClick={() => setCollapsed(new Set())} size="sm" type="button" variant="ghost">
                <ChevronsDown aria-hidden="true" className="size-4" />
              </Button>
              <Button onClick={() => setCollapsed(new Set(allPrefixes))} size="sm" type="button" variant="ghost">
                <ChevronsUp aria-hidden="true" className="size-4" />
              </Button>
            </div>
          </div>
          <Button className="sm:justify-self-end" onClick={() => setCreating(true)} type="button"><Plus aria-hidden="true" className="size-3.5" />New entry</Button>
        </div>

        <div className="flex flex-col gap-3">
          {effective.isLoading ? <Skeleton className="min-h-96 flex-1" /> : (
            <div className="flex flex-1 flex-col overflow-hidden rounded-xl border border-stroke-subtle bg-bg-surface">
              {tree.length === 0 ? <div className="p-10 text-center text-fg-caption">No entries found.</div> : tree.map((node) => (
                <TreeRow
                  collapsed={collapsed}
                  key={node.kind === 'group' ? node.prefix : node.entry.id}
                  node={node}
                  onDelete={setDeletingEntry}
                  onEdit={setEditingEntry}
                  onSelect={setSelectedEntryId}
                  selectedEntryId={selectedEntryId}
                  setCollapsed={setCollapsed}
                  sourceById={sourceById}
                />
              ))}
            </div>
          )}
        </div>

        <div className="self-start">
          <EntryDetailPanel item={selectedItem} onEdit={setEditingEntry} ownerName={ownerName} />
        </div>

        <EntryModal mode="create" onOpenChange={setCreating} open={creating} ownerId={owner.id} ownerType={ownerType} />
        <EntryModal entry={editingEntry} key={editingEntry?.id ?? 'edit-empty'} mode="edit" onOpenChange={(open) => !open && setEditingEntry(undefined)} open={Boolean(editingEntry)} ownerId={owner.id} ownerType={ownerType} />
        <DeleteEntryDialog entry={deletingEntry} onOpenChange={(open) => !open && setDeletingEntry(undefined)} open={Boolean(deletingEntry)} ownerId={owner.id} ownerType={ownerType} />
      </div>
    </TooltipProvider>
  );
}

interface TreeRowProps {
  collapsed: Set<string>;
  depth?: number;
  node: TreeNode;
  onDelete: (entry: ConfigEntry) => void;
  onEdit: (entry: ConfigEntry) => void;
  onSelect: (id: string) => void;
  selectedEntryId: null | string;
  setCollapsed: (value: Set<string>) => void;
  sourceById: Map<string, EntrySource>;
}

function TreeRow({ collapsed, depth = 0, node, onDelete, onEdit, onSelect, selectedEntryId, setCollapsed, sourceById }: TreeRowProps) {
  if (node.kind === 'group') {
    const isCollapsed = collapsed.has(node.prefix);
    const segmentName = lastSegment(node.prefix);

    return (
      <div>
        <button
          className="flex w-full items-center gap-2 border-b border-stroke-subtle px-4 py-2.5 text-left text-[13.5px] hover:bg-bg-container"
          onClick={() => setCollapsed(toggle(collapsed, node.prefix))}
          style={{ paddingLeft: 16 + depth * 20 }}
          type="button"
        >
          {isCollapsed ? <ChevronRight className="size-4 text-fg-icon-subtle" /> : <ChevronDown className="size-4 text-fg-icon-subtle" />}
          {isCollapsed ? <Folder aria-hidden="true" className="size-4 text-fg-icon-subtle" /> : <FolderOpen aria-hidden="true" className="size-4 text-fg-icon-subtle" />}
          <span className="min-w-0 font-semibold text-fg-heading [overflow-wrap:anywhere]">{segmentName}</span>
          <span className="text-[12px] text-fg-caption [overflow-wrap:anywhere]">
            {node.count} {node.count === 1 ? 'key' : 'keys'}
            {node.sensitiveCount > 0 ? ` · ${node.sensitiveCount} sensitive` : ''}
          </span>
        </button>
        {!isCollapsed ? node.children.map((child) => (
          <TreeRow
            collapsed={collapsed}
            depth={depth + 1}
            key={child.kind === 'group' ? child.prefix : child.entry.id}
            node={child}
            onDelete={onDelete}
            onEdit={onEdit}
            onSelect={onSelect}
            selectedEntryId={selectedEntryId}
            setCollapsed={setCollapsed}
            sourceById={sourceById}
          />
        )) : null}
      </div>
    );
  }

  const source = sourceById.get(node.entry.id);
  const isInherited = source?.kind === 'template';
  const isSelected = selectedEntryId === node.entry.id;
  const segmentName = lastSegment(node.entry.key);
  const defaultVal = defaultValue(node.entry);
  const scopes = scopedValueCount(node.entry);

  return (
    <div
      className={cn(
        'group relative grid w-full cursor-pointer items-center gap-3 border-b border-stroke-subtle px-4 py-2.5 text-left text-[13px] last:border-b-0 hover:bg-bg-container',
        'grid-cols-[16px_minmax(0,1fr)] sm:grid-cols-[16px_minmax(0,1fr)_auto_auto]',
        isSelected && 'bg-bg-selected before:absolute before:inset-y-0 before:left-0 before:w-[2px] before:bg-primary',
      )}
      onClick={() => onSelect(node.entry.id)}
      style={{ paddingLeft: 16 + depth * 20 }}
    >
      {node.entry.isSensitive ? <Lock aria-hidden="true" className="size-3.5 text-fg-icon-subtle" />
        : isInherited
          ? <Layers3 aria-hidden="true" className="size-3.5 text-fg-icon-subtle" />
          : <Hash aria-hidden="true" className="size-3.5 text-fg-icon-subtle" />}
      <span className="flex min-w-0 items-center gap-2">
        <span className="font-semibold text-fg-heading [overflow-wrap:anywhere]">{segmentName}</span>
        <Badge variant="neutral">{node.entry.valueType}</Badge>
      </span>
      <div className="col-start-2 flex min-w-0 flex-wrap items-center gap-2 text-fg-body sm:col-start-auto sm:justify-end">
        {defaultVal ? <SensitiveValue isSensitive={node.entry.isSensitive} value={defaultVal} /> : null}
        {scopes > 0 ? <Badge variant="info">+{scopes}</Badge> : null}
      </div>
      {isInherited ? <span /> : (
        <div className="col-start-2 flex flex-wrap justify-start gap-1 opacity-100 transition-opacity sm:col-start-auto sm:justify-end sm:opacity-0 sm:group-hover:opacity-100">
          <Button onClick={(event) => { event.stopPropagation(); onEdit(node.entry); }} size="sm" type="button" variant="ghost">Edit</Button>
          <Button onClick={(event) => { event.stopPropagation(); onDelete(node.entry); }} size="sm" type="button" variant="ghost">Delete</Button>
        </div>
      )}
    </div>
  );
}

interface EntryDetailPanelProps {
  item: EffectiveEntry | null;
  onEdit: (entry: ConfigEntry) => void;
  ownerName: string;
}

function EntryDetailPanel({ item, onEdit, ownerName }: EntryDetailPanelProps) {
  if (!item) {
    return null;
  }

  return <EntryDetailPanelBody item={item} key={item.entry.id} onEdit={onEdit} ownerName={ownerName} />;
}

interface EntryDetailPanelBodyProps {
  item: EffectiveEntry;
  onEdit: (entry: ConfigEntry) => void;
  ownerName: string;
}

function EntryDetailPanelBody({ item, onEdit, ownerName }: EntryDetailPanelBodyProps) {
  const { entry, source } = item;
  const reveal = useEntryReveal(entry);
  const isInherited = source.kind === 'template';
  const defaultVal = entry.values.find((value) => !value.scopes || Object.keys(value.scopes).length === 0);
  const scopedVals = entry.values.filter((value) => value.scopes && Object.keys(value.scopes).length > 0);

  return (
    <div className="rounded-xl border border-stroke-subtle bg-bg-container p-6">
      <div className="flex items-start justify-between gap-3">
        <div className="min-w-0">
          <div className="text-[11px] font-medium uppercase text-fg-caption">Config entry</div>
          <h2 className="mt-2"><InlineCode className="text-[20px] font-semibold [overflow-wrap:anywhere]">{entry.key}</InlineCode></h2>
        </div>
        {!isInherited ? (
          <Button onClick={() => onEdit(entry)} size="sm" type="button" variant="secondary">
            <Pencil aria-hidden="true" className="size-3.5" />Edit
          </Button>
        ) : null}
      </div>

      <div className="mt-3 flex flex-wrap items-center gap-2">
        <Badge variant="neutral">{entry.valueType}</Badge>
        <OwnerPill ownerName={ownerName} source={source} />
      </div>

      {entry.description ? <p className="mt-4 text-[13.5px] text-fg-body [overflow-wrap:anywhere]">{entry.description}</p> : null}

      <div className="mt-6">
        <div className="text-[11px] font-medium uppercase text-fg-caption">
          Default value <span className="ml-1 normal-case text-fg-caption/80"></span>
        </div>
        <div className="mt-2">
          <EntryValue ariaLabel="Copy default value" emptyMessage="No default value." reveal={reveal} scopedValue={defaultVal} />
        </div>
      </div>

      <div className="mt-6">
        <div className="text-[11px] font-medium uppercase text-fg-caption">Scoped values</div>
        <div className="mt-2 grid gap-2">
          {scopedVals.length === 0 ? (
            <div className="rounded-lg border border-dashed border-stroke-subtle p-6 text-center text-[13px] text-fg-caption">No scoped values defined.</div>
          ) : scopedVals.map((value, index) => (
            <EntryValue ariaLabel="Copy scoped value" key={`${scopedValueKey(value.scopes ?? {})}-${index}`} reveal={reveal} scopedValue={value} scopeKey="top" />
          ))}
        </div>
      </div>

    </div>
  );
}

function OwnerPill({ ownerName, source }: { ownerName: string; source: EntrySource }) {
  if (source.kind === 'project') {
    return <Badge variant="neutral">project{ownerName ? ` · ${ownerName}` : ''}</Badge>;
  }

  if (source.kind === 'template-self') {
    return <Badge variant="neutral">template{ownerName ? ` · ${ownerName}` : ''}</Badge>;
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

function toggle(collapsed: Set<string>, prefix: string) {
  const next = new Set(collapsed);

  if (next.has(prefix)) {
    next.delete(prefix);
  } else {
    next.add(prefix);
  }

  return next;
}

function collectPrefixes(nodes: TreeNode[]): string[] {
  return nodes.flatMap((node) => node.kind === 'group' ? [node.prefix, ...collectPrefixes(node.children)] : []);
}

function firstEntryId(nodes: TreeNode[]): null | string {
  for (const node of nodes) {
    if (node.kind === 'entry') {
      return node.entry.id;
    }

    const found = firstEntryId(node.children);

    if (found) {
      return found;
    }
  }

  return null;
}

function lastSegment(key: string): string {
  const segments = key.split(':').filter(Boolean);

  return segments.length === 0 ? key : segments[segments.length - 1]!;
}

function defaultValue(entry: ConfigEntry): string {
  return entry.values.find((value) => !value.scopes || Object.keys(value.scopes).length === 0)?.value ?? '';
}

function scopedValueCount(entry: ConfigEntry): number {
  return entry.values.filter((value) => value.scopes && Object.keys(value.scopes).length > 0).length;
}
