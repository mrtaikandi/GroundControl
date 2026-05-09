import { mkdir, readFile, writeFile } from 'node:fs/promises';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

type TokenMode = 'light' | 'dark';

type JsonValue = string | number | boolean | null | JsonValue[] | JsonObject;

interface JsonObject {
  [key: string]: JsonValue;
}

interface CssToken {
  name: string;
  light: string;
  dark: string;
}

const scriptDirectory = dirname(fileURLToPath(import.meta.url));
const projectDirectory = resolve(scriptDirectory, '..');
const tokenSourcePath = resolve(projectDirectory, 'design-tokens/tokens.json');
const outputPath = resolve(projectDirectory, 'src/styles/tokens.css');

function isObject(value: JsonValue | undefined): value is JsonObject {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

function toCssName(pathSegments: string[]): string {
  return `--tower-${pathSegments.join('-').replace(/[A-Z]/g, (match) => `-${match.toLowerCase()}`)}`;
}

function readPath(root: JsonObject, path: string): JsonObject {
  const value = path.split('.').reduce<JsonValue | undefined>((current, segment) => (isObject(current) ? current[segment] : undefined), root);

  if (!isObject(value)) {
    throw new Error(`Token reference {${path}} does not point to an object token.`);
  }

  return value;
}

function resolveTokenValue(root: JsonObject, token: JsonObject, mode: TokenMode): string {
  const rawValue = token.$value;

  if (typeof rawValue === 'string') {
    const referenceMatch = /^\{(?<path>[^}]+)\}$/.exec(rawValue);

    if (referenceMatch?.groups?.path) {
      return resolveTokenValue(root, readPath(root, referenceMatch.groups.path), mode);
    }

    return rawValue;
  }

  if (typeof rawValue === 'number') {
    return rawValue.toString();
  }

  if (isObject(rawValue)) {
    const modeValue = rawValue[mode];

    if (typeof modeValue === 'string') {
      const referenceMatch = /^\{(?<path>[^}]+)\}$/.exec(modeValue);

      if (referenceMatch?.groups?.path) {
        return resolveTokenValue(root, readPath(root, referenceMatch.groups.path), mode);
      }

      return modeValue;
    }

    if (typeof modeValue === 'number') {
      return modeValue.toString();
    }
  }

  throw new Error(`Token is missing a ${mode} value.`);
}

function collectSemanticTokens(root: JsonObject, current: JsonObject, pathSegments: string[] = []): CssToken[] {
  const tokens: CssToken[] = [];

  for (const [key, value] of Object.entries(current)) {
    if (key.startsWith('$') || !isObject(value)) {
      continue;
    }

    const nextPathSegments = [...pathSegments, key];

    if ('$value' in value) {
      tokens.push({
        name: toCssName(nextPathSegments),
        light: resolveTokenValue(root, value, 'light'),
        dark: resolveTokenValue(root, value, 'dark'),
      });

      continue;
    }

    tokens.push(...collectSemanticTokens(root, value, nextPathSegments));
  }

  return tokens;
}

function renderBlock(selector: string, tokens: CssToken[], mode: TokenMode): string {
  const declarations = tokens.map((token) => `  ${token.name}: ${token[mode]};`).join('\n');

  return `${selector} {\n${declarations}\n}`;
}

function renderShadcnBridge(): string {
  return [
    ':root {',
    '  /* shadcn variable bridge */',
    '  --background: var(--tower-bg-page);',
    '  --foreground: var(--tower-fg-body);',
    '  --card: var(--tower-bg-surface);',
    '  --card-foreground: var(--tower-fg-body);',
    '  --popover: var(--tower-bg-surface);',
    '  --popover-foreground: var(--tower-fg-body);',
    '  --primary: var(--tower-stroke-field-focus);',
    '  --primary-foreground: var(--tower-fg-chip-selected);',
    '  --secondary: var(--tower-bg-surface);',
    '  --secondary-foreground: var(--tower-fg-heading);',
    '  --muted: var(--tower-bg-container);',
    '  --muted-foreground: var(--tower-fg-caption);',
    '  --accent: var(--tower-bg-selected);',
    '  --accent-foreground: var(--tower-fg-heading);',
    '  --destructive: var(--tower-interaction-button-danger-background);',
    '  --destructive-foreground: var(--tower-interaction-button-danger-foreground);',
    '  --border: var(--tower-stroke-subtle);',
    '  --input: var(--tower-stroke-field-initial);',
    '  --ring: var(--tower-stroke-field-focus);',
    '  --radius: var(--radius-lg);',
    '}',
  ].join('\n');
}

const tokenSource = JSON.parse(await readFile(tokenSourcePath, 'utf8')) as JsonObject;
const semantic = tokenSource.semantic;

if (!isObject(semantic)) {
  throw new Error('tokens.json is missing a semantic token object.');
}

const tokens = collectSemanticTokens(tokenSource, semantic);
const css = [
  '/* AUTO-GENERATED — do not edit. Source: tokens.json */',
  '',
  renderBlock(':root', tokens, 'light'),
  '',
  renderBlock('[data-tower-theme="dark"]', tokens, 'dark'),
  '',
  renderShadcnBridge(),
  '',
].join('\n');

await mkdir(dirname(outputPath), { recursive: true });
await writeFile(outputPath, css, 'utf8');