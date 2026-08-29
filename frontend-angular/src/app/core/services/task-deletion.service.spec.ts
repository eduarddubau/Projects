import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { HttpResourceRef } from '@angular/common/http';
import { MatSnackBar } from '@angular/material/snack-bar';
import { signal } from '@angular/core';
import { Subject } from 'rxjs';
import { TranslocoService } from '@jsverse/transloco';
import { vi } from 'vitest';

import { TaskDeletionService } from './task-deletion.service';
import { API_URL } from '@core/tokens/app.tokens';
import { Task } from '@core/models/task';
import { provideTranslocoTesting } from '@shared/testing/transloco-testing';

const apiUrl = 'http://api.test';

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

/**
 * Stands in for the snackbar so the undo can be driven directly. Clicking through the real
 * overlay would test Material's rendering; what matters here is what happens on the action.
 */
class SnackBarStub {
  readonly action = new Subject<void>();
  readonly dismissed = new Subject<{ dismissedByAction: boolean }>();
  readonly opened: { message: string; action?: string }[] = [];

  open(message: string, action?: string) {
    this.opened.push({ message, action });
    return {
      onAction: () => this.action.asObservable(),
      afterDismissed: () => this.dismissed.asObservable(),
    };
  }
}

/** The board and the list both hand over their resource; only these three members are used. */
function resourceStub(initial: Task[]) {
  const value = signal(initial);
  const reload = vi.fn();
  const ref = {
    value,
    hasValue: () => true,
    update: (updater: (list: Task[]) => Task[]) => value.update(updater),
    reload,
  } as unknown as HttpResourceRef<Task[]>;

  return { ref, value, reload };
}

describe('TaskDeletionService', () => {
  let service: TaskDeletionService;
  let httpMock: HttpTestingController;
  let snackBar: SnackBarStub;

  beforeEach(() => {
    snackBar = new SnackBarStub();

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideTranslocoTesting(),
        { provide: API_URL, useValue: apiUrl },
        { provide: MatSnackBar, useValue: snackBar },
      ],
    });

    service = TestBed.inject(TaskDeletionService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('deletes the task and drops it from the list', () => {
    const tasks = resourceStub([task('a'), task('b')]);
    const changed = vi.fn();

    service.deleteWithUndo(task('a'), tasks.ref).subscribe(changed);
    httpMock.expectOne(`${apiUrl}/tasks/a`).flush(null);

    expect(tasks.value().map((t) => t.id)).toEqual(['b']);
    expect(changed).toHaveBeenCalledTimes(1);
  });

  // The whole point of dropping the confirmation dialog: the way back has to be on the
  // snackbar, or the delete became strictly less recoverable than it was before.
  it('offers Undo on the snackbar rather than a confirmation up front', () => {
    const tasks = resourceStub([task('a')]);
    const undoLabel = TestBed.inject(TranslocoService).translate('common.actions.undo');

    service.deleteWithUndo(task('a'), tasks.ref).subscribe();
    httpMock.expectOne(`${apiUrl}/tasks/a`).flush(null);

    expect(snackBar.opened).toHaveLength(1);
    expect(snackBar.opened[0].action).toBe(undoLabel);
  });

  it('restores the task and reloads when Undo is taken', () => {
    const tasks = resourceStub([task('a')]);
    const changed = vi.fn();

    service.deleteWithUndo(task('a'), tasks.ref).subscribe(changed);
    httpMock.expectOne(`${apiUrl}/tasks/a`).flush(null);

    snackBar.action.next();
    httpMock.expectOne(`${apiUrl}/tasks/a/restore`).flush(task('a'));

    // Reloaded, not spliced: the server decides where a restored card lands.
    expect(tasks.reload).toHaveBeenCalledTimes(1);
    expect(changed).toHaveBeenCalledTimes(2);
  });

  // Undo can be taken after navigating away from the board it came from, where reload() is a
  // no-op on a destroyed resource. Without this the restore succeeds and says nothing, and
  // the reader goes looking in the trash for a task that is already back.
  it('confirms the restore, since the reload may have nothing left to refresh', () => {
    const tasks = resourceStub([task('a')]);

    service.deleteWithUndo(task('a'), tasks.ref).subscribe();
    httpMock.expectOne(`${apiUrl}/tasks/a`).flush(null);

    snackBar.action.next();
    httpMock.expectOne(`${apiUrl}/tasks/a/restore`).flush(task('a'));

    const restored = TestBed.inject(TranslocoService).translate('tasks.trash.restored');
    expect(snackBar.opened.map((s) => s.message)).toContain(restored);
  });

  it('leaves the task deleted when the snackbar times out', () => {
    const tasks = resourceStub([task('a')]);

    service.deleteWithUndo(task('a'), tasks.ref).subscribe();
    httpMock.expectOne(`${apiUrl}/tasks/a`).flush(null);

    snackBar.dismissed.next({ dismissedByAction: false });

    // No restore request at all — httpMock.verify() in afterEach is the assertion.
    expect(tasks.value()).toEqual([]);
    expect(tasks.reload).not.toHaveBeenCalled();
  });

  // A failed delete must not leave a hole in the board with an Undo that restores nothing.
  it('keeps the task and offers no Undo when the delete fails', () => {
    const tasks = resourceStub([task('a')]);
    const changed = vi.fn();

    service.deleteWithUndo(task('a'), tasks.ref).subscribe(changed);
    httpMock
      .expectOne(`${apiUrl}/tasks/a`)
      .flush('boom', { status: 500, statusText: 'Server Error' });

    expect(tasks.value().map((t) => t.id)).toEqual(['a']);
    expect(changed).not.toHaveBeenCalled();
    expect(snackBar.opened.every((s) => s.action !== 'Undo')).toBe(true);
  });

  // Restoring can fail too, and the card is already gone from the local list by then.
  it('reports a failed restore instead of pretending the card came back', () => {
    const tasks = resourceStub([task('a')]);

    service.deleteWithUndo(task('a'), tasks.ref).subscribe();
    httpMock.expectOne(`${apiUrl}/tasks/a`).flush(null);

    snackBar.action.next();
    httpMock
      .expectOne(`${apiUrl}/tasks/a/restore`)
      .flush('boom', { status: 500, statusText: 'Server Error' });

    expect(tasks.reload).not.toHaveBeenCalled();
    expect(snackBar.opened).toHaveLength(2);
  });

  describe('restoreWithFeedback', () => {
    it('drops the row and says so when the restore lands', () => {
      const tasks = resourceStub([task('a'), task('b')]);
      const outcome = vi.fn();

      service.restoreWithFeedback(task('a'), tasks.ref).subscribe(outcome);
      httpMock.expectOne(`${apiUrl}/tasks/a/restore`).flush(task('a'));

      expect(tasks.value().map((t) => t.id)).toEqual(['b']);
      expect(outcome).toHaveBeenCalledWith(true);
    });

    // Dropping the row on failure would leave no way back except a reload.
    it('keeps the row and reports false when it fails', () => {
      const tasks = resourceStub([task('a')]);
      const outcome = vi.fn();

      service.restoreWithFeedback(task('a'), tasks.ref).subscribe(outcome);
      httpMock
        .expectOne(`${apiUrl}/tasks/a/restore`)
        .flush('boom', { status: 500, statusText: 'Server Error' });

      expect(tasks.value().map((t) => t.id)).toEqual(['a']);
      expect(outcome).toHaveBeenCalledWith(false);
    });

    // Nothing should happen until something subscribes — the trashes rely on that to avoid
    // firing a restore for a dialog the reader closed without choosing it.
    it('sends nothing until it is subscribed to', () => {
      const tasks = resourceStub([task('a')]);

      service.restoreWithFeedback(task('a'), tasks.ref);

      // httpMock.verify() in afterEach is the assertion: no request went out.
      expect(tasks.value().map((t) => t.id)).toEqual(['a']);
    });
  });
});
