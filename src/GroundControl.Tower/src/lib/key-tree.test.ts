import { describe, expect, it } from 'vitest';
import { buildKeyTree } from './key-tree';
import type { ConfigEntry } from '@/queries/useConfigEntries';

describe('buildKeyTree', () => {
  it('keeps flat keys as root entries', () => {
    const tree = buildKeyTree([entry('feature')]);

    expect(tree).toMatchObject([{ kind: 'entry', entry: { key: 'feature' } }]);
  });

  it('groups colon-separated keys by prefix', () => {
    const tree = buildKeyTree([entry('database:host'), entry('database:port')]);

    expect(tree).toMatchObject([{ kind: 'group', prefix: 'database', count: 2, sensitiveCount: 0, children: [{ kind: 'entry', entry: { key: 'database:host' } }, { kind: 'entry', entry: { key: 'database:port' } }] }]);
  });

  it('handles mixed nesting', () => {
    const tree = buildKeyTree([entry('api'), entry('database:primary:host'), entry('database:replica:host')]);

    expect(tree).toMatchObject([{ kind: 'group', prefix: 'database', count: 2 }, { kind: 'entry', entry: { key: 'api' } }]);
  });

  it('does not group keys that use dots as separators', () => {
    const tree = buildKeyTree([entry('database.host'), entry('database.port')]);

    expect(tree).toMatchObject([{ kind: 'entry', entry: { key: 'database.host' } }, { kind: 'entry', entry: { key: 'database.port' } }]);
  });

  it('counts sensitive descendants per group', () => {
    const tree = buildKeyTree([
      entry('database:host'),
      entry('database:password', true),
      entry('encryption:keys:primary', true),
    ]);

    expect(tree).toMatchObject([
      { kind: 'group', prefix: 'database', count: 2, sensitiveCount: 1 },
      { kind: 'group', prefix: 'encryption', count: 1, sensitiveCount: 1, children: [{ kind: 'group', prefix: 'encryption:keys', count: 1, sensitiveCount: 1 }] },
    ]);
  });
});

function entry(key: string, isSensitive = false): ConfigEntry {
  return {
    createdAt: new Date().toISOString(),
    createdBy: 'user',
    id: key,
    isSensitive,
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