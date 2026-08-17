import {
  Component,
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  DestroyRef,
  computed,
  inject,
  input,
  signal,
} from '@angular/core';
import { DatePipe } from '@angular/common';
import { HttpResourceRef } from '@angular/common/http';
import { MatTableModule } from '@angular/material/table';
import { MatSortModule, Sort } from '@angular/material/sort';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { serverErrorKey } from '@core/i18n/server-error-keys';
import { Task, TASK_STATUSES, sortTasks } from '@core/models/task';
import { WorkspaceMember } from '@core/models/workspace';
import { AuthService } from '@core/services/auth.service';
import { LanguageService } from '@core/services/language.service';
import { TaskService } from '@core/services/task.service';
import { fromIsoDate, isOverdue } from '@core/utils/iso-date';
import { TableState } from '@shared/table/table-state';
import { ConfirmDialogComponent } from '@shared/confirm-dialog/confirm-dialog.component';
import { TaskFormDialogComponent } from '../task-form-dialog/task-form-dialog.component';

@Component({
  selector: 'app-task-list',
  templateUrl: './task-list.component.html',
  styleUrl: './task-list.component.scss',
  imports: [
    DatePipe,
    MatTableModule,
    MatSortModule,
    MatPaginatorModule,
    MatButtonModule,
    MatIconModule,
    MatFormFieldModule,
    MatInputModule,
    TranslocoDirective,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TaskListComponent {
  private taskService = inject(TaskService);
  private dialog = inject(MatDialog);
  private snackBar = inject(MatSnackBar);
  private cdr = inject(ChangeDetectorRef);
  private destroyRef = inject(DestroyRef);
  private transloco = inject(TranslocoService);
  private auth = inject(AuthService);
  private languageService = inject(LanguageService);

  /** Owned by the project page so both views share one fetch. */
  tasks = input.required<HttpResourceRef<Task[]>>();
  // InputSignal is itself a Signal, so this hands the dialog a live view rather
  // than a snapshot taken when it opened.
  members = input.required<readonly WorkspaceMember[]>();

  dateLocale = computed(() => (this.languageService.lang() === 'ro' ? 'ro' : 'en-US'));

  readonly displayedColumns = ['title', 'assignee', 'status', 'dueDate'];

  /** Which quick filters are on. Client-side: the payload is one project's tasks. */
  mineOnly = signal(false);
  overdueOnly = signal(false);

  table = new TableState<Task>({
    source: () => (this.tasks().hasValue() ? this.tasks().value() : []),
    searchText: (task) => `${task.title} ${task.assigneeDisplayName ?? ''}`,
    sortValue: (task, column) => {
      switch (column) {
        case 'title':
          return task.title.toLowerCase();
        case 'assignee':
          return (task.assigneeDisplayName ?? '').toLowerCase();
        case 'status':
          // The index, not the string: sorting the values alphabetically gives
          // Done, InProgress, Todo, which is not the order the workflow runs in.
          return TASK_STATUSES.indexOf(task.status);
        case 'dueDate':
          // Undated tasks sort last ascending rather than first.
          return task.dueDate ?? '9999-12-31';
        default:
          return '';
      }
    },
  });

  isOverdue = isOverdue;
  asDate = fromIsoDate;

  toggleMine(): void {
    this.mineOnly.update((on) => !on);
    this.applyFilters();
  }

  toggleOverdue(): void {
    this.overdueOnly.update((on) => !on);
    this.applyFilters();
  }

  private applyFilters(): void {
    const mine = this.mineOnly();
    const overdue = this.overdueOnly();
    const userId = this.auth.currentUser()?.id;

    if (!mine && !overdue) {
      this.table.setFilter(null);
      return;
    }

    this.table.setFilter(
      (task) =>
        (!mine || task.assigneeId === userId) && (!overdue || isOverdue(task.dueDate, task.status)),
    );
  }

  // Creating is a page-level action and lives in the shell's view bar. The row opens
  // this, and deleting is one of the things the editor can close with.
  openEdit(task: Task): void {
    this.dialog
      .open(TaskFormDialogComponent, {
        width: '560px',
        data: { task, members: this.members },
      })
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
            // Re-sort: an edit can change status, which moves the row to another group.
            this.tasks().update((list) =>
              sortTasks(list.map((t) => (t.id === saved.id ? saved : t))),
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
            this.tasks().update((list) => list.filter((t) => t.id !== task.id));
            this.cdr.markForCheck();
            this.notify('tasks.notifications.deleted');
          },
          error: () => this.notify('tasks.notifications.deleteFailed', 5000),
        });
      });
  }

  onSort(sort: Sort): void {
    this.table.setSort(sort);
  }

  onPage(event: PageEvent): void {
    this.table.setPage(event);
  }

  private notify(key: string, duration = 3000): void {
    this.snackBar.open(
      this.transloco.translate(key),
      this.transloco.translate('common.actions.close'),
      { duration },
    );
  }
}
