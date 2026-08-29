import { TestBed } from '@angular/core/testing';
import { PLATFORM_ID } from '@angular/core';
import { afterEach, beforeEach, vi } from 'vitest';

import { TodayService } from './today.service';

describe('TodayService', () => {
  beforeEach(() => vi.useFakeTimers());
  afterEach(() => vi.useRealTimers());

  function setup(platform: 'browser' | 'server' = 'browser'): TodayService {
    TestBed.configureTestingModule({ providers: [{ provide: PLATFORM_ID, useValue: platform }] });
    return TestBed.inject(TodayService);
  }

  it('starts on the local calendar day', () => {
    vi.setSystemTime(new Date(2026, 7, 28, 14, 0, 0));

    expect(setup().today()).toBe('2026-08-28');
  });

  // The point of the service: a page left open overnight must not keep yesterday's
  // "Due today" band until something unrelated happens to recompute it.
  it('rolls over at midnight', () => {
    vi.setSystemTime(new Date(2026, 7, 28, 23, 59, 0));
    const service = setup();
    expect(service.today()).toBe('2026-08-28');

    vi.advanceTimersByTime(2 * 60 * 1000);

    expect(service.today()).toBe('2026-08-29');
  });

  it('keeps rolling over on later days', () => {
    vi.setSystemTime(new Date(2026, 7, 28, 23, 59, 0));
    const service = setup();

    vi.advanceTimersByTime(2 * 60 * 1000);
    vi.advanceTimersByTime(24 * 60 * 60 * 1000);

    expect(service.today()).toBe('2026-08-30');
  });

  // A month boundary is where a naive "+1 day" on the date number breaks.
  it('crosses a month boundary', () => {
    vi.setSystemTime(new Date(2026, 7, 31, 23, 59, 0));
    const service = setup();

    vi.advanceTimersByTime(2 * 60 * 1000);

    expect(service.today()).toBe('2026-09-01');
  });

  // Nothing to roll over during a server render, and a pending timer would hold it open.
  it('schedules nothing on the server', () => {
    vi.setSystemTime(new Date(2026, 7, 28, 23, 59, 0));
    setup('server');

    expect(vi.getTimerCount()).toBe(0);
  });

  // A backwards clock correction, or a DST fall-back onto 00:00, can fire the timer while
  // the date has not turned. Re-arming for the next midnight there would strand the day for
  // a full 24 hours — worse than the bug this service fixes.
  it('looks again shortly when the clock fires early', () => {
    vi.setSystemTime(new Date(2026, 7, 28, 23, 59, 0));
    const service = setup();

    // Drag the clock back after the timer is scheduled. advanceTimersByTime drives the timer
    // queue, which vitest keeps separate from the system time — so the timer fires while
    // Date still reads the 28th, which is what a backwards NTP correction looks like.
    vi.setSystemTime(new Date(2026, 7, 28, 23, 54, 0));
    vi.advanceTimersByTime(61 * 1000);
    expect(service.today()).toBe('2026-08-28');

    // The retry, not a 24-hour wait, is what picks the new day up.
    vi.setSystemTime(new Date(2026, 7, 29, 0, 1, 0));
    vi.advanceTimersByTime(61 * 1000);

    expect(service.today()).toBe('2026-08-29');
  });
});
