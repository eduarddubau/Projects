import { ComponentFixture, TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';

import { TrashExpiryComponent } from './trash-expiry.component';
import { LanguageService } from '@core/services/language.service';
import { TodayService } from '@core/services/today.service';
import { provideTranslocoTesting } from '@shared/testing/transloco-testing';
import { provideAppConfigTesting } from '@shared/testing/app-config-testing';

describe('TrashExpiryComponent', () => {
  let fixture: ComponentFixture<TrashExpiryComponent>;
  let today: ReturnType<typeof signal<string>>;

  /** Mounts a row deleted on `deletedAt`, read on `day` under a `windowDays` policy. */
  function setup(deletedAt: string, day = '2026-08-29', windowDays: number | null = 30) {
    today = signal(day);

    TestBed.configureTestingModule({
      providers: [
        provideTranslocoTesting(),
        provideAppConfigTesting(windowDays),
        { provide: LanguageService, useValue: { dateLocale: signal('en-US') } },
        { provide: TodayService, useValue: { today } },
      ],
    });

    fixture = TestBed.createComponent(TrashExpiryComponent);
    fixture.componentRef.setInput('deletedAt', deletedAt);
    fixture.detectChanges();
    return fixture;
  }

  function text(): string {
    return (fixture.nativeElement as HTMLElement).textContent!.trim();
  }

  it('counts the days a row has left', () => {
    setup('2026-08-20T12:00:00Z');
    expect(text()).toBe('Expires in 21 days');
  });

  it('names the last day rather than counting zero', () => {
    setup('2026-07-30T12:00:00Z');
    expect(text()).toBe('Last day to restore');
  });

  it('says tomorrow instead of one day', () => {
    setup('2026-07-31T12:00:00Z');
    expect(text()).toBe('Expires tomorrow');
  });

  it('marks a row past the window as expired', () => {
    setup('2026-07-01T12:00:00Z');
    expect(text()).toBe('Expired');
  });

  it('follows the configured window rather than a built-in one', () => {
    setup('2026-08-20T12:00:00Z', '2026-08-29', 7);
    expect(text()).toBe('Expired');
  });

  it('re-counts when the reader’s day turns', () => {
    setup('2026-08-20T12:00:00Z');
    today.set('2026-09-18');
    fixture.detectChanges();

    expect(text()).toBe('Expires tomorrow');
  });

  it('flags a row close to the end so it reads as urgent', () => {
    setup('2026-08-01T12:00:00Z');
    expect(fixture.nativeElement.querySelector('.expiry-soon')).not.toBeNull();
  });

  it('leaves a row with time left unflagged', () => {
    setup('2026-08-20T12:00:00Z');
    expect(fixture.nativeElement.querySelector('.expiry-soon')).toBeNull();
  });

  // The admin trash counts up to the erase instead of down to the expiry.
  it('counts towards purgeable in the admin variant, without the urgency', () => {
    setup('2026-08-20T12:00:00Z');
    fixture.componentRef.setInput('variant', 'purgeable');
    fixture.detectChanges();

    expect(text()).toBe('Purgeable in 21 days');
    expect(fixture.nativeElement.querySelector('.expiry-soon')).toBeNull();
  });

  it('says nothing at all when the window never arrived', () => {
    setup('2026-08-20T12:00:00Z', '2026-08-29', null);
    expect(text()).toBe('');
  });

  it('says nothing for a row that was never deleted', () => {
    setup('');
    expect(text()).toBe('');
  });
});
