import { ChevronDown, ChevronRight } from 'lucide-react';
import { useMemo, useState } from 'react';
import { Badge } from '@/components/tower/data/Badge';
import { InlineCode } from '@/components/tower/data/InlineCode';
import { SensitiveValue } from '@/components/tower/code/SensitiveValue';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from '@/components/ui/tooltip';
import { type ConfigEntry } from '@/queries/useConfigEntries';
import { useEffectiveEntries, type EntrySource } from '@/queries/useEffectiveEntries';
import { buildKeyTree, type TreeNode } from '@/lib/key-tree';
import { DeleteEntryDialog } from './DeleteEntryDialog';
import { EntryModal } from './EntryModal';

interface ConfigTreeViewProps {
  projectId: string;
}

export function ConfigTreeView({ projectId }: ConfigTreeViewProps) {
  const effective = useEffectiveEntries(projectId);
  const [collapsed, setCollapsed] = useState<Set<string>>(() => new Set());
  const [editingEntry, setEditingEntry] = useState<ConfigEntry | undefined>();
  const [deletingEntry, setDeletingEntry] = useState<ConfigEntry | undefined>();
  const sourceById = useMemo(() => new Map(effective.entries.map((item) => [item.entry.id, item.source])), [effective.entries]);
  const tree = useMemo(() => buildKeyTree(effective.entries.map((item) => item.entry)), [effective.entries]);
  const allPrefixes = useMemo(() => collectPrefixes(tree), [tree]);

  if (effective.isLoading) {
    return <Skeleton className="h-96" />;
  }

  return (
    <TooltipProvider>
      <div className="grid gap-4">
        <div className="flex justify-end gap-2">
          <Button onClick={() => setCollapsed(new Set())} type="button" variant="ghost">Expand all</Button>
          <Button onClick={() => setCollapsed(new Set(allPrefixes))} type="button" variant="ghost">Collapse all</Button>
        </div>
        <div className="overflow-hidden rounded-xl border border-stroke-subtle bg-bg-surface">
          {tree.length === 0 ? <div className="p-10 text-center text-fg-caption">No entries found.</div> : tree.map((node) => <TreeRow collapsed={collapsed} key={node.kind === 'group' ? node.prefix : node.entry.id} node={node} onDelete={setDeletingEntry} onEdit={setEditingEntry} setCollapsed={setCollapsed} sourceById={sourceById} />)}
        </div>
        <EntryModal entry={editingEntry} mode="edit" onOpenChange={(open) => !open && setEditingEntry(undefined)} open={Boolean(editingEntry)} projectId={projectId} />
        <DeleteEntryDialog entry={deletingEntry} onOpenChange={(open) => !open && setDeletingEntry(undefined)} open={Boolean(deletingEntry)} projectId={projectId} />
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
  setCollapsed: (value: Set<string>) => void;
  sourceById: Map<string, EntrySource>;
}

function TreeRow({ collapsed, depth = 0, node, onDelete, onEdit, setCollapsed, sourceById }: TreeRowProps) {
  if (node.kind === 'group') {
    const isCollapsed = collapsed.has(node.prefix);

    return (
      <div>
        <button className="flex w-full items-center gap-2 border-b border-stroke-subtle px-4 py-3 text-left hover:bg-bg-container" onClick={() => setCollapsed(toggle(collapsed, node.prefix))} style={{ paddingLeft: 16 + depth * 20 }} type="button">
          {isCollapsed ? <ChevronRight className="size-4 text-fg-icon-subtle" /> : <ChevronDown className="size-4 text-fg-icon-subtle" />}
          <InlineCode>{node.prefix}</InlineCode>
          <Badge variant="neutral">{node.count} entries</Badge>
        </button>
        {!isCollapsed ? node.children.map((child) => <TreeRow collapsed={collapsed} depth={depth + 1} key={child.kind === 'group' ? child.prefix : child.entry.id} node={child} onDelete={onDelete} onEdit={onEdit} setCollapsed={setCollapsed} sourceById={sourceById} />) : null}
      </div>
    );
  }

  const source = sourceById.get(node.entry.id);
  const isInherited = source?.kind === 'template';

  return (
    <div
      className={`group grid items-center gap-3 border-b border-stroke-subtle px-4 py-3 text-[13px] last:border-b-0 grid-cols-[minmax(220px,1.3fr)_120px_minmax(180px,1fr)_110px_140px_140px] ${isInherited ? 'cursor-default' : 'cursor-pointer hover:bg-bg-container'}`}
      onClick={isInherited ? undefined : () => onEdit(node.entry)}
      style={{ paddingLeft: 16 + depth * 20 }}
    >
      <InlineCode>{node.entry.key}</InlineCode>
      <Badge variant="neutral">{node.entry.valueType}</Badge>
      <SensitiveValue isSensitive={node.entry.isSensitive} value={defaultValue(node.entry)} />
      <Badge variant="info">{scopeCount(node.entry)} scopes</Badge>
      {source ? <OwnerBadge source={source} /> : <span />}
      {isInherited ? <span /> : (
        <div className="flex justify-end gap-1 opacity-0 transition-opacity group-hover:opacity-100">
          <Button onClick={(event) => { event.stopPropagation(); onEdit(node.entry); }} size="sm" type="button" variant="ghost">Edit</Button>
          <Button onClick={(event) => { event.stopPropagation(); onDelete(node.entry); }} size="sm" type="button" variant="ghost">Delete</Button>
        </div>
      )}
    </div>
  );
}

function OwnerBadge({ source }: { source: EntrySource }) {
  if (source.kind === 'project') {
    return <Badge variant="neutral">project</Badge>;
  }

  if (source.kind === 'template') {
    return (
      <Tooltip>
        <TooltipTrigger asChild>
          <span><Badge variant="info">inherited · {source.templateName}</Badge></span>
        </TooltipTrigger>
        <TooltipContent>Edit in the Templates page</TooltipContent>
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

function defaultValue(entry: ConfigEntry): string {
  return entry.values.find((value) => !value.scopes || Object.keys(value.scopes).length === 0)?.value ?? '';
}

function scopeCount(entry: ConfigEntry): number {
  return entry.values.filter((value) => value.scopes && Object.keys(value.scopes).length > 0).length;
}
