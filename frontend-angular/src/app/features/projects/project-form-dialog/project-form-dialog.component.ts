import { Component, inject, ChangeDetectionStrategy } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { TranslocoDirective } from '@jsverse/transloco';
import { Project } from '@core/models/project';

export interface ProjectFormDialogData {
  project?: Project;
}

export interface ProjectFormResult {
  name: string;
  description?: string;
}

@Component({
  selector: 'app-project-form-dialog',
  templateUrl: './project-form-dialog.component.html',
  styleUrl: './project-form-dialog.component.scss',
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    TranslocoDirective
  ],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ProjectFormDialogComponent {
  private fb = inject(FormBuilder);
  private dialogRef = inject(MatDialogRef<ProjectFormDialogComponent, ProjectFormResult>);
  data = inject<ProjectFormDialogData>(MAT_DIALOG_DATA);

  isEditMode = !!this.data.project;

  form = this.fb.nonNullable.group({
    name: [this.data.project?.name ?? '', [Validators.required, Validators.maxLength(100)]],
    description: [this.data.project?.description ?? '', Validators.maxLength(500)]
  });

  submit(): void {
    if (this.form.invalid) return;

    const { name, description } = this.form.getRawValue();
    this.dialogRef.close({ name, description: description || undefined });
  }

  cancel(): void {
    this.dialogRef.close();
  }
}
