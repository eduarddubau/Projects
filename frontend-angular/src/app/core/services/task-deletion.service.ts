import { Injectable, inject } from '@angular/core';
import { HttpResourceRef } from '@angular/common/http';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Observable, ReplaySubject, defer } from 'rxjs';
import { TranslocoService } from '@jsverse/transloco';
import { Task } from '@core/models/task';
import { TaskService } from '@core/services/task.service';

/** Long enough to read and reach, inside the 4–10s Material allows an actionable snackbar. */
const UNDO_DURATION_MS = 8000;

/**
 * Deleting a task with the way back attached.
 *
 * No confirmation dialog, deliberately: this app gates on what cannot be undone, and a
 * deleted task can be. The dialog was friction in front of a reversible action while the
 * recovery affordance was missing at the moment recovery is actually wanted.
 *
 * Undo is never the only way home — "Recently deleted" holds the task for the whole
 * retention window for anyone who lets the snackbar time out.
 *
 * Shared by the board and the list because they had the same delete twice, and this app has
 * a habit of fixing one copy and shipping the other.
 */
@Injectable({ providedIn: 'root' })
export class TaskDeletionService {
  private taskService = inject(TaskService);
  private snackBar = inject(MatSnackBar);
  private transloco = inject(TranslocoService);

  /**
   * Drops the card from `tasks`, then offers Undo. Emits whenever the list changed, so an
   * OnPush caller can mark itself; completes once the snackbar is gone and nothing more
   * can happen. A restore reloads rather than splicing: the server decides where a card
   * comes back, and it is not always where it left.
   */
  deleteWithUndo(task: Task, tasks: HttpResourceRef<Task[]>): Observable<void> {
    // defer + ReplaySubject rather than a bare Subject started eagerly: the delete must not
    // fire until something subscribes, and an emission must not be lost if it ever lands
    // before the caller is attached.
    return defer(() => {
      const changed = new ReplaySubject<void>();
      this.startDelete(task, tasks, changed);
      return changed;
    });
  }

  private startDelete(
    task: Task,
    tasks: HttpResourceRef<Task[]>,
    changed: ReplaySubject<void>,
  ): void {
    this.taskService.deleteTask(task.id).subscribe({
      next: () => {
        tasks.update((list) => list.filter((t) => t.id !== task.id));
        changed.next();

        const snack = this.snackBar.open(
          this.transloco.translate('tasks.notifications.deleted'),
          this.transloco.translate('common.actions.undo'),
          { duration: UNDO_DURATION_MS },
        );

        snack.onAction().subscribe(() => {
          this.taskService.restoreTask(task.id).subscribe({
            next: () => {
              // Reload may be a no-op — the board this came from can already be gone if the
              // reader navigated inside the undo window — so the snackbar is what confirms it.
              tasks.reload();
              this.notify('tasks.trash.restored', 3000);
              changed.next();
              changed.complete();
            },
            error: () => {
              this.notify('tasks.trash.restoreFailed', 5000);
              changed.complete();
            },
          });
        });

        // Only the timeout path completes here; the action path completes on its own once
        // the restore has resolved, which is after this fires.
        snack.afterDismissed().subscribe((dismissal) => {
          if (!dismissal.dismissedByAction) changed.complete();
        });
      },
      error: () => {
        this.notify('tasks.notifications.deleteFailed', 5000);
        changed.complete();
      },
    });
  }

  /**
   * Restores a task out of a trash listing, reporting either way.
   *
   * Both trashes call this — the per-project dialog and the workspace table — because they
   * had the same twenty lines each. Emits true only when the task actually came back, so a
   * caller can tell whether it needs to reload anything behind it.
   */
  restoreWithFeedback<T extends Task>(task: T, list: HttpResourceRef<T[]>): Observable<boolean> {
    return defer(() => {
      const done = new ReplaySubject<boolean>();

      this.taskService.restoreTask(task.id).subscribe({
        next: () => {
          list.update((rows) => rows.filter((row) => row.id !== task.id));
          this.notify('tasks.trash.restored', 3000);
          done.next(true);
          done.complete();
        },
        error: () => {
          // The row stays put on purpose: dropping it would leave no way back but a reload.
          this.notify('tasks.trash.restoreFailed', 5000);
          done.next(false);
          done.complete();
        },
      });

      return done;
    });
  }

  private notify(key: string, duration: number): void {
    this.snackBar.open(
      this.transloco.translate(key),
      this.transloco.translate('common.actions.close'),
      { duration },
    );
  }
}
