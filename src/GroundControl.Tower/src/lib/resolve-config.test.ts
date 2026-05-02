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

    expect(buildResolvedDocument(resolved, { maskSensitive: false })).toEqual({ Feature: { Checkout: { Enabled: false } } });
  });

  it('uses the most specific matching scoped value', () => {
    const resolved = resolveConfigEntries([
      entry('Service:Replicas', 'Int32', [
        { scopes: {}, value: '1' },
        { scopes: { Environment: 'prod' }, value: '2' },
        { scopes: { Environment: 'prod', Region: 'eu-west' }, value: '4' },
      ]),
    ], { Environment: 'prod', Region: 'eu-west' });

    expect(buildResolvedDocument(resolved, { maskSensitive: false })).toEqual({ Service: { Replicas: 4 } });
  });

  it('coerces double values to JS numbers', () => {
    const resolved = resolveConfigEntries([
      entry('Service:LoadFactor', 'Double', [{ scopes: {}, value: '0.75' }]),
    ], {});

    expect(buildResolvedDocument(resolved, { maskSensitive: false })).toEqual({ Service: { LoadFactor: 0.75 } });
  });

  it('matches value type case-insensitively', () => {
    const resolved = resolveConfigEntries([
      entry('Service:Threshold', 'decimal', [{ scopes: {}, value: '12.5' }]),
    ], {});

    expect(buildResolvedDocument(resolved, { maskSensitive: false })).toEqual({ Service: { Threshold: 12.5 } });
  });

  it('masks sensitive values when requested', () => {
    const resolved = resolveConfigEntries([entry('Database:Password', 'string', [{ scopes: {}, value: 'secret' }], true)], {});

    expect(buildResolvedDocument(resolved, { maskSensitive: true })).toEqual({ Database: { Password: '••••••••' } });
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