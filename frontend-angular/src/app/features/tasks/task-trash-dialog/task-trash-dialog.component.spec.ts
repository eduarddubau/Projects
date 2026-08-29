import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { signal } from '@angular/core';
import { vi } from 'vitest';

import { TaskTrashDialogComponent } from './task-trash-dialog.component';
import { API_URL } from '@core/tokens/app.tokens';
import { ThemeService } from '@core/services/theme.service';
import { Task } from '@core/models/task';
import { provideTranslocoTesting } from '@shared/testing/transloco-testing';
import { provideAppConfigTesting } from '@shared/testing/app-config-testing';

const apiUrl = 'http://api.test';
const projectId = '11111111-1111-1111-1111-111111111111';
const trashUrl = `${apiUrl}/projects/${projectId}/tasks/trash`;

// Dates render through LanguageService, which pulls in the real ThemeService — and that
// touches window.matchMedia, which jsdom lacks.
const themeStub = { provide: ThemeService, useValue: { theme: signal('light') } };

function task(id: string, overrides: Partial<Task> = {}): Task {
  return {
    id,
    title: `Task ${id}`,
    status: 'Todo',
    position: 0,
    projectId,
    createdAt: '2026-07-01T09:00:00Z',
    isDeleted: true,
    deletedAt: '2026-08-20T09:00:00Z',
    ...overrides,
  };
}

describe('TaskTrashDialogComponent', () => {
  let fixture: ComponentFixture<TaskTrashDialogComponent>;
  let httpMock: HttpTestingController;

  async function setup() {
    await TestBed.configureTestingModule({
      imports: [TaskTrashDialogComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideTranslocoTesting(),
        provideAppConfigTesting(),
        themeStub,
        { provide: API_URL, useValue: apiUrl },
        { provide: MAT_DIALOG_DATA, useValue: { projectId } },
        { provide: MatDialogRef, useValue: { close: vi.fn() } },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(TaskTrashDialogComponent);
    httpMock = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
  }

  async function flush(tasks: Task[]) {
    httpMock.expectOne(trashUrl).flush(tasks);
    await fixture.whenStable();
    fixture.detectChanges();
  }

  function rowTitles(): string[] {
    return Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('.row-title')).map(
      (el) => el.textContent?.trim() ?? '',
    );
  }

  function restoreButtons(): HTMLButtonElement[] {
    return Array.from(
      (fixture.nativeElement as HTMLElement).querySelectorAll<HTMLButtonElement>(
        '.trash-row button',
      ),
    );
  }

  afterEach(() => httpMock.verify());

  it('lists the deleted tasks the project still holds', async () => {
    await setup();
    await flush([task('a'), task('b')]);

    expect(rowTitles()).toEqual(['Task a', 'Task b']);
  });

  it('shows an empty state rather than a bare list when nothing was deleted', async () => {
    await setup();
    await flush([]);

    expect(rowTitles()).toEqual([]);
    expect((fixture.nativeElement as HTMLElement).querySelector('.state-container')).not.toBeNull();
  });

  it('restores a task and drops it out of the list', async () => {
    await setup();
    await flush([task('a'), task('b')]);

    restoreButtons()[0].click();
    httpMock.expectOne(`${apiUrl}/tasks/a/restore`).flush(task('a', { isDeleted: false }));
    await fixture.whenStable();
    fixture.detectChanges();

    expect(rowTitles()).toEqual(['Task b']);
  });

  // A failed restore must leave the row where it is, or the only way back is a reload.
  it('keeps the row when the restore fails', async () => {
    await setup();
    await flush([task('a')]);

    restoreButtons()[0].click();
    httpMock
      .expectOne(`${apiUrl}/tasks/a/restore`)
      .flush('boom', { status: 500, statusText: 'Server Error' });
    await fixture.whenStable();
    fixture.detectChanges();

    expect(rowTitles()).toEqual(['Task a']);
    expect(restoreButtons()[0].disabled).toBe(false);
  });

  // The caller reloads the board off this, so it has to stay false until one lands.
  it('reports whether anything was actually restored', async () => {
    await setup();
    await flush([task('a')]);
    expect(fixture.componentInstance.restoredAny()).toBe(false);

    restoreButtons()[0].click();
    httpMock.expectOne(`${apiUrl}/tasks/a/restore`).flush(task('a', { isDeleted: false }));
    await fixture.whenStable();

    expect(fixture.componentInstance.restoredAny()).toBe(true);
  });
});
