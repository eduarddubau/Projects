import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar } from '@angular/material/snack-bar';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { WorkspaceService } from '@core/services/workspace.service';
import { WorkspaceContextService } from '@core/services/workspace-context.service';
import { Workspace } from '@core/models/workspace';
import { AuroraComponent } from '@shared/aurora/aurora.component';
import { serverErrorKey } from '@core/i18n/server-error-keys';

/**
 * The owner's own deleted workspaces. Only workspaces they own appear — the API
 * filters by ownership, so a member of a workspace someone else deleted sees
 * nothing here and cannot bring it back.
 */
@Component({
  selector: 'app-workspace-trash',
  templateUrl: './workspace-trash.component.html',
  imports: [
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    DatePipe,
    RouterLink,
    AuroraComponent,
    TranslocoDirective,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class WorkspaceTrashComponent {
  private api = inject(WorkspaceService);
  private context = inject(WorkspaceContextService);
  private snackBar = inject(MatSnackBar);
  private transloco = inject(TranslocoService);
  private destroyRef = inject(DestroyRef);

  deleted = signal<Workspace[]>([]);
  isLoading = signal(true);
  hasError = signal(false);

  constructor() {
    this.api
      .getMyDeletedWorkspaces()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (list) => {
          this.deleted.set(list);
          this.isLoading.set(false);
        },
        error: () => {
          this.hasError.set(true);
          this.isLoading.set(false);
        },
      });
  }

  restore(workspace: Workspace): void {
    this.api
      .restoreWorkspace(workspace.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (restored) => {
          this.deleted.update((list) => list.filter((w) => w.id !== workspace.id));
          // Back into the switcher without a refetch, so it reappears immediately.
          this.context.upsert(restored);
          this.toast(
            this.transloco.translate('workspaces.trash.restored', { name: restored.name }),
          );
        },
        error: (err) =>
          this.toast(
            this.transloco.translate(serverErrorKey(err, 'workspaces.trash.restoreFailed')),
            5000,
          ),
      });
  }

  private toast(message: string, duration = 3000): void {
    this.snackBar.open(message, this.transloco.translate('common.actions.close'), { duration });
  }
}
