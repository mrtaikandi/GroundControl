import { ChevronDown, ChevronRight } from 'lucide-react';
import { useMemo, useState } from 'react';
import { Badge } from '@/components/tower/data/Badge';
import { InlineCode } from '@/components/tower/data/InlineCode';
import { SensitiveValue } from '@/components/tower/code/SensitiveValue';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { useConfigEntries, type ConfigEntry } from '@/queries/useConfigEntries';
import { buildKeyTree, type TreeNode } from '@/lib/key-tree';
import { DeleteEntryDialog } from './DeleteEntryDialog';
import { EntryModal } from './EntryModal';

interface ConfigTreeViewProps {
  projectId: string;
}

export function ConfigTreeView({ projectId }: ConfigTreeViewProps) {
  const entries = useConfigEntries(projectId);
  const [collapsed, setCollapsed] = useState<Set<string>>(() => new Set());
  const [editingEntry, setEditingEntry] = useState<ConfigEntry | undefined>();
  const [deletingEntry, setDeletingEntry] = useState<ConfigEntry | undefined>();
  const tree = useMemo(() => buildKeyTree(entries.data?.data ?? []), [entries.data?.data]);
  const allPrefixes = useMemo(() => collectPrefixes(tree), [tree]);

  if (entries.isLoading) {
    return <Skeleton className="h-96" />;
  }

  return (
    <div className="grid gap-4">
      <div className="flex justify-end gap-2">
        <Button onClick={() => setCollapsed(new Set())} type="button" variant="ghost">Expand all</Button>
        <Button onClick={() => setCollapsed(new Set(allPrefixes))} type="button" variant="ghost">Collapse all</Button>
      </div>
      <div className="overflow-hidden rounded-xl border border-stroke-subtle bg-bg-surface">
        {tree.length === 0 ? <div className="p-10 text-center text-fg-caption">No entries found.</div> : tree.map((node) => <TreeRow collapsed={collapsed} key={node.kind === 'group' ? node.prefix : node.entry.id} node={node} onDelete={setDeletingEntry} onEdit={setEditingEntry} setCollapsed={setCollapsed} />)}
      </div>
      <EntryModal entry={editingEntry} mode="edit" onOpenChange={(open) => !open && setEditingEntry(undefined)} open={Boolean(editingEntry)} projectId={projectId} />
      <DeleteEntryDialog entry={deletingEntry} onOpenChange={(open) => !open && setDeletingEntry(undefined)} open={Boolean(deletingEntry)} projectId={projectId} />
    </div>
  );
}

function TreeRow({ collapsed, depth = 0, node, onDelete, onEdit, setCollapsed }: { collapsed: Set<string>; depth?: number; node: TreeNode; onDelete: (entry: ConfigEntry) => void; onEdit: (entry: ConfigEntry) => void; setCollapsed: (value: Set<string>) => void }) {
  if (node.kind === 'group') {
    const isCollapsed = collapsed.has(node.prefix);

    return (
      <div>
        <button className="flex w-full items-center gap-2 border-b border-stroke-subtle px-4 py-3 text-left hover:bg-bg-container" onClick={() => setCollapsed(toggle(collapsed, node.prefix))} style={{ paddingLeft: 16 + depth * 20 }} type="button">
          {isCollapsed ? <ChevronRight className="size-4 text-fg-icon-subtle" /> : <ChevronDown className="size-4 text-fg-icon-subtle" />}
          <InlineCode>{node.prefix}</InlineCode>
          <Badge variant="neutral">{node.count} entries</Badge>
        </button>
        {!isCollapsed ? node.children.map((child) => <TreeRow collapsed={collapsed} depth={depth + 1} key={child.kind === 'group' ? child.prefix : child.entry.id} node={child} onDelete={onDelete} onEdit={onEdit} setCollapsed={setCollapsed} />) : null}
      </div>
    );
  }

  return (
    <div className="group grid cursor-pointer grid-cols-[minmax(220px,1.3fr)_120px_minmax(180px,1fr)_110px_140px] items-center gap-3 border-b border-stroke-subtle px-4 py-3 text-[13px] last:border-b-0 hover:bg-bg-container" onClick={() => onEdit(node.entry)} style={{ paddingLeft: 16 + depth * 20 }}>
      <InlineCode>{node.entry.key}</InlineCode>
      <Badge variant="neutral">{node.entry.valueType}</Badge>
      <SensitiveValue isSensitive={node.entry.isSensitive} value={defaultValue(node.entry)} />
      <Badge variant="info">{scopeCount(node.entry)} scopes</Badge>
      <div className="flex justify-end gap-1 opacity-0 transition-opacity group-hover:opacity-100"><Button onClick={(event) => { event.stopPropagation(); onEdit(node.entry); }} size="sm" type="button" variant="ghost">Edit</Button><Button onClick={(event) => { event.stopPropagation(); onDelete(node.entry); }} size="sm" type="button" variant="ghost">Delete</Button></div>
    </div>
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