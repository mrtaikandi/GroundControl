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

  return highlighter.codeToHtml(source, {
    lang: 'json',
    theme: buildTowerTheme(mode),
  });
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