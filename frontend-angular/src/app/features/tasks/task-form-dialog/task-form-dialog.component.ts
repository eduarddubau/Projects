import { Component, ChangeDetectionStrategy, Signal, computed, inject } from '@angular/core';
import {
  AbstractControl,
  FormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
  Validators,
} from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MAT_DATE_LOCALE, provideNativeDateAdapter } from '@angular/material/core';
import { TranslocoDirective } from '@jsverse/transloco';
import { Task, TaskPayload, TASK_STATUSES } from '@core/models/task';
import { WorkspaceMember } from '@core/models/workspace';
import { LanguageService } from '@core/services/language.service';
import { fromIsoDate, toIsoDate } from '@core/utils/iso-date';

export interface TaskFormDialogData {
  task?: Task;
  /** A signal, not an array: members load after the project, so a snapshot taken
   * when the dialog opens can be empty and never fills in. */
  members: Signal<readonly WorkspaceMember[]>;
}

/** Delete closes the dialog with an intent; the caller still asks for confirmation. */
export type TaskFormResult = { action: 'save'; payload: TaskPayload } | { action: 'delete' };

/** Mirrors the server's 400 so the error shows before submitting; the server stays the authority. */
function dueOnOrAfterStart(group: AbstractControl): ValidationErrors | null {
  const start = group.get('startDate')?.value as Date | null;
  const due = group.get('dueDate')?.value as Date | null;
  return start && due && due < start ? { dueBeforeStart: true } : null;
}

@Component({
  selector: 'app-task-form-dialog',
  templateUrl: './task-form-dialog.component.html',
  styleUrl: './task-form-dialog.component.scss',
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatIconModule,
    MatDatepickerModule,
    TranslocoDirective,
  ],
  providers: [
    provideNativeDateAdapter(),
    {
      provide: MAT_DATE_LOCALE,
      useFactory: () => (inject(LanguageService).lang() === 'ro' ? 'ro-RO' : 'en-US'),
    },
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TaskFormDialogComponent {
  private fb = inject(FormBuilder);
  private dialogRef = inject(MatDialogRef<TaskFormDialogComponent, TaskFormResult>);
  data = inject<TaskFormDialogData>(MAT_DIALOG_DATA);

  readonly statuses = TASK_STATUSES;
  isEditMode = !!this.data.task;

  // An assignee who has since left the workspace is not in members, so the field shows
  // blank while the form still carries their id — saving keeps them rather than clearing.
  assignableMembers = computed(() => {
    const current = this.data.task;
    const members = this.data.members();

    if (!current?.assigneeId || members.some((m) => m.userId === current.assigneeId)) {
      return members;
    }

    return [
      ...members,
      {
        userId: current.assigneeId,
        userDisplayName: current.assigneeDisplayName ?? '',
      } as WorkspaceMember,
    ];
  });

  form = this.fb.nonNullable.group(
    {
      title: [this.data.task?.title ?? '', [Validators.required, Validators.maxLength(200)]],
      description: [this.data.task?.description ?? '', Validators.maxLength(2000)],
      status: [this.data.task?.status ?? 'Todo', Validators.required],
      assigneeId: [this.data.task?.assigneeId ?? ''],
      startDate: [fromIsoDate(this.data.task?.startDate)],
      dueDate: [fromIsoDate(this.data.task?.dueDate)],
    },
    { validators: dueOnOrAfterStart },
  );

  submit(): void {
    if (this.form.invalid) return;

    const { title, description, status, assigneeId, startDate, dueDate } = this.form.getRawValue();

    this.dialogRef.close({
      action: 'save',
      payload: {
        title,
        description: description || undefined,
        status,
        assigneeId: assigneeId || undefined,
        startDate: toIsoDate(startDate),
        dueDate: toIsoDate(dueDate),
      },
    });
  }

  requestDelete(): void {
    this.dialogRef.close({ action: 'delete' });
  }

  cancel(): void {
    this.dialogRef.close();
  }
}
