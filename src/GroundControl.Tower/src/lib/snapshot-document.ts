import type { SnapshotDetail } from '@/queries/useSnapshots';

export type SnapshotEntry = SnapshotDetail['entries'][number];

export interface ResolvableEntry {
  isSensitive: boolean;
  key: string;
  values: ReadonlyArray<{ scopes?: null | Record<string, string>; value: string }>;
  valueType: string;
}

export function snapshotToDocument(snapshot?: SnapshotDetail, options: { maskSensitive?: boolean } = {}): Record<string, unknown> {
  return entriesToDocument(snapshot?.entries, options);
}

export function entriesToDocument(entries: readonly ResolvableEntry[] | undefined, options: { maskSensitive?: boolean } = {}): Record<string, unknown> {
  const document: Record<string, unknown> = {};

  for (const entry of entries ?? []) {
    setPath(document, entry.key.split(':').filter(Boolean), entryToValue(entry, options));
  }

  return document;
}

function entryToValue(entry: ResolvableEntry, options: { maskSensitive?: boolean }): unknown {
  const mask = options.maskSensitive && entry.isSensitive;

  if (entry.values.length === 1 && Object.keys(entry.values[0]?.scopes ?? {}).length === 0) {
    return mask ? '••••••••' : coerceValue(entry.values[0]?.value ?? null, entry.valueType);
  }

  return Object.fromEntries(entry.values.map((value) => [scopeLabel(value.scopes), mask ? '••••••••' : coerceValue(value.value, entry.valueType)]));
}

function scopeLabel(scopes: null | Record<string, string> | undefined) {
  if (!scopes) {
    return 'default';
  }


  const entries = Object.entries(scopes);

  return entries.length === 0 ? 'default' : entries.map(([dimension, value]) => `${dimension}=${value}`).join(', ');
}

function setPath(document: Record<string, unknown>, path: string[], value: unknown) {
  if (path.length === 0) {
    return;
  }

  let target = document;

  for (const segment of path.slice(0, -1)) {
    if (!isRecord(target[segment])) {
      target[segment] = {};
    }

    target = target[segment] as Record<string, unknown>;
  }

  target[path[path.length - 1]!] = value;
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

const numericValueTypes: ReadonlySet<string> = new Set(['int32', 'int64', 'double', 'decimal']);

function coerceValue(value: null | string, valueType: string): unknown {
  if (value === null) {
    return null;
  }

  const normalized = valueType.toLowerCase();

  if (numericValueTypes.has(normalized)) {
    const numberValue = Number(value);

    return Number.isFinite(numberValue) ? numberValue : value;
  }

  if (normalized === 'boolean') {
    if (value.toLowerCase() === 'true') {
      return true;
    }

    if (value.toLowerCase() === 'false') {
      return false;
    }
  }

  return value;
}

export function snapshotToResolvedDocument(snapshot: SnapshotDetail | undefined, scopes: Record<string, string>, options: { maskSensitive?: boolean } = {}): Record<string, unknown> {
  return entriesToResolvedDocument(snapshot?.entries, scopes, options);
}

export function entriesToResolvedDocument(
  entries: readonly ResolvableEntry[] | undefined,
  scopes: Record<string, string>,
  options: { maskSensitive?: boolean } = {},
): Record<string, unknown> {
  const document: Record<string, unknown> = {};

  for (const entry of entries ?? []) {
    const value = resolveScopedValue(entry.values, scopes);

    setPath(document, entry.key.split(':').filter(Boolean), options.maskSensitive && entry.isSensitive ? '••••••••' : coerceValue(value?.value ?? null, entry.valueType));
  }

  return document;
}

function resolveScopedValue(values: ResolvableEntry['values'], scopes: Record<string, string>) {
  let unscopedDefault: ResolvableEntry['values'][number] | undefined;
  let bestMatch: ResolvableEntry['values'][number] | undefined;
  let bestSpecificity = 0;

  for (const candidate of values) {
    const candidateScopes = candidate.scopes ?? {};
    const specificity = Object.keys(candidateScopes).length;

    if (specificity === 0) {
      unscopedDefault = candidate;
      continue;
    }

    if (Object.entries(candidateScopes).every(([dimension, value]) => lookupScope(scopes, dimension) === value) && specificity > bestSpecificity) {
      bestMatch = candidate;
      bestSpecificity = specificity;
    }
  }

  return bestMatch ?? unscopedDefault;
}

function lookupScope(scopes: Record<string, string>, dimension: string): string | undefined {
  if (dimension in scopes) {
    return scopes[dimension];
  }

  const lower = dimension.toLowerCase();

  for (const key in scopes) {
    if (key.toLowerCase() === lower) {
      return scopes[key];
    }
  }

  return undefined;
}