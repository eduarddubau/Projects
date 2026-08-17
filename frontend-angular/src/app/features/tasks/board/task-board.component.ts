import {
  Component,
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  DestroyRef,
  computed,
  inject,
  input,
} from '@angular/core';
import { DatePipe } from '@angular/common';
import { HttpResourceRef } from '@angular/common/http';
import { CdkDragDrop, DragDropModule } from '@angular/cdk/drag-drop';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { serverErrorKey } from '@core/i18n/server-error-keys';
import { Task, TaskStatus, TASK_STATUSES, applyMove, sortTasks } from '@core/models/task';
import { WorkspaceMember } from '@core/models/workspace';
import { LanguageService } from '@core/services/language.service';
import { TaskService } from '@core/services/task.service';
import { fromIsoDate, isOverdue } from '@core/utils/iso-date';
import { ConfirmDialogComponent } from '@shared/confirm-dialog/confirm-dialog.component';
import { TaskFormDialogComponent } from '../task-form-dialog/task-form-dialog.component';

@Component({
  selector: 'app-task-board',
  templateUrl: './task-board.component.html',
  styleUrl: './task-board.component.scss',
  imports: [
    DatePipe,
    DragDropModule,
    MatButtonModule,
    MatIconModule,
    MatMenuModule,
    MatTooltipModule,
    TranslocoDirective,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TaskBoardComponent {
  private taskService = inject(TaskService);
  private dialog = inject(MatDialog);
  private snackBar = inject(MatSnackBar);
  private cdr = inject(ChangeDetectorRef);
  private destroyRef = inject(DestroyRef);
  private transloco = inject(TranslocoService);
  private languageService = inject(LanguageService);

  /** Owned by the project page, so switching views neither refetches nor loses an edit. */
  tasks = input.required<HttpResourceRef<Task[]>>();
  members = input.required<readonly WorkspaceMember[]>();

  readonly statuses = TASK_STATUSES;
  dateLocale = computed(() => (this.languageService.lang() === 'ro' ? 'ro' : 'en-US'));

  isOverdue = isOverdue;
  asDate = fromIsoDate;

  private all = computed(() => (this.tasks().hasValue() ? this.tasks().value() : []));

  /** The server already returns tasks in column order, so this only partitions. */
  private columns = computed(() => {
    const grouped = new Map<TaskStatus, Task[]>(TASK_STATUSES.map((status) => [status, []]));
    for (const task of this.all()) grouped.get(task.status)?.push(task);
    return grouped;
  });

  column(status: TaskStatus): Task[] {
    return this.columns().get(status) ?? [];
  }

  /** Drop lists are connected by id so a card can cross columns. */
  readonly dropListIds = TASK_STATUSES.map((status) => `column-${status}`);

  drop(event: CdkDragDrop<TaskStatus>, status: TaskStatus): void {
    const moved = event.item.data as Task;

    if (event.previousContainer === event.container && event.previousIndex === event.currentIndex) {
      return;
    }

    // Excluding the moved card first makes the index arithmetic identical whether it
    // came from this column (where it is still present) or another (where it is not).
    const destination = this.column(status).filter((task) => task.id !== moved.id);

    this.move(
      moved,
      status,
      destination[event.currentIndex - 1]?.id,
      destination[event.currentIndex]?.id,
    );
  }

  // WCAG 2.2 SC 2.5.7: every drag outcome has to be reachable without dragging.
  // These three are that path, and they call the same endpoint the drop handler does.
  moveToStatus(task: Task, status: TaskStatus): void {
    if (task.status === status) return;
    this.move(task, status, undefined, undefined);
  }

  moveUp(task: Task): void {
    const column = this.column(task.status);
    const index = column.findIndex((candidate) => candidate.id === task.id);
    if (index <= 0) return;

    this.move(task, task.status, column[index - 2]?.id, column[index - 1].id);
  }

  moveDown(task: Task): void {
    const column = this.column(task.status);
    const index = column.findIndex((candidate) => candidate.id === task.id);
    if (index < 0 || index === column.length - 1) return;

    this.move(task, task.status, column[index + 1].id, column[index + 2]?.id);
  }

  isFirst(task: Task): boolean {
    return this.column(task.status)[0]?.id === task.id;
  }

  isLast(task: Task): boolean {
    const column = this.column(task.status);
    return column[column.length - 1]?.id === task.id;
  }

  private move(
    task: Task,
    status: TaskStatus,
    previousTaskId: string | undefined,
    nextTaskId: string | undefined,
  ): void {
    if (!this.tasks().hasValue()) return;

    this.tasks().update((list) => applyMove(list, task.id, status, previousTaskId, nextTaskId));

    // No takeUntilDestroyed: switching to the list view mid-flight would cancel the
    // request while the shared resource keeps the optimistic position, and neither the
    // reconcile below nor the rollback would ever run.
    this.taskService.moveTask(task.id, { status, previousTaskId, nextTaskId }).subscribe({
      next: (saved) => {
        // The optimistic guess put the card in the right slot; this replaces it with
        // the row the server actually wrote, whose position is authoritative.
        this.tasks().update((list) =>
          sortTasks(list.map((item) => (item.id === saved.id ? saved : item))),
        );
        this.cdr.markForCheck();
      },
      error: (err) => {
        // Refetch rather than restore a snapshot: a snapshot taken before the optimistic
        // apply would also erase anything created or moved while this was in flight.
        this.tasks().reload();
        this.notify(serverErrorKey(err, 'tasks.notifications.moveFailed'), 5000);
      },
    });
  }

  openEdit(task: Task): void {
    this.dialog
      .open(TaskFormDialogComponent, { width: '560px', data: { task, members: this.members } })
      .afterClosed()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((result) => {
        if (!result) return;

        if (result.action === 'delete') {
          this.confirmDelete(task);
          return;
        }

        this.taskService.updateTask(task.id, result.payload).subscribe({
          next: (saved) => {
            this.tasks().update((list) =>
              sortTasks(list.map((item) => (item.id === saved.id ? saved : item))),
            );
            this.cdr.markForCheck();
            this.notify('tasks.notifications.updated');
          },
          error: (err) =>
            this.notify(serverErrorKey(err, 'tasks.notifications.updateFailed'), 5000),
        });
      });
  }

  confirmDelete(task: Task): void {
    this.dialog
      .open(ConfirmDialogComponent, {
        width: '400px',
        data: {
          title: this.transloco.translate('tasks.confirmDelete.title'),
          message: this.transloco.translate('tasks.confirmDelete.message', { title: task.title }),
          confirmLabel: this.transloco.translate('common.actions.delete'),
          warn: true,
        },
      })
      .afterClosed()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((confirmed: boolean | undefined) => {
        if (!confirmed) return;

        this.taskService.deleteTask(task.id).subscribe({
          next: () => {
            this.tasks().update((list) => list.filter((item) => item.id !== task.id));
            this.cdr.markForCheck();
            this.notify('tasks.notifications.deleted');
          },
          error: () => this.notify('tasks.notifications.deleteFailed', 5000),
        });
      });
  }

  private notify(key: string, duration = 3000): void {
    this.snackBar.open(
      this.transloco.translate(key),
      this.transloco.translate('common.actions.close'),
      { duration },
    );
  }
}
