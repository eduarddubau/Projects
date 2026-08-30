import { ComponentFixture, TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { afterEach, vi } from 'vitest';

import { ProjectTrashedPanelComponent } from './trashed-panel.component';
import { LanguageService } from '@core/services/language.service';
import { TodayService } from '@core/services/today.service';
import { Project } from '@core/models/project';
import { provideTranslocoTesting } from '@shared/testing/transloco-testing';
import { provideAppConfigTesting } from '@shared/testing/app-config-testing';

function project(deletedAt: string): Project {
  return {
    id: 'p1',
    name: 'Old thing',
    workspaceId: 'w1',
    workspaceName: 'Acme',
    createdAt: '2026-01-01T12:00:00Z',
    isDeleted: true,
    isPurgeable: false,
    deletedAt,
  } as Project;
}

describe('ProjectTrashedPanelComponent', () => {
  let fixture: ComponentFixture<ProjectTrashedPanelComponent>;
  let today: ReturnType<typeof signal<string>>;

  // Fixed so "now" sits on the same calendar day as an expiry instant, which is where a
  // day-level check and an instant-level one disagree.
  beforeEach(() => vi.setSystemTime(new Date('2026-08-29T13:00:00Z')));
  afterEach(() => vi.useRealTimers());

  function setup(deletedAt: string, canRestore = true) {
    today = signal('2026-08-29');

    TestBed.configureTestingModule({
      providers: [
        provideTranslocoTesting(),
        provideAppConfigTesting(30),
        { provide: LanguageService, useValue: { dateLocale: signal('en-US') } },
        { provide: TodayService, useValue: { today } },
      ],
    });

    fixture = TestBed.createComponent(ProjectTrashedPanelComponent);
    fixture.componentRef.setInput('project', project(deletedAt));
    fixture.componentRef.setInput('canRestore', canRestore);
    fixture.detectChanges();
    return fixture;
  }

  function restoreButton(): HTMLElement | null {
    return (fixture.nativeElement as HTMLElement).querySelector('button');
  }

  function text(): string {
    return (fixture.nativeElement as HTMLElement).textContent!;
  }

  it('dates the end of the window rather than making the owner count', () => {
    setup('2026-08-20T12:00:00Z');
    expect(text()).toContain('Sep 19, 2026');
  });

  // The window closes at the deletion's time of day, not at midnight.
  it('still offers Restore earlier on the closing day', () => {
    setup('2026-07-30T14:00:00Z');
    expect(restoreButton()).not.toBeNull();
  });

  it('withdraws Restore once that day’s hour has passed', () => {
    vi.setSystemTime(new Date('2026-08-29T15:00:00Z'));
    setup('2026-07-30T14:00:00Z');

    expect(restoreButton()).toBeNull();
    expect(text()).toContain('recovery window has closed');
  });

  // Date.now() is not reactive, so a page left open would keep the button without the day
  // signal driving a recompute.
  it('withdraws Restore from a page left open across the closing day', () => {
    setup('2026-07-30T14:00:00Z');
    expect(restoreButton()).not.toBeNull();

    vi.setSystemTime(new Date('2026-08-30T09:00:00Z'));
    today.set('2026-08-30');
    fixture.detectChanges();

    expect(restoreButton()).toBeNull();
  });

  // The server refuses a restore past the window, so an owner must not be offered one.
  it('withdraws Restore once the window has closed', () => {
    setup('2026-07-01T12:00:00Z');

    expect(restoreButton()).toBeNull();
    expect(text()).toContain('recovery window has closed');
  });

  it('still offers Restore inside the window', () => {
    setup('2026-08-20T12:00:00Z');
    expect(restoreButton()).not.toBeNull();
  });

  it('offers a member no Restore either way', () => {
    setup('2026-08-20T12:00:00Z', false);
    expect(restoreButton()).toBeNull();
  });
});
