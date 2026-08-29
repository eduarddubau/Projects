import { ComponentFixture, TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';

import { ProjectTrashedPanelComponent } from './trashed-panel.component';
import { LanguageService } from '@core/services/language.service';
import { TodayService } from '@core/services/today.service';
import { Project } from '@core/models/project';
import { provideTranslocoTesting } from '@shared/testing/transloco-testing';
import { provideAppConfigTesting } from '@shared/testing/app-config-testing';

const today = '2026-08-29';

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

  function setup(deletedAt: string, canRestore = true) {
    TestBed.configureTestingModule({
      providers: [
        provideTranslocoTesting(),
        provideAppConfigTesting(30),
        { provide: LanguageService, useValue: { dateLocale: signal('en-US') } },
        { provide: TodayService, useValue: { today: signal(today) } },
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
