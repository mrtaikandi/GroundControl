import { describe, expect, it } from 'vitest';
import { buildKeyTree } from './key-tree';
import type { ConfigEntry } from '@/queries/useConfigEntries';

describe('buildKeyTree', () => {
  it('keeps flat keys as root entries', () => {
    const tree = buildKeyTree([entry('feature')]);

    expect(tree).toMatchObject([{ kind: 'entry', entry: { key: 'feature' } }]);
  });

  it('groups dotted keys by prefix', () => {
    const tree = buildKeyTree([entry('database.host'), entry('database.port')]);

    expect(tree).toMatchObject([{ kind: 'group', prefix: 'database', count: 2, children: [{ kind: 'entry', entry: { key: 'database.host' } }, { kind: 'entry', entry: { key: 'database.port' } }] }]);
  });

  it('handles mixed nesting', () => {
    const tree = buildKeyTree([entry('api'), entry('database.primary.host'), entry('database.replica.host')]);

    expect(tree).toMatchObject([{ kind: 'group', prefix: 'database', count: 2 }, { kind: 'entry', entry: { key: 'api' } }]);
  });
});

function entry(key: string): ConfigEntry {
  return {
    createdAt: new Date().toISOString(),
    createdBy: 'user',
    id: key,
    isSensitive: false,
    key,
    ownerId: 'project',
    ownerType: 1,
    updatedAt: new Date().toISOString(),
    updatedBy: 'user',
    valueType: 'string',
    values: [{ scopes: {}, value: 'value' }],
    version: 1,
  };
}