import { fromIsoDate, toIsoDate } from '@core/utils/iso-date';

const MS_PER_DAY = 86_400_000;

/** The day a soft-deleted row leaves the trash, as the reader's calendar day. */
export function expiryIso(deletedAt: string, windowDays: number): string {
  const expiry = new Date(deletedAt);
  expiry.setDate(expiry.getDate() + windowDays);
  return toIsoDate(expiry)!;
}

/**
 * Whole days from the reader's day to `expiry`, negative once it has passed.
 *
 * `today` is required for the reason isOverdue's is: a default would let a caller reach the
 * clock by omission and strand a page left open past midnight on yesterday's answer.
 */
export function daysUntilExpiry(expiry: string, today: string): number {
  const from = fromIsoDate(today)!;
  const to = fromIsoDate(expiry)!;

  // Round, not floor: both ends are local midnights, and a DST boundary between them
  // makes the difference 23 or 25 hours.
  return Math.round((to.getTime() - from.getTime()) / MS_PER_DAY);
}
