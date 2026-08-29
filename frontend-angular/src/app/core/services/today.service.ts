import { DestroyRef, Injectable, PLATFORM_ID, inject, signal } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { todayIso } from '@core/utils/iso-date';

/** A second past midnight, so the date has usually turned when we re-read it. */
const ROLLOVER_GRACE_MS = 1000;

/** How soon to look again when it had not. */
const ROLLOVER_RETRY_MS = 60_000;

/**
 * The reader's calendar day, kept current across midnight.
 *
 * Anything banding work by due date reads this instead of calling todayIso() itself: a
 * page left open overnight otherwise keeps yesterday's "Due today" until something
 * unrelated happens to recompute it.
 */
@Injectable({ providedIn: 'root' })
export class TodayService {
  private platformId = inject(PLATFORM_ID);

  readonly today = signal(todayIso());

  private handle: ReturnType<typeof setTimeout> | undefined;

  constructor() {
    // Nothing to roll over server-side, where the render finishes in milliseconds — and a
    // pending timer would hold it open.
    if (!isPlatformBrowser(this.platformId)) return;

    this.scheduleRollover();

    // The chain re-arms itself, so without this a torn-down injector leaves a timer writing
    // to a dead signal forever — in a test run, every later spec inherits one.
    inject(DestroyRef).onDestroy(() => clearTimeout(this.handle));
  }

  private scheduleRollover(): void {
    const now = new Date();
    const midnight = new Date(now.getFullYear(), now.getMonth(), now.getDate() + 1);

    this.handle = setTimeout(
      () => this.rollOver(),
      midnight.getTime() - now.getTime() + ROLLOVER_GRACE_MS,
    );
  }

  private rollOver(): void {
    const day = todayIso();

    // Check rather than assume: an NTP correction backwards, or a DST fall-back landing on
    // 00:00, can fire the timer while the date has not turned. Scheduling the *next*
    // midnight from here would strand the day for a full 24 hours — worse than the bug this
    // service fixes — so look again shortly instead.
    if (day === this.today()) {
      this.handle = setTimeout(() => this.rollOver(), ROLLOVER_RETRY_MS);
      return;
    }

    this.today.set(day);
    this.scheduleRollover();
  }
}
