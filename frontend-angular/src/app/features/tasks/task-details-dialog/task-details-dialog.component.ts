import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { DatePipe } from '@angular/common';
import { MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { TranslocoDirective } from '@jsverse/transloco';
import { LanguageService } from '@core/services/language.service';
import { WorkspaceTask } from '@core/models/task';

export interface TaskDetailsDialogData {
  task: WorkspaceTask;
}

/**
 * The whole record of a deleted task, with one way out of the trash.
 *
 * A dialog rather than a page because a task has none — the project trash can hand the job
 * to the project's own detail view, and this is the nearest equivalent. Read-only on
 * purpose: editing something that is in the trash writes to a record nobody can see.
 *
 * Closes with true when a restore is wanted; the caller owns the request, because it also
 * owns the list the restored row has to leave.
 */
@Component({
  selector: 'app-task-details-dialog',
  templateUrl: './task-details-dialog.component.html',
  styleUrl: './task-details-dialog.component.scss',
  imports: [DatePipe, MatDialogModule, MatButtonModule, MatIconModule, TranslocoDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TaskDetailsDialogComponent {
  task = inject<TaskDetailsDialogData>(MAT_DIALOG_DATA).task;

  dateLocale = inject(LanguageService).dateLocale;
}
