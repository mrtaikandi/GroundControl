import { describe, expect, it } from 'vitest';
import { formatRelativeTime } from './relative-time';

describe('formatRelativeTime', () => {
  const now = new Date('2026-05-09T12:00:00Z');

  it('returns "just now" for sub-minute differences', () => {
    expect(formatRelativeTime('2026-05-09T11:59:30Z', now)).toBe('just now');
  });

  it('formats minutes', () => {
    expect(formatRelativeTime('2026-05-09T11:55:00Z', now)).toMatch(/min/);
  });

  it('formats hours', () => {
    expect(formatRelativeTime('2026-05-09T07:00:00Z', now)).toMatch(/hr|hours?/i);
  });

  it('formats days as yesterday for -1', () => {
    expect(formatRelativeTime('2026-05-08T12:00:00Z', now)).toMatch(/yesterday|day/i);
  });

  it('formats older entries as months', () => {
    expect(formatRelativeTime('2026-02-09T12:00:00Z', now)).toMatch(/mo|month/i);
  });

  it('accepts a Date input', () => {
    const target = new Date('2026-05-09T11:00:00Z');
    expect(formatRelativeTime(target, now)).toMatch(/hr|hour/i);
  });
});