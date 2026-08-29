import { ChangeDetectionStrategy, Component, DestroyRef, inject } from '@angular/core';
import { DatePipe } from '@angular/common';
import { ActivatedRoute } from '@angular/router';
import { map } from 'rxjs/operators';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, MatPaginatorIntl } from '@angular/material/paginator';
import { MatSortModule } from '@angular/material/sort';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatIconModule } from '@angular/material/icon';
import { MatDialog } from '@angular/material/dialog';
import { TranslocoDirective } from '@jsverse/transloco';
import { TranslocoPaginatorIntl } from '@core/i18n/transloco-paginator-intl';
import { TaskService } from '@core/services/task.service';
import { TaskDeletionService } from '@core/services/task-deletion.service';
import { LanguageService } from '@core/services/language.service';
import { WorkspaceTask } from '@core/models/task';
import { TableState } from '@shared/table/table-state';
import { ClickableRowDirective } from '@shared/clickable-row/clickable-row.directive';
import { TrashExpiryComponent } from '@shared/trash-expiry/trash-expiry.component';
import { TaskDetailsDialogComponent } from '../task-details-dialog/task-details-dialog.component';

/**
 * Every deleted task in the workspace, wherever it was deleted from.
 *
 * No actions column, matching the projects trash: a row opens the task's whole record, and
 * Restore lives in there beside it. The projects trash opens a page; a task has none, so
 * this opens a dialog.
 */
@Component({
  selector: 'app-task-trash',
  templateUrl: './task-trash.component.html',
  imports: [
    ClickableRowDirective,
    TrashExpiryComponent,
    MatTableModule,
    MatPaginatorModule,
    MatSortModule,
    MatFormFieldModule,
    MatInputModule,
    MatProgressSpinnerModule,
    MatIconModule,
    DatePipe,
    TranslocoDirective,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [{ provide: MatPaginatorIntl, useClass: TranslocoPaginatorIntl }],
})
export class TaskTrashComponent {
  private taskService = inject(TaskService);
  private dialog = inject(MatDialog);
  private taskDeletion = inject(TaskDeletionService);
  private language = inject(LanguageService);
  private route = inject(ActivatedRoute);
  private destroyRef = inject(DestroyRef);

  dateLocale = this.language.dateLocale;

  workspaceId = toSignal(this.route.paramMap.pipe(map((p) => p.get('workspaceId'))), {
    initialValue: this.route.snapshot.paramMap.get('workspaceId'),
  });

  deleted = this.taskService.workspaceDeletedTasks(this.workspaceId);

  table = new TableState<WorkspaceTask>({
    source: () => (this.deleted.hasValue() ? this.deleted.value() : []),
    searchText: (task) => `${task.title} ${task.projectName}`,
    sortValue: (task, column) => {
      switch (column) {
        case 'title':
          return task.title.toLowerCase();
        case 'projectName':
          return task.projectName.toLowerCase();
        case 'deletedAt':
          return task.deletedAt ?? '';
        default:
          return '';
      }
    },
  });

  displayedColumns = ['index', 'title', 'projectName', 'status', 'deletedAt', 'expires'];

  openDetail(task: WorkspaceTask): void {
    this.dialog
      .open(TaskDetailsDialogComponent, { width: '560px', data: { task } })
      .afterClosed()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((restore: boolean | undefined) => {
        if (restore) this.taskDeletion.restoreWithFeedback(task, this.deleted).subscribe();
      });
  }
}
