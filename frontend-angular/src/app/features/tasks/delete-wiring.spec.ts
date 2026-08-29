import { TestBed, ComponentFixture } from '@angular/core/testing';
import { provideHttpClient, HttpResourceRef } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { MatDialog } from '@angular/material/dialog';
import { Type, signal } from '@angular/core';
import { of } from 'rxjs';
import { vi } from 'vitest';

import { TaskBoardComponent } from './board/task-board.component';
import { TaskListComponent } from './task-list/task-list.component';
import { TaskDeletionService } from '@core/services/task-deletion.service';
import { ThemeService } from '@core/services/theme.service';
import { API_URL } from '@core/tokens/app.tokens';
import { Task } from '@core/models/task';
import { provideTranslocoTesting } from '@shared/testing/transloco-testing';

/**
 * The board and the list wire delete separately, and this app's recurring bug is fixing one
 * copy and shipping the other. Both are asserted here, together, for that reason.
 *
 * What is being pinned is the trade the app made: the confirmation dialog went away *because*
 * an Undo replaced it. A regression that restores the dialog is tolerable; one that drops the
 * dialog without the Undo leaves a delete with nothing in front of it and nothing after it.
 */
const components: [string, Type<{ deleteTask(task: Task): void }>][] = [
  ['TaskBoardComponent', TaskBoardComponent],
  ['TaskListComponent', TaskListComponent],
];

const themeStub = { provide: ThemeService, useValue: { theme: signal('light') } };

function task(id: string): Task {
  return {
    id,
    title: `Task ${id}`,
    status: 'Todo',
    position: 0,
    projectId: 'p1',
    createdAt: '2026-07-01T09:00:00Z',
    isDeleted: false,
  };
}

/** Enough of the resource for both templates to render a resolved, non-empty list. */
function resourceStub(initial: Task[]) {
  const value = signal(initial);
  return {
    value,
    hasValue: () => true,
    isLoading: () => false,
    error: () => undefined,
    status: () => 'resolved',
    update: (updater: (list: Task[]) => Task[]) => value.update(updater),
    reload: vi.fn(),
  } as unknown as HttpResourceRef<Task[]>;
}

describe.each(components)('%s delete wiring', (_name, component) => {
  let fixture: ComponentFixture<{ deleteTask(task: Task): void }>;
  let deletion: { deleteWithUndo: ReturnType<typeof vi.fn> };
  let dialog: { open: ReturnType<typeof vi.fn> };

  beforeEach(async () => {
    deletion = { deleteWithUndo: vi.fn().mockReturnValue(of(undefined)) };
    dialog = { open: vi.fn() };

    await TestBed.configureTestingModule({
      imports: [component],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideTranslocoTesting(),
        themeStub,
        { provide: API_URL, useValue: 'http://api.test' },
        { provide: TaskDeletionService, useValue: deletion },
        { provide: MatDialog, useValue: dialog },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(component);
    fixture.componentRef.setInput('tasks', resourceStub([task('a')]));
    fixture.componentRef.setInput('members', []);
    fixture.detectChanges();
  });

  it('hands the delete to the shared undo path', () => {
    const target = task('a');
    fixture.componentInstance.deleteTask(target);

    expect(deletion.deleteWithUndo).toHaveBeenCalledTimes(1);
    expect(deletion.deleteWithUndo.mock.calls[0][0]).toBe(target);
  });

  it('asks for no confirmation, because the Undo is what replaced it', () => {
    fixture.componentInstance.deleteTask(task('a'));

    expect(dialog.open).not.toHaveBeenCalled();
  });
});
