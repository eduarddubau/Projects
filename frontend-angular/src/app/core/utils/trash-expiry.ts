import { fromIsoDate, toIsoDate } from '@core/utils/iso-date';

const MS_PER_DAY = 86_400_000;

/** The day a soft-deleted row leaves the trash, as the reader's calendar day. */
export function expiryIso(deletedAt: string, windowDays: number): string {
  const expiry = new Date(deletedAt);
  expiry.setDate(expiry.getDate() + windowDays);
  return toIsoDate(expiry)!;
}

/**
 * The instant a soft-deleted row stops being restorable.
 *
 * Exact days from the deletion, matching the server's own comparison, so a decision about
 * whether an action is still allowed agrees with the answer the API will give. The day-level
 * countdown below is for reading; this is for gating.
 */
export function expiryInstant(deletedAt: string, windowDays: number): number {
  return new Date(deletedAt).getTime() + windowDays * MS_PER_DAY;
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
