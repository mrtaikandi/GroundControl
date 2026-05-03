export interface ChangeSet {
  additions: string[];
  deletions: string[];
  modifications: string[];
}

export interface ChangeSummary {
  additions: number;
  deletions: number;
  modifications: number;
}

export function diffDocuments(before: Record<string, unknown>, after: Record<string, unknown>): ChangeSet {
  const beforeFlat = flattenDocument(before);
  const afterFlat = flattenDocument(after);
  const additions: string[] = [];
  const modifications: string[] = [];
  const deletions: string[] = [];

  for (const [key, value] of afterFlat) {
    if (!beforeFlat.has(key)) {
      additions.push(key);
    } else if (!deepEqual(beforeFlat.get(key), value)) {
      modifications.push(key);
    }
  }

  for (const key of beforeFlat.keys()) {
    if (!afterFlat.has(key)) {
      deletions.push(key);
    }
  }

  return { additions, deletions, modifications };
}

export function summarize(changes: ChangeSet): ChangeSummary {
  return {
    additions: changes.additions.length,
    deletions: changes.deletions.length,
    modifications: changes.modifications.length,
  };
}

export function deepEqual(left: unknown, right: unknown): boolean {
  return JSON.stringify(left) === JSON.stringify(right);
}

function flattenDocument(value: unknown, prefix = ''): Map<string, unknown> {
  if (!isRecord(value)) {
    return new Map([[prefix, value]]);
  }

  const flattened = new Map<string, unknown>();

  for (const [key, child] of Object.entries(value)) {
    const path = prefix ? `${prefix}:${key}` : key;

    if (isRecord(child)) {
      for (const [childKey, childValue] of flattenDocument(child, path)) {
        flattened.set(childKey, childValue);
      }
    } else {
      flattened.set(path, child);
    }
  }

  return flattened;
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}
