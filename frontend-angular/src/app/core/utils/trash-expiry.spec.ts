import { daysUntilExpiry, expiryIso } from './trash-expiry';

// Deletion instants are midday UTC so the reader's-zone conversion cannot move the day.
describe('trash-expiry', () => {
  it('lands the expiry a whole window after the deletion, in the reader’s day', () => {
    expect(expiryIso('2026-08-01T12:00:00Z', 30)).toBe('2026-08-31');
  });

  it('carries the expiry across a month end', () => {
    expect(expiryIso('2026-08-20T12:00:00Z', 30)).toBe('2026-09-19');
  });

  it('counts the days left from the day it is given', () => {
    expect(daysUntilExpiry('2026-08-31', '2026-08-29')).toBe(2);
    expect(daysUntilExpiry('2026-08-31', '2026-08-30')).toBe(1);
    expect(daysUntilExpiry('2026-08-31', '2026-08-31')).toBe(0);
  });

  it('goes negative once the window has closed', () => {
    expect(daysUntilExpiry('2026-08-31', '2026-09-03')).toBe(-3);
  });

  // A window spanning a DST change is 23 or 25 hours short of a whole number of days.
  it('counts whole days across a daylight-saving boundary', () => {
    expect(daysUntilExpiry('2026-11-05', '2026-10-29')).toBe(7);
    expect(daysUntilExpiry('2026-04-01', '2026-03-25')).toBe(7);
  });
});
