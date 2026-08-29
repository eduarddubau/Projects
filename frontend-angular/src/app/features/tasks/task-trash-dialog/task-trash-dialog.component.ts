import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { DatePipe } from '@angular/common';
import { TranslocoDirective } from '@jsverse/transloco';
import { TaskService } from '@core/services/task.service';
import { TaskDeletionService } from '@core/services/task-deletion.service';
import { LanguageService } from '@core/services/language.service';
import { Task } from '@core/models/task';

export interface TaskTrashDialogData {
  projectId: string;
}

/**
 * Deleted tasks still inside the retention window, with a way back.
 *
 * This is what lets task deletion stay open to every member: the app gates on what cannot
 * be undone, not on the word "delete". Restoring is member access for the same reason.
 */
@Component({
  selector: 'app-task-trash-dialog',
  templateUrl: './task-trash-dialog.component.html',
  styleUrl: './task-trash-dialog.component.scss',
  imports: [
    MatDialogModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    DatePipe,
    TranslocoDirective,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TaskTrashDialogComponent {
  private data = inject<TaskTrashDialogData>(MAT_DIALOG_DATA);
  private taskService = inject(TaskService);
  private taskDeletion = inject(TaskDeletionService);
  private language = inject(LanguageService);

  dateLocale = this.language.dateLocale;

  // The dialog opens on one project and never changes it; the signal is only the shape
  // the resource factory takes.
  deleted = this.taskService.projectDeletedTasks(signal(this.data.projectId));

  /** Whether anything came back, so the caller reloads the board only when it must. */
  restoredAny = signal(false);

  /** Locks every row while one is in flight — the list reshuffles when a restore lands. */
  restoring = signal(false);

  restore(task: Task): void {
    if (this.restoring()) return;
    this.restoring.set(true);

    this.taskDeletion.restoreWithFeedback(task, this.deleted).subscribe((restored) => {
      if (restored) this.restoredAny.set(true);
      this.restoring.set(false);
    });
  }
}
