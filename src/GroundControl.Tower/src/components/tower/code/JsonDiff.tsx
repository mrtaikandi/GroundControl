import { diffLines } from 'diff';
import { useEffect, useMemo, useState } from 'react';
import { useTweaksStore } from '@/store/tweaks';
import { cn } from '@/lib/utils';
import { highlightJson } from './shiki-theme';

interface JsonDiffProps {
  after: unknown;
  before: unknown;
  className?: string;
  mode?: 'unified' | 'split';
}

interface DiffLine {
  content: string;
  kind: 'add' | 'del' | 'same';
}

interface HighlightedDiffLine extends DiffLine {
  html: string;
}

export function JsonDiff({ after, before, className, mode = 'unified' }: JsonDiffProps) {
  const theme = useTweaksStore((state) => state.theme);
  const lines = useMemo(() => buildDiffLines(before, after), [after, before]);
  const [highlightedLines, setHighlightedLines] = useState<HighlightedDiffLine[]>([]);

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
      <div className={cn('grid gap-3 rounded-xl bg-bg-surface p-4 font-mono text-[12.5px] md:grid-cols-2', className)}>
        <DiffColumn lines={highlightedLines.filter((line) => line.kind !== 'add')} title="Before" />
        <DiffColumn lines={highlightedLines.filter((line) => line.kind !== 'del')} title="After" />
      </div>
    );
  }

  return (
    <div className={cn('overflow-auto rounded-xl bg-bg-surface py-4 font-mono text-[12.5px]', className)}>
      <div className="min-w-max">
        {highlightedLines.map((line, index) => <DiffRow index={index} key={`${line.kind}-${index}-${line.content}`} line={line} />)}
      </div>
    </div>
  );
}

function DiffColumn({ lines, title }: { lines: HighlightedDiffLine[]; title: string }) {
  return (
    <div className="min-w-0 overflow-auto rounded-lg border border-stroke-subtle">
      <div className="border-b border-stroke-subtle px-3 py-2 text-[11.5px] font-medium text-fg-caption">{title}</div>
      <div>{lines.map((line, index) => <DiffRow index={index} key={`${title}-${line.kind}-${index}-${line.content}`} line={line} />)}</div>
    </div>
  );
}

function DiffRow({ index, line }: { index: number; line: HighlightedDiffLine }) {
  return (
    <div className={cn('grid grid-cols-[24px_1fr] gap-2 px-3 py-0.5', line.kind === 'add' && 'bg-syntax-diff-add-bg', line.kind === 'del' && 'bg-syntax-diff-del-bg')}>
      <span className={cn('select-none text-right text-fg-caption', line.kind === 'add' && 'text-syntax-diff-add-fg', line.kind === 'del' && 'text-syntax-diff-del-fg')}>
        {line.kind === 'add' ? '+' : line.kind === 'del' ? '-' : index + 1}
      </span>
      <code className="min-w-0 whitespace-pre" dangerouslySetInnerHTML={{ __html: line.html }} />
    </div>
  );
}

function buildDiffLines(before: unknown, after: unknown): DiffLine[] {
  return diffLines(toJson(before), toJson(after)).flatMap((part) => part.value.replace(/\n$/, '').split('\n').map((content) => ({ content, kind: part.added ? 'add' : part.removed ? 'del' : 'same' })));
}

function toJson(value: unknown): string {
  return `${JSON.stringify(value, null, 2)}\n`;
}

function extractCode(html: string): string {
  const match = /<code>(?<code>[\s\S]*)<\/code>/.exec(html);

  return match?.groups?.code.replace(/\n$/, '') ?? '';
}