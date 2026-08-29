import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { MatDialog } from '@angular/material/dialog';
import { signal } from '@angular/core';
import { of } from 'rxjs';
import { vi } from 'vitest';

import { TaskTrashComponent } from './task-trash.component';
import { API_URL } from '@core/tokens/app.tokens';
import { ThemeService } from '@core/services/theme.service';
import { WorkspaceTask } from '@core/models/task';
import { provideTranslocoTesting } from '@shared/testing/transloco-testing';
import { provideAppConfigTesting } from '@shared/testing/app-config-testing';

const apiUrl = 'http://api.test';
const workspaceId = '22222222-2222-2222-2222-222222222222';
const trashUrl = `${apiUrl}/workspaces/${workspaceId}/tasks/trash`;

// Dates render through LanguageService, which pulls in the real ThemeService — and that
// touches window.matchMedia, which jsdom lacks.
const themeStub = { provide: ThemeService, useValue: { theme: signal('light') } };

function task(id: string, overrides: Partial<WorkspaceTask> = {}): WorkspaceTask {
  return {
    id,
    title: `Task ${id}`,
    status: 'Todo',
    position: 0,
    projectId: 'p1',
    projectName: 'Project One',
    createdAt: '2026-07-01T09:00:00Z',
    isDeleted: true,
    deletedAt: '2026-08-20T09:00:00Z',
    ...overrides,
  };
}

describe('TaskTrashComponent', () => {
  let fixture: ComponentFixture<TaskTrashComponent>;
  let httpMock: HttpTestingController;
  let dialog: { open: ReturnType<typeof vi.fn> };

  /** Opens with whatever the details dialog would have closed with. */
  function dialogCloses(result: boolean) {
    dialog.open.mockReturnValue({ afterClosed: () => of(result) });
  }

  async function setup() {
    dialog = { open: vi.fn() };
    dialogCloses(false);

    await TestBed.configureTestingModule({
      imports: [TaskTrashComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideTranslocoTesting(),
        provideAppConfigTesting(),
        themeStub,
        { provide: API_URL, useValue: apiUrl },
        { provide: MatDialog, useValue: dialog },
        {
          // Angular inherits parent params by default, so the child route carries workspaceId.
          provide: ActivatedRoute,
          useValue: {
            paramMap: of(convertToParamMap({ workspaceId })),
            snapshot: { paramMap: convertToParamMap({ workspaceId }) },
          },
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(TaskTrashComponent);
    httpMock = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
  }

  async function flush(tasks: WorkspaceTask[]) {
    httpMock.expectOne(trashUrl).flush(tasks);
    await fixture.whenStable();
    fixture.detectChanges();
  }

  function cells(column: number): string[] {
    return Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('tbody tr')).map(
      (row) => row.querySelectorAll('td')[column]?.textContent?.trim() ?? '',
    );
  }

  function rows(): HTMLElement[] {
    return Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('tbody tr'));
  }

  afterEach(() => httpMock.verify());

  it('lists every deleted task in the workspace', async () => {
    await setup();
    await flush([task('a'), task('b')]);

    expect(cells(1)).toEqual(['Task a', 'Task b']);
  });

  // The reason this trash projects a different DTO from the per-project one: a task listed
  // away from its board has to say where it came from.
  it('names the project each task came from', async () => {
    await setup();
    await flush([task('a', { projectName: 'Alpha' }), task('b', { projectName: 'Beta' })]);

    expect(cells(2)).toEqual(['Alpha', 'Beta']);
  });

  // No actions column: the row opens the record, and Restore lives in there.
  it('offers no inline actions, only the row', async () => {
    await setup();
    await flush([task('a')]);

    expect((fixture.nativeElement as HTMLElement).querySelectorAll('tbody button')).toHaveLength(0);
  });

  it('opens the task record when a row is clicked', async () => {
    await setup();
    await flush([task('a'), task('b')]);

    rows()[1].click();

    expect(dialog.open).toHaveBeenCalledTimes(1);
    expect(dialog.open.mock.calls[0][1].data.task.id).toBe('b');
  });

  it('restores and drops the row when the record asks for it', async () => {
    await setup();
    dialogCloses(true);
    await flush([task('a'), task('b')]);

    rows()[0].click();
    httpMock.expectOne(`${apiUrl}/tasks/a/restore`).flush(task('a', { isDeleted: false }));
    await fixture.whenStable();
    fixture.detectChanges();

    expect(cells(1)).toEqual(['Task b']);
  });

  // Closing without asking for a restore must not fire a request; httpMock.verify() is the
  // assertion that no stray call went out.
  it('leaves the task deleted when the record is merely closed', async () => {
    await setup();
    await flush([task('a')]);

    rows()[0].click();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(cells(1)).toEqual(['Task a']);
  });

  it('keeps the row when the restore fails', async () => {
    await setup();
    dialogCloses(true);
    await flush([task('a')]);

    rows()[0].click();
    httpMock
      .expectOne(`${apiUrl}/tasks/a/restore`)
      .flush('boom', { status: 500, statusText: 'Server Error' });
    await fixture.whenStable();
    fixture.detectChanges();

    expect(cells(1)).toEqual(['Task a']);
  });

  it('searches across the title and the project name', async () => {
    await setup();
    await flush([
      task('a', { title: 'Fix login', projectName: 'Alpha' }),
      task('b', { title: 'Write docs', projectName: 'Beta' }),
    ]);

    fixture.componentInstance.table.setSearch('beta');
    fixture.detectChanges();

    expect(cells(1)).toEqual(['Write docs']);
  });
});
