import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  inject,
  input,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Router } from '@angular/router';
import { DatePipe } from '@angular/common';
import { Observable } from 'rxjs';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTableModule } from '@angular/material/table';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { serverErrorKey } from '@core/i18n/server-error-keys';
import { AuthService } from '@core/services/auth.service';
import { InvitationService } from '@core/services/invitation.service';
import { WorkspaceService } from '@core/services/workspace.service';
import { WorkspaceContextService } from '@core/services/workspace-context.service';
import { WorkspaceRole } from '@core/models/workspace';
import { AuroraComponent } from '@shared/aurora/aurora.component';
import { WorkspaceScopeComponent } from '@shared/workspace-scope/workspace-scope.component';
import { ConfirmDialogComponent } from '@shared/confirm-dialog/confirm-dialog.component';
import {
  InviteDialogComponent,
  InviteDialogData,
  InviteDialogResult,
} from '../invite-dialog/invite-dialog.component';

@Component({
  selector: 'app-workspace-members',
  templateUrl: './members.component.html',
  styleUrl: './members.component.scss',
  imports: [
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    MatMenuModule,
    MatProgressSpinnerModule,
    DatePipe,
    AuroraComponent,
    WorkspaceScopeComponent,
    TranslocoDirective,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MembersComponent {
  private api = inject(WorkspaceService);
  private invitations = inject(InvitationService);
  private context = inject(WorkspaceContextService);
  private auth = inject(AuthService);
  private dialog = inject(MatDialog);
  private snackBar = inject(MatSnackBar);
  private transloco = inject(TranslocoService);
  private router = inject(Router);
  private destroyRef = inject(DestroyRef);

  // Bound from the route by withComponentInputBinding. Pass the SIGNAL to the
  // resource below, never workspaceId() — field initialisers run at construction,
  // before the router assigns inputs, and input.required throws when read early.
  workspaceId = input.required<string>();

  members = this.api.membersResource(this.workspaceId);

  // The guard called setCurrent for this id, so "current" and "this page" agree.
  workspace = this.context.currentWorkspace;
  isOwner = this.context.isOwner;
  isBusy = signal(false);

  // Personal workspaces 409 on invite and the pending list is owner-only, so
  // this gates the resource itself — not just the markup. httpResource fetches
  // from an effect whether or not anything renders value().
  canInvite = this.context.canManageCurrent;
  pending = this.invitations.pendingResource(this.workspaceId, this.canInvite);

  columns = ['name', 'role', 'joined', 'actions'];
  pendingColumns = ['email', 'role', 'expires', 'actions'];

  isMe(userId: string): boolean {
    return userId === this.auth.currentUser()?.id;
  }

  changeRole(userId: string, role: WorkspaceRole): void {
    this.mutate(
      this.api.changeMemberRole(this.workspaceId(), userId, role),
      'workspaces.members.roleChanged',
    );
  }

  remove(userId: string, name: string): void {
    this.confirm('remove', name, () =>
      this.mutate(this.api.removeMember(this.workspaceId(), userId), 'workspaces.members.removed'),
    );
  }

  openInviteDialog(): void {
    this.dialog
      .open<InviteDialogComponent, InviteDialogData, InviteDialogResult>(InviteDialogComponent, {
        width: '480px',
        data: { workspaceId: this.workspaceId() },
      })
      .afterClosed()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((result) => {
        if (!result) return;
        // A known address joins outright, so only that branch stales the member
        // table; both branches change the pending list.
        if (result === 'joined') this.members.reload();
        this.pending.reload();
      });
  }

  // No confirmation: re-inviting undoes it, and the products that do this well
  // (Slack, Notion) do not confirm either.
  revoke(invitationId: string): void {
    this.mutate(
      this.invitations.revoke(this.workspaceId(), invitationId),
      'invitations.pending.revoked',
      () => this.pending.reload(),
    );
  }

  daysLeft(expiresAt: string): number {
    return Math.max(0, Math.ceil((new Date(expiresAt).getTime() - Date.now()) / 86_400_000));
  }

  leave(): void {
    // Captured up front: navigating away destroys this component, and the input
    // signal goes with it.
    const id = this.workspaceId();
    const name = this.workspace()?.name ?? '';

    this.confirm('leave', name, () =>
      this.mutate(this.api.leaveWorkspace(id), 'workspaces.members.left', () => {
        // The store repairs its own invariant; navigating is ours to do.
        this.context.remove(id);
        this.router.navigate(['/workspaces']);
      }),
    );
  }

  private confirm(action: 'remove' | 'leave', name: string, run: () => void): void {
    this.dialog
      .open(ConfirmDialogComponent, {
        width: '420px',
        data: {
          title: this.transloco.translate(`workspaces.members.${action}Title`),
          message: this.transloco.translate(`workspaces.members.${action}Message`, { name }),
          warn: true,
        },
      })
      .afterClosed()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((confirmed: boolean) => confirmed && run());
  }

  /**
   * The only place that raises the busy flag, lowers it and maps the error. A
   * hand-rolled copy per action is how a page ends up permanently disabled by
   * the one error branch nobody exercised.
   *
   * `onSuccess` replaces the default reload rather than adding to it — leaving
   * navigates away, and refetching the members of a workspace you just left
   * would 403.
   *
   * `successKey` is a full dictionary path, not a `workspaces.members.*` suffix:
   * this now serves invitation actions too.
   */
  private mutate(request: Observable<unknown>, successKey: string, onSuccess?: () => void): void {
    this.isBusy.set(true);
    request.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        this.isBusy.set(false);
        // Server truth rather than an optimistic edit: the API owns the
        // last-owner rule, so a local guess could show a state it rejected.
        if (onSuccess) onSuccess();
        else this.members.reload();
        this.toast(successKey);
      },
      error: (err) => {
        this.isBusy.set(false);
        this.toast(serverErrorKey(err, 'workspaces.members.actionFailed'), 5000);
      },
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
