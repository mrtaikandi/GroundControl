const MinuteSeconds = 60;
const HourSeconds = 60 * MinuteSeconds;
const DaySeconds = 24 * HourSeconds;
const WeekSeconds = 7 * DaySeconds;
const MonthSeconds = 30 * DaySeconds;
const YearSeconds = 365 * DaySeconds;

const formatter = new Intl.RelativeTimeFormat(undefined, { numeric: 'auto', style: 'short' });

export function formatRelativeTime(value: string | Date, now: Date = new Date()): string {
  const target = typeof value === 'string' ? new Date(value) : value;
  const diffSeconds = Math.round((target.getTime() - now.getTime()) / 1000);
  const absSeconds = Math.abs(diffSeconds);

  if (absSeconds < MinuteSeconds) {
    return 'just now';
  }

  if (absSeconds < HourSeconds) {
    return formatter.format(Math.round(diffSeconds / MinuteSeconds), 'minute');
  }

  if (absSeconds < DaySeconds) {
    return formatter.format(Math.round(diffSeconds / HourSeconds), 'hour');
  }

  if (absSeconds < WeekSeconds) {
    return formatter.format(Math.round(diffSeconds / DaySeconds), 'day');
  }

  if (absSeconds < MonthSeconds) {
    return formatter.format(Math.round(diffSeconds / WeekSeconds), 'week');
  }

  if (absSeconds < YearSeconds) {
    return formatter.format(Math.round(diffSeconds / MonthSeconds), 'month');
  }

  return formatter.format(Math.round(diffSeconds / YearSeconds), 'year');
}