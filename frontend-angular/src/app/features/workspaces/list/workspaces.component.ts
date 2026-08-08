import {
  Component,
  ChangeDetectionStrategy,
  DestroyRef,
  afterNextRender,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { serverErrorKey } from '@core/i18n/server-error-keys';
import { WorkspaceService } from '@core/services/workspace.service';
import { WorkspaceContextService } from '@core/services/workspace-context.service';
import { AuroraComponent } from '@shared/aurora/aurora.component';
import {
  WorkspaceFormDialogComponent,
  WorkspaceFormResult,
} from '../workspace-form-dialog/workspace-form-dialog.component';

@Component({
  selector: 'app-workspaces',
  templateUrl: './workspaces.component.html',
  styleUrl: './workspaces.component.scss',
  imports: [MatButtonModule, MatIconModule, AuroraComponent, TranslocoDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class WorkspacesComponent {
  private api = inject(WorkspaceService);
  private context = inject(WorkspaceContextService);
  private dialog = inject(MatDialog);
  private snackBar = inject(MatSnackBar);
  private transloco = inject(TranslocoService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private destroyRef = inject(DestroyRef);

  workspaces = this.context.workspaces;
  currentWorkspaceId = this.context.currentWorkspaceId;
  isSaving = signal(false);

  constructor() {
    // Deep link from the switcher's "New workspace" item. afterNextRender keeps
    // this browser-only — the dialog needs a DOM — but the subscription has to
    // outlive the first render: navigating here while already on this page
    // reuses the component, so a one-shot snapshot read would never fire again.
    // The param is cleared with replaceUrl so a refresh can't reopen the dialog.
    afterNextRender(() => {
      this.route.queryParamMap.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((params) => {
        if (!params.has('new')) return;
        this.router.navigate([], { relativeTo: this.route, queryParams: {}, replaceUrl: true });
        this.openCreateDialog();
      });
    });
  }

  select(id: string): void {
    this.context.setCurrent(id);
  }

  openCreateDialog(): void {
    this.dialog
      .open(WorkspaceFormDialogComponent, { width: '480px', data: {} })
      .afterClosed()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((result: WorkspaceFormResult | undefined) => {
        if (!result) return;

        this.isSaving.set(true);
        this.api.createWorkspace(result).subscribe({
          next: (created) => {
            this.isSaving.set(false);
            // Both: upsert makes it appear in the switcher, setCurrent selects it.
            this.context.upsert(created);
            this.context.setCurrent(created.id);
            this.snackBar.open(
              this.transloco.translate('workspaces.list.created'),
              this.transloco.translate('common.actions.close'),
              { duration: 3000 },
            );
          },
          error: (err) => {
            this.isSaving.set(false);
            this.snackBar.open(
              this.transloco.translate(serverErrorKey(err, 'workspaces.list.createFailed')),
              this.transloco.translate('common.actions.close'),
              { duration: 5000 },
            );
          },
        });
      });
  }
}
