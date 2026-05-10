import { createHighlighter, type Highlighter, type ThemeRegistration } from 'shiki';

type TowerThemeMode = 'light' | 'dark';

const themeCache = new WeakMap<Document, Map<TowerThemeMode, ThemeRegistration>>();
const highlighterCache = new WeakMap<Document, Map<TowerThemeMode, Promise<Highlighter>>>();

export function buildTowerTheme(mode: TowerThemeMode): ThemeRegistration {
  const ownerDocument = document;
  const cachedTheme = themeCache.get(ownerDocument)?.get(mode);

  if (cachedTheme) {
    return cachedTheme;
  }

  const theme: ThemeRegistration = {
    bg: token('--tower-bg-surface'),
    fg: token('--tower-fg-body'),
    name: `tower-${mode}`,
    settings: [
      { scope: ['punctuation', 'meta.brace', 'meta.delimiter'], settings: { foreground: token('--tower-syntax-punct') } },
      { scope: ['string', 'string.quoted.double.json'], settings: { foreground: token('--tower-syntax-string') } },
      { scope: ['constant.numeric', 'number'], settings: { foreground: token('--tower-syntax-number') } },
      { scope: ['support.type.property-name.json', 'variable', 'variable.other'], settings: { foreground: token('--tower-syntax-key') } },
      { scope: ['constant.language', 'keyword'], settings: { foreground: token('--tower-syntax-number') } },
    ],
    type: mode,
  };

  let documentCache = themeCache.get(ownerDocument);

  if (!documentCache) {
    documentCache = new Map<TowerThemeMode, ThemeRegistration>();
    themeCache.set(ownerDocument, documentCache);
  }

  documentCache.set(mode, theme);

  return theme;
}

export async function highlightJson(source: string, mode: TowerThemeMode): Promise<string> {
  const highlighter = await getHighlighter(mode);
  const html = highlighter.codeToHtml(source, {
    lang: 'json',
    theme: buildTowerTheme(mode),
  });

  return splitScopedKeys(html);
}

export async function highlightJsonLines(source: string, mode: TowerThemeMode): Promise<string[]> {
  const html = await highlightJson(source, mode);

  return extractLines(html);
}

function extractLines(html: string): string[] {
  const codeMatch = /<code[^>]*>([\s\S]*?)<\/code>/.exec(html);
  if (!codeMatch) {
    return [html];
  }

  const codeContent = codeMatch[1];
  const lineRegex = /<span class="line">([\s\S]*?)<\/span>(?=\n|$)/g;
  const lines: string[] = [];
  let match: RegExpExecArray | null;
  while ((match = lineRegex.exec(codeContent)) !== null) {
    lines.push(match[1]);
  }

  return lines.length > 0 ? lines : codeContent.split('\n');
}

function splitScopedKeys(html: string): string {
  const keyColor = token('--tower-syntax-key');
  const dimensionColor = token('--tower-syntax-scope-dimension');
  const valueColor = token('--tower-syntax-scope-value');

  if (!keyColor || !dimensionColor || !valueColor) {
    return html;
  }

  const pattern = new RegExp(
    `<span style="color:\\s*${escapeRegex(keyColor)}[^"]*"([^>]*)>"([^"]*=[^"]*)"</span>`,
    'gi',
  );

  return html.replace(pattern, (_match, attrs: string, content: string) => {
    const parts = content.split(/(,\s*)/).map((segment) => {
      if (/^,\s*$/.test(segment)) {
        return `<span style="color:${keyColor}"${attrs}>${segment}</span>`;
      }

      const eqIndex = segment.indexOf('=');
      if (eqIndex === -1) {
        return `<span style="color:${dimensionColor}"${attrs}>${segment}</span>`;
      }

      const dimension = segment.slice(0, eqIndex);
      const value = segment.slice(eqIndex + 1);

      return [
        `<span style="color:${dimensionColor}"${attrs}>${dimension}</span>`,
        `<span style="color:${keyColor}"${attrs}>=</span>`,
        `<span style="color:${valueColor}"${attrs}>${value}</span>`,
      ].join('');
    }).join('');

    return [
      `<span style="color:${keyColor}"${attrs}>"</span>`,
      parts,
      `<span style="color:${keyColor}"${attrs}>"</span>`,
    ].join('');
  });
}

function escapeRegex(value: string): string {
  return value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}

async function getHighlighter(mode: TowerThemeMode): Promise<Highlighter> {
  const ownerDocument = document;
  let documentCache = highlighterCache.get(ownerDocument);

  if (!documentCache) {
    documentCache = new Map<TowerThemeMode, Promise<Highlighter>>();
    highlighterCache.set(ownerDocument, documentCache);
  }

  const cachedHighlighter = documentCache.get(mode);

  if (cachedHighlighter) {
    return cachedHighlighter;
  }

  const highlighter = createHighlighter({
    langs: ['json'],
    themes: [buildTowerTheme(mode)],
  });

  documentCache.set(mode, highlighter);

  return highlighter;
}

function token(name: string): string {
  return getComputedStyle(document.documentElement).getPropertyValue(name).trim();
}