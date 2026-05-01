import { describe, expect, it } from 'vitest';
import { buildResolvedDocument, resolveConfigEntries } from './resolve-config';
import type { ConfigEntry } from '@/queries/useConfigEntries';

describe('resolveConfigEntries', () => {
  it('uses default values when no scoped value matches', () => {
    const resolved = resolveConfigEntries([
      entry('Feature:Checkout:Enabled', 'boolean', [
        { scopes: {}, value: 'false' },
        { scopes: { Environment: 'prod' }, value: 'true' },
      ]),
    ], { Environment: 'dev' });

    expect(buildResolvedDocument(resolved, { maskSensitive: false })).toEqual({ feature: { checkout: { enabled: false } } });
  });

  it('uses the most specific matching scoped value', () => {
    const resolved = resolveConfigEntries([
      entry('service.replicas', 'number', [
        { scopes: {}, value: '1' },
        { scopes: { Environment: 'prod' }, value: '2' },
        { scopes: { Environment: 'prod', Region: 'eu-west' }, value: '4' },
      ]),
    ], { Environment: 'prod', Region: 'eu-west' });

    expect(buildResolvedDocument(resolved, { maskSensitive: false })).toEqual({ service: { replicas: 4 } });
  });

  it('masks sensitive values when requested', () => {
    const resolved = resolveConfigEntries([entry('database.password', 'string', [{ scopes: {}, value: 'secret' }], true)], {});

    expect(buildResolvedDocument(resolved, { maskSensitive: true })).toEqual({ database: { password: '••••••••' } });
  });
});

function entry(key: string, valueType: string, values: ConfigEntry['values'], isSensitive = false): ConfigEntry {
  return {
    createdAt: '2025-01-01T00:00:00Z',
    createdBy: '00000000-0000-0000-0000-000000000000',
    description: null,
    id: crypto.randomUUID(),
    isSensitive,
    key,
    ownerId: '00000000-0000-0000-0000-000000000000',
    ownerType: 1,
    updatedAt: '2025-01-01T00:00:00Z',
    updatedBy: '00000000-0000-0000-0000-000000000000',
    values,
    valueType,
    version: 1,
  };
}