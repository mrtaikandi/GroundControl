import type { ConfigEntry } from '@/queries/useConfigEntries';

export type TreeNode =
  | { children: TreeNode[]; count: number; kind: 'group'; prefix: string; sensitiveCount: number }
  | { entry: ConfigEntry; kind: 'entry' };

interface MutableGroup {
  children: Map<string, MutableGroup>;
  entries: ConfigEntry[];
  prefix: string;
}

export function buildKeyTree(entries: ConfigEntry[]): TreeNode[] {
  const root: MutableGroup = { children: new Map(), entries: [], prefix: '' };

  for (const entry of entries) {
    const segments = entry.key.split(':').filter(Boolean);

    if (segments.length <= 1) {
      root.entries.push(entry);
      continue;
    }

    let current = root;

    for (const segment of segments.slice(0, -1)) {
      const prefix = current.prefix ? `${current.prefix}:${segment}` : segment;
      let child = current.children.get(segment);

      if (!child) {
        child = { children: new Map(), entries: [], prefix };
        current.children.set(segment, child);
      }

      current = child;
    }

    current.entries.push(entry);
  }

  return toNodes(root);
}

function toNodes(group: MutableGroup): TreeNode[] {
  const groups = Array.from(group.children.values()).sort((left, right) => left.prefix.localeCompare(right.prefix)).map((child) => {
    const children = toNodes(child);

    return { children, count: countEntries(children), kind: 'group' as const, prefix: child.prefix, sensitiveCount: countSensitive(children) };
  });
  const entries = group.entries.sort((left, right) => left.key.localeCompare(right.key)).map((entry) => ({ entry, kind: 'entry' as const }));

  return [...groups, ...entries];
}

function countEntries(nodes: TreeNode[]): number {
  return nodes.reduce((total, node) => total + (node.kind === 'entry' ? 1 : node.count), 0);
}

function countSensitive(nodes: TreeNode[]): number {
  return nodes.reduce((total, node) => {
    if (node.kind === 'entry') {
      return total + (node.entry.isSensitive ? 1 : 0);
    }

    return total + node.sensitiveCount;
  }, 0);
}
