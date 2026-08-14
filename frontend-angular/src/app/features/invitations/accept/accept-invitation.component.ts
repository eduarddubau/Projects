import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { TranslocoDirective } from '@jsverse/transloco';
import { serverErrorKey } from '@core/i18n/server-error-keys';
import { InvitationService } from '@core/services/invitation.service';
import { WorkspaceContextService } from '@core/services/workspace-context.service';
import { Workspace } from '@core/models/workspace';
import { AuroraComponent } from '@shared/aurora/aurora.component';

@Component({
  selector: 'app-accept-invitation',
  templateUrl: './accept-invitation.component.html',
  imports: [
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    RouterLink,
    AuroraComponent,
    TranslocoDirective,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AcceptInvitationComponent {
  private route = inject(ActivatedRoute);
  private api = inject(InvitationService);
  private context = inject(WorkspaceContextService);
  private destroyRef = inject(DestroyRef);

  workspace = signal<Workspace | null>(null);
  errorKey = signal<string | null>(null);
  isBusy = signal(true);

  constructor() {
    const token = this.route.snapshot.queryParamMap.get('token');

    if (!token) {
      this.isBusy.set(false);
      this.errorKey.set('invitations.accept.missingToken');
      return;
    }

    // Accepted on arrival rather than behind a confirm button
    this.api
      .accept(token)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (workspace) => {
          this.isBusy.set(false);
          this.workspace.set(workspace);
          // Registration auto-redeems by email, so the caller may already be a
          // member; accept is idempotent and returns the workspace either way.
          this.context.upsert(workspace);
          this.context.setCurrent(workspace.id);
        },
        error: (err) => {
          this.isBusy.set(false);
          this.errorKey.set(serverErrorKey(err, 'invitations.accept.failed'));
        },
      });
  }
}
