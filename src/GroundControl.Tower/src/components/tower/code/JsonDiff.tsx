import { diffLines } from 'diff';
import { useEffect, useMemo, useState } from 'react';
import { useTweaksStore } from '@/store/tweaks';
import { cn } from '@/lib/utils';
import { highlightJson } from './shiki-theme';

interface JsonDiffProps {
  after: unknown;
  before: unknown;
  className?: string;
  mode?: 'inline' | 'split';
}

interface DiffLine {
  content: string;
  kind: 'add' | 'del' | 'same';
}

interface HighlightedDiffLine extends DiffLine {
  html: string;
}

interface SplitRow {
  left: HighlightedDiffLine | null;
  right: HighlightedDiffLine | null;
}

export function JsonDiff({ after, before, className, mode = 'inline' }: JsonDiffProps) {
  const theme = useTweaksStore((state) => state.theme);
  const lines = useMemo(() => buildDiffLines(before, after), [after, before]);
  const [highlightedLines, setHighlightedLines] = useState<HighlightedDiffLine[]>([]);
  const splitRows = useMemo(() => buildSplitRows(highlightedLines), [highlightedLines]);

  useEffect(() => {
    let cancelled = false;

    void Promise.all(lines.map(async (line) => ({ ...line, html: extractCode(await highlightJson(line.content || ' ', theme)) }))).then((nextLines) => {
      if (!cancelled) {
        setHighlightedLines(nextLines);
      }
    });

    return () => {
      cancelled = true;
    };
  }, [lines, theme]);

  if (mode === 'split') {
    return (
      <div className={cn('ui-surface-card ui-text-code grid divide-x divide-stroke-subtle md:grid-cols-2', className)}>
        <DiffColumn rows={splitRows.map((row) => row.left)} side="left" title="Before" />
        <DiffColumn rows={splitRows.map((row) => row.right)} side="right" title="After" />
      </div>
    );
  }

  return (
    <div className={cn('ui-surface-card ui-text-code overflow-auto py-4', className)}>
      <div className="min-w-max">
        {highlightedLines.map((line, index) => <DiffRow index={index} key={`${line.kind}-${index}-${line.content}`} line={line} />)}
      </div>
    </div>
  );
}

interface DiffColumnProps {
  rows: (HighlightedDiffLine | null)[];
  side: 'left' | 'right';
  title: string;
}

function DiffColumn({ rows, side, title }: DiffColumnProps) {
  let lineIndex = 0;

  return (
    <div className="min-w-0">
      <div className="ui-text-caption sticky top-0 z-10 border-b border-stroke-subtle bg-bg-surface px-3 py-2 font-medium text-fg-caption">{title}</div>
      <div>
        {rows.map((row, index) => {
          if (row === null) {
            return <DiffPlaceholderRow key={`${side}-empty-${index}`} />;
          }

          const currentIndex = lineIndex++;
          return <DiffRow index={currentIndex} key={`${side}-${row.kind}-${index}-${row.content}`} line={row} />;
        })}
      </div>
    </div>
  );
}

function DiffRow({ index, line }: { index: number; line: HighlightedDiffLine }) {
  return (
    <div className={cn('grid grid-cols-[24px_1fr] gap-2 px-3 py-0.5', line.kind === 'add' && 'bg-syntax-diff-add-bg', line.kind === 'del' && 'bg-syntax-diff-del-bg')}>
      <span className={cn('select-none text-right text-fg-caption', line.kind === 'add' && 'text-syntax-diff-add-fg', line.kind === 'del' && 'text-syntax-diff-del-fg')}>
        {line.kind === 'add' ? '+' : line.kind === 'del' ? '-' : index + 1}
      </span>
      <code className="min-w-0 whitespace-pre-wrap break-all" dangerouslySetInnerHTML={{ __html: line.html }} />
    </div>
  );
}

function DiffPlaceholderRow() {
  return (
    <div aria-hidden="true" className="grid grid-cols-[24px_1fr] gap-2 bg-bg-container/40 px-3 py-0.5">
      <span className="select-none">&nbsp;</span>
      <code className="min-w-0">&nbsp;</code>
    </div>
  );
}

function buildDiffLines(before: unknown, after: unknown): DiffLine[] {
  return diffLines(toJson(before), toJson(after)).flatMap((part) => part.value.replace(/\n$/, '').split('\n').map((content) => ({ content, kind: part.added ? 'add' : part.removed ? 'del' : 'same' })));
}

function buildSplitRows(lines: HighlightedDiffLine[]): SplitRow[] {
  const rows: SplitRow[] = [];
  let cursor = 0;

  while (cursor < lines.length) {
    const line = lines[cursor];

    if (line.kind === 'same') {
      rows.push({ left: line, right: line });
      cursor += 1;
      continue;
    }

    if (line.kind === 'del') {
      const dels: HighlightedDiffLine[] = [];
      while (cursor < lines.length && lines[cursor].kind === 'del') {
        dels.push(lines[cursor]);
        cursor += 1;
      }

      const adds: HighlightedDiffLine[] = [];
      while (cursor < lines.length && lines[cursor].kind === 'add') {
        adds.push(lines[cursor]);
        cursor += 1;
      }

      const pairCount = Math.max(dels.length, adds.length);
      for (let pairIndex = 0; pairIndex < pairCount; pairIndex += 1) {
        rows.push({ left: dels[pairIndex] ?? null, right: adds[pairIndex] ?? null });
      }

      continue;
    }

    const adds: HighlightedDiffLine[] = [];
    while (cursor < lines.length && lines[cursor].kind === 'add') {
      adds.push(lines[cursor]);
      cursor += 1;
    }

    for (const add of adds) {
      rows.push({ left: null, right: add });
    }
  }

  return rows;
}

function toJson(value: unknown): string {
  return `${JSON.stringify(value, null, 2)}\n`;
}

function extractCode(html: string): string {
  const match = /<code>(?<code>[\s\S]*)<\/code>/.exec(html);

  return match?.groups?.code.replace(/\n$/, '') ?? '';
}
