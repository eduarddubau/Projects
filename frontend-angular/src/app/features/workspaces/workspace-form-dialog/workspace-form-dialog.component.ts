import { Component, inject, ChangeDetectionStrategy } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { TranslocoDirective } from '@jsverse/transloco';

// Mirrors Workspace.NameMaxLength / DescriptionMaxLength on the API, which back
// both the columns and the server-side validator.
export const WORKSPACE_NAME_MAX = 60;
export const WORKSPACE_DESCRIPTION_MAX = 500;

export interface WorkspaceFormResult {
  name: string;
  description?: string;
}

@Component({
  selector: 'app-workspace-form-dialog',
  templateUrl: './workspace-form-dialog.component.html',
  styleUrl: './workspace-form-dialog.component.scss',
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    TranslocoDirective,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class WorkspaceFormDialogComponent {
  private fb = inject(FormBuilder);
  private dialogRef = inject(MatDialogRef<WorkspaceFormDialogComponent, WorkspaceFormResult>);

  nameMax = WORKSPACE_NAME_MAX;
  descriptionMax = WORKSPACE_DESCRIPTION_MAX;

  // Create only; renaming is an inline form on /w/:workspaceId/settings.
  form = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(WORKSPACE_NAME_MAX)]],
    description: ['', Validators.maxLength(WORKSPACE_DESCRIPTION_MAX)],
  });

  submit(): void {
    if (this.form.invalid) return;
    const { name, description } = this.form.getRawValue();
    this.dialogRef.close({ name: name.trim(), description: description.trim() || undefined });
  }

  cancel(): void {
    this.dialogRef.close();
  }
}
