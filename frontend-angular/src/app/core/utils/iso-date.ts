import { TaskStatus } from '@core/models/task';

/**
 * Calendar dates as `yyyy-MM-dd`, matching the API's DateOnly fields.
 *
 * Both conversions exist to keep UTC out of it. A Material datepicker hands back
 * a Date at *local* midnight, and `toISOString()` on that rolls the day backwards
 * for anyone east of UTC; `new Date('2026-08-20')` parses as UTC midnight and
 * rolls forwards for anyone west of it. Neither is ever correct for a due date.
 */
const pad = (value: number) => String(value).padStart(2, '0');

export function toIsoDate(date: Date | null | undefined): string | undefined {
  if (!date) return undefined;
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;
}

export function fromIsoDate(iso: string | null | undefined): Date | null {
  if (!iso) return null;
  return new Date(+iso.slice(0, 4), +iso.slice(5, 7) - 1, +iso.slice(8, 10));
}

export function todayIso(): string {
  return toIsoDate(new Date())!;
}

/**
 * Lexicographic comparison is date comparison for this format, so no Date math.
 *
 * `today` is required, not defaulted: a default would let a caller reach the clock by
 * omission and silently reintroduce the bug this exists to prevent — a page left open past
 * midnight keeping yesterday's answer. Pass TodayService.today().
 */
export function isOverdue(dueDate: string | undefined, status: TaskStatus, today: string): boolean {
  return !!dueDate && status !== 'Done' && dueDate < today;
}

/** The groups a task list is banded into, in the order they are shown. */
export const DUE_BUCKETS = ['overdue', 'today', 'thisWeek', 'later', 'none'] as const;

export type DueBucket = (typeof DUE_BUCKETS)[number];

/** Which band a due date falls in. `today` is required for the reason isOverdue's is. */
export function dueBucket(dueDate: string | undefined, today: string): DueBucket {
  if (!dueDate) return 'none';
  if (dueDate < today) return 'overdue';
  if (dueDate === today) return 'today';

  // Six days out, not "to Sunday": a rolling window means the band never empties
  // itself late in the week, when it is the one people are actually reading.
  const horizon = fromIsoDate(today)!;
  horizon.setDate(horizon.getDate() + 6);

  return dueDate <= toIsoDate(horizon)! ? 'thisWeek' : 'later';
}
