import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { signal } from '@angular/core';

import { TrashShellComponent } from './trash-shell.component';
import { WorkspaceContextService } from '@core/services/workspace-context.service';
import { provideTranslocoTesting } from '@shared/testing/transloco-testing';
import { provideAppConfigTesting } from '@shared/testing/app-config-testing';

describe('TrashShellComponent', () => {
  let fixture: ComponentFixture<TrashShellComponent>;

  const contextStub = {
    isOwner: signal(true),
    currentWorkspace: signal(null),
    workspaces: signal([]),
  };

  function setup(windowDays: number | null = 30) {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideTranslocoTesting(),
        provideAppConfigTesting(windowDays),
        { provide: WorkspaceContextService, useValue: contextStub },
      ],
    });

    fixture = TestBed.createComponent(TrashShellComponent);
    fixture.detectChanges();
    return fixture;
  }

  function subtitle(): string {
    return (
      (fixture.nativeElement as HTMLElement).querySelector('.page-subtitle')?.textContent?.trim() ??
      ''
    );
  }

  it('states the window the server configured', () => {
    setup(7);
    expect(subtitle()).toBe('Deleted projects and tasks can be restored here for 7 days.');
  });

  // Nothing invents a window: the sentence is dropped rather than guessed at.
  it('says nothing about a window it could not read', () => {
    setup(null);
    expect(subtitle()).toBe('');
  });
});
