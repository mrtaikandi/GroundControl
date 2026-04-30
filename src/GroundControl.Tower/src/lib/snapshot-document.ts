import type { SnapshotDetail } from '@/queries/useSnapshots';

export function snapshotToDocument(snapshot?: SnapshotDetail): Record<string, unknown> {
  const document: Record<string, unknown> = {};

  for (const entry of snapshot?.entries ?? []) {
    setPath(document, entry.key.split('.').filter(Boolean), entryToValue(entry));
  }

  return document;
}

function entryToValue(entry: SnapshotDetail['entries'][number]): unknown {
  if (entry.values.length === 1 && Object.keys(entry.values[0]?.scopes ?? {}).length === 0) {
    return coerceValue(entry.values[0]?.value ?? null, entry.valueType);
  }

  return Object.fromEntries(entry.values.map((value) => [scopeLabel(value.scopes), coerceValue(value.value, entry.valueType)]));
}

function scopeLabel(scopes: Record<string, string>) {
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

function coerceValue(value: null | string, valueType: string): unknown {
  if (value === null) {
    return null;
  }

  if (valueType === 'number') {
    const numberValue = Number(value);

    return Number.isFinite(numberValue) ? numberValue : value;
  }

  if (valueType === 'boolean') {
    if (value.toLowerCase() === 'true') {
      return true;
    }

    if (value.toLowerCase() === 'false') {
      return false;
    }
  }

  if (valueType === 'json') {
    try {
      return JSON.parse(value) as unknown;
    } catch {
      return value;
    }
  }

  return value;
}