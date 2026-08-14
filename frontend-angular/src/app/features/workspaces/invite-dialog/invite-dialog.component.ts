import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBar } from '@angular/material/snack-bar';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { serverErrorKey } from '@core/i18n/server-error-keys';
import { InvitationService } from '@core/services/invitation.service';
import { WorkspaceRole } from '@core/models/workspace';
import { copyText } from '@core/utils/clipboard';

export interface InviteDialogData {
  workspaceId: string;
}

/** 'joined' means they were added outright, so the member list is now stale. */
export type InviteDialogResult = 'joined' | 'invited' | undefined;

@Component({
  selector: 'app-invite-dialog',
  templateUrl: './invite-dialog.component.html',
  styleUrl: './invite-dialog.component.scss',
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatIconModule,
    TranslocoDirective,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class InviteDialogComponent {
  private fb = inject(FormBuilder);
  private api = inject(InvitationService);
  private dialogRef = inject(MatDialogRef<InviteDialogComponent, InviteDialogResult>);
  private snackBar = inject(MatSnackBar);
  private transloco = inject(TranslocoService);
  private destroyRef = inject(DestroyRef);
  private data = inject<InviteDialogData>(MAT_DIALOG_DATA);

  form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    role: ['Member' as WorkspaceRole, Validators.required],
  });

  isSending = signal(false);
  errorKey = signal<string | null>(null);

  // Set once, never refetchable: InvitationResponseDto carries no token because
  // the server keeps only its hash. Closing without copying costs a revoke and
  // a re-invite, which is why submit() locks the dialog before it sends.
  link = signal<string | null>(null);

  /**
   * Validators.email is anchored, so a pasted "  a@b.com  " fails it and the
   * user is told their perfectly good address is invalid. Trimming on blur fixes
   * the value before validation runs rather than explaining the problem to them.
   */
  trimEmail(): void {
    const control = this.form.controls.email;
    const trimmed = control.value.trim();
    if (trimmed !== control.value) control.setValue(trimmed);
  }

  submit(): void {
    if (this.form.invalid || this.isSending()) return;
    const { email, role } = this.form.getRawValue();

    this.isSending.set(true);
    this.errorKey.set(null);
    this.dialogRef.disableClose = true;

    this.api
      .invite(this.data.workspaceId, email.trim(), role)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (result) => {
          this.isSending.set(false);
          // The discriminated union earns its keep: `token` is typed null on the
          // Joined branch, so these cannot be collapsed into one path.
          if (result.outcome === 'Joined') {
            this.dialogRef.close('joined');
            return;
          }
          this.link.set(`${window.location.origin}/invitations/accept?token=${result.token}`);
        },
        error: (err) => {
          this.isSending.set(false);
          // Nothing was issued, so the dialog is dismissable again.
          this.dialogRef.disableClose = false;
          this.errorKey.set(serverErrorKey(err, 'invitations.invite.failed'));
        },
      });
  }

  async copy(): Promise<void> {
    const copied = await copyText(this.link() ?? '');
    this.snackBar.open(
      this.transloco.translate(
        copied ? 'invitations.invite.copied' : 'invitations.invite.copyFailed',
      ),
      this.transloco.translate('common.actions.close'),
      { duration: copied ? 3000 : 6000 },
    );
  }

  done(): void {
    this.dialogRef.close('invited');
  }

  cancel(): void {
    this.dialogRef.close();
  }
}
