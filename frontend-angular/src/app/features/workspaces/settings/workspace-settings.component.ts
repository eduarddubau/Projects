import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  inject,
  input,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Router, RouterLink } from '@angular/router';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSnackBar } from '@angular/material/snack-bar';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { serverErrorKey } from '@core/i18n/server-error-keys';
import { WorkspaceService } from '@core/services/workspace.service';
import { WorkspaceContextService } from '@core/services/workspace-context.service';
import { AuroraComponent } from '@shared/aurora/aurora.component';
import {
  ConfirmDialogComponent,
  ConfirmDialogData,
} from '@shared/confirm-dialog/confirm-dialog.component';
import {
  WORKSPACE_DESCRIPTION_MAX,
  WORKSPACE_NAME_MAX,
} from '../workspace-form-dialog/workspace-form-dialog.component';

@Component({
  selector: 'app-workspace-settings',
  templateUrl: './workspace-settings.component.html',
  styleUrl: './workspace-settings.component.scss',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    AuroraComponent,
    TranslocoDirective,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class WorkspaceSettingsComponent {
  private fb = inject(FormBuilder);
  private api = inject(WorkspaceService);
  private context = inject(WorkspaceContextService);
  private dialog = inject(MatDialog);
  private snackBar = inject(MatSnackBar);
  private transloco = inject(TranslocoService);
  private router = inject(Router);
  private destroyRef = inject(DestroyRef);

  workspaceId = input.required<string>();

  workspace = this.context.currentWorkspace;
  canManage = this.context.canManageCurrent;
  isBusy = signal(false);

  nameMax = WORKSPACE_NAME_MAX;
  descriptionMax = WORKSPACE_DESCRIPTION_MAX;

  form = this.fb.nonNullable.group({
    name: [
      this.workspace()?.name ?? '',
      [Validators.required, Validators.maxLength(WORKSPACE_NAME_MAX)],
    ],
    description: [
      this.workspace()?.description ?? '',
      Validators.maxLength(WORKSPACE_DESCRIPTION_MAX),
    ],
  });

  save(): void {
    if (this.form.invalid || this.form.pristine) return;

    const { name, description } = this.form.getRawValue();
    this.isBusy.set(true);

    this.api
      .updateWorkspace(this.workspaceId(), {
        name: name.trim(),
        description: description.trim() || undefined,
      })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (updated) => {
          this.isBusy.set(false);
          // Everything else reads the store, not this form.
          this.context.upsert(updated);
          // Not reset(): that would blank the fields too.
          this.form.markAsPristine();
          this.toast('workspaces.settings.saved');
        },
        error: (err) => {
          this.isBusy.set(false);
          this.toast(serverErrorKey(err, 'workspaces.settings.saveFailed'), 5000);
        },
      });
  }

  confirmDelete(): void {
    const workspace = this.workspace();
    if (!workspace) return;

    // Captured up front: the delete navigates away and destroys this component.
    const id = this.workspaceId();

    // A plain confirm, not a typed name: typing the name is the tier for something
    // unrecoverable, and this now lands in a trash the owner can restore from.
    const data: ConfirmDialogData = {
      title: this.transloco.translate('workspaces.settings.deleteTitle'),
      message: this.transloco.translate('workspaces.settings.deleteMessage', {
        name: workspace.name,
      }),
      confirmLabel: this.transloco.translate('workspaces.settings.delete'),
      warn: true,
    };

    this.dialog
      .open(ConfirmDialogComponent, { width: '460px', data })
      .afterClosed()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((confirmed: boolean) => confirmed && this.remove(id));
  }

  private remove(id: string): void {
    this.isBusy.set(true);

    this.api
      .deleteWorkspace(id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.isBusy.set(false);
          // Before navigating, or the guard on the way out resolves against a
          // list that still holds this id.
          this.context.remove(id);
          this.router.navigate(['/workspaces']);
          this.offerUndo(id);
        },
        error: (err) => {
          this.isBusy.set(false);
          this.toast(serverErrorKey(err, 'workspaces.settings.deleteFailed'), 5000);
        },
      });
  }

  /**
   * Undo is the quick path; the trash at /workspaces/trash is the durable one. The
   * snackbar is deliberately not the only way back — a missed snackbar must not be
   * the difference between recoverable and not.
   *
   * No takeUntilDestroyed on either subscription: the delete navigates away and
   * destroys this component immediately, so tying them to its lifetime cancels the
   * undo before the user can reach it. Both are bounded anyway — the snackbar
   * completes when it dismisses, and the restore is a single response.
   */
  private offerUndo(id: string): void {
    this.snackBar
      .open(
        this.transloco.translate('workspaces.settings.deleted'),
        this.transloco.translate('common.actions.undo'),
        { duration: 8000 },
      )
      .onAction()
      .subscribe(() => this.undoDelete(id));
  }

  private undoDelete(id: string): void {
    this.api.restoreWorkspace(id).subscribe({
      next: (restored) => {
        this.context.upsert(restored);
        this.router.navigate(['/w', restored.id, 'settings']);
      },
      error: (err) => this.toast(serverErrorKey(err, 'workspaces.settings.undoFailed'), 5000),
    });
  }

  private toast(key: string, duration = 3000): void {
    this.snackBar.open(
      this.transloco.translate(key),
      this.transloco.translate('common.actions.close'),
      { duration },
    );
  }
}
