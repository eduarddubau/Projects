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

/** Lexicographic comparison is date comparison for this format, so no Date math. */
export function isOverdue(dueDate: string | undefined, status: TaskStatus): boolean {
  return !!dueDate && status !== 'Done' && dueDate < todayIso();
}
