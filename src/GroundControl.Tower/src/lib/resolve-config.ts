import type { ConfigEntry } from '@/queries/useConfigEntries';

export interface ResolvedConfigEntry {
  isSensitive: boolean;
  key: string;
  value: null | string;
  valueType: string;
}

export function resolveConfigEntries(entries: ConfigEntry[], scopes: Record<string, string>): ResolvedConfigEntry[] {
  return entries.map((entry) => {
    const resolvedValue = resolveScopedValue(entry.values, scopes);

    return {
      isSensitive: entry.isSensitive,
      key: entry.key,
      value: resolvedValue?.value ?? null,
      valueType: entry.valueType,
    };
  });
}

export function buildResolvedDocument(entries: ResolvedConfigEntry[], options: { maskSensitive: boolean }): Record<string, unknown> {
  const document: Record<string, unknown> = {};

  for (const entry of entries) {
    const value = options.maskSensitive && entry.isSensitive ? '••••••••' : coerceValue(entry.value, entry.valueType);
    setPath(document, entry.key.split('.').filter(Boolean), value);
  }

  return document;
}

function resolveScopedValue(values: ConfigEntry['values'], scopes: Record<string, string>) {
  let unscopedDefault: ConfigEntry['values'][number] | undefined;
  let bestMatch: ConfigEntry['values'][number] | undefined;
  let bestSpecificity = 0;

  for (const candidate of values) {
    const candidateScopes = candidate.scopes ?? {};
    const specificity = Object.keys(candidateScopes).length;

    if (specificity === 0) {
      unscopedDefault = candidate;
      continue;
    }

    if (!isFullMatch(candidateScopes, scopes)) {
      continue;
    }

    if (specificity > bestSpecificity) {
      bestMatch = candidate;
      bestSpecificity = specificity;
    }
  }

  return bestMatch ?? unscopedDefault;
}

function isFullMatch(candidateScopes: Record<string, string>, scopes: Record<string, string>) {
  return Object.entries(candidateScopes).every(([dimension, value]) => scopes[dimension] === value);
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