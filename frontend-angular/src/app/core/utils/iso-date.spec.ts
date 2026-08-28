import { fromIsoDate, dueBucket, isOverdue, toIsoDate, todayIso } from './iso-date';

describe('iso-date', () => {
  it('formats a local Date without shifting the day', () => {
    // Local midnight on the 1st: toISOString() would report July 31 east of UTC.
    expect(toIsoDate(new Date(2026, 7, 1))).toBe('2026-08-01');
  });

  it('formats the last day of a month', () => {
    expect(toIsoDate(new Date(2026, 11, 31))).toBe('2026-12-31');
  });

  it('parses to local midnight rather than UTC', () => {
    const parsed = fromIsoDate('2026-08-20')!;
    expect(parsed.getFullYear()).toBe(2026);
    expect(parsed.getMonth()).toBe(7);
    expect(parsed.getDate()).toBe(20);
    expect(parsed.getHours()).toBe(0);
  });

  it('round-trips every day of a month in the local zone', () => {
    for (let day = 1; day <= 31; day++) {
      const iso = `2026-01-${String(day).padStart(2, '0')}`;
      expect(toIsoDate(fromIsoDate(iso))).toBe(iso);
    }
  });

  it('treats null and undefined as absent', () => {
    expect(toIsoDate(null)).toBeUndefined();
    expect(toIsoDate(undefined)).toBeUndefined();
    expect(fromIsoDate(null)).toBeNull();
    expect(fromIsoDate('')).toBeNull();
  });

  describe('isOverdue', () => {
    const yesterday = toIsoDate(new Date(Date.now() - 86_400_000))!;
    const tomorrow = toIsoDate(new Date(Date.now() + 86_400_000))!;

    it('flags a past due date on an unfinished task', () => {
      expect(isOverdue(yesterday, 'Todo')).toBe(true);
      expect(isOverdue(yesterday, 'InProgress')).toBe(true);
    });

    it('never flags a completed task', () => {
      expect(isOverdue(yesterday, 'Done')).toBe(false);
    });

    it('does not flag today or the future', () => {
      expect(isOverdue(todayIso(), 'Todo')).toBe(false);
      expect(isOverdue(tomorrow, 'Todo')).toBe(false);
    });

    it('does not flag a task with no due date', () => {
      expect(isOverdue(undefined, 'Todo')).toBe(false);
    });
  });
  describe('dueBucket', () => {
    const today = '2026-08-20';

    it('bands a date against the given day rather than the clock', () => {
      expect(dueBucket('2026-08-19', today)).toBe('overdue');
      expect(dueBucket('2026-08-20', today)).toBe('today');
      expect(dueBucket('2026-08-23', today)).toBe('thisWeek');
      expect(dueBucket('2026-09-01', today)).toBe('later');
    });

    it('puts an undated task in its own band, never in overdue', () => {
      expect(dueBucket(undefined, today)).toBe('none');
    });

    // Six days out, inclusive — the boundary is the part that silently drifts.
    it('ends the week band on the sixth day', () => {
      expect(dueBucket('2026-08-26', today)).toBe('thisWeek');
      expect(dueBucket('2026-08-27', today)).toBe('later');
    });

    // The window is a day count, so it has to survive a month rolling over.
    it('crosses a month boundary', () => {
      expect(dueBucket('2026-09-02', '2026-08-30')).toBe('thisWeek');
    });
  });
});
