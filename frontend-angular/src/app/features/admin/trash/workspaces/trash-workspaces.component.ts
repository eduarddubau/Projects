import { ChangeDetectionStrategy, Component, DestroyRef, inject } from '@angular/core';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, MatPaginatorIntl } from '@angular/material/paginator';
import { MatSortModule } from '@angular/material/sort';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { DatePipe } from '@angular/common';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { TranslocoPaginatorIntl } from '@core/i18n/transloco-paginator-intl';
import { WorkspaceService } from '@core/services/workspace.service';
import { AdminWorkspace } from '@core/models/admin-workspace';
import { TableState } from '@shared/table/table-state';
import { TableSelection } from '@shared/table/table-selection';
import { ConfirmDialogComponent } from '@shared/confirm-dialog/confirm-dialog.component';
import { confirmPhraseFor } from '@shared/confirm-dialog/confirm-phrase';
import { serverErrorKey, serverErrorParams } from '@core/i18n/server-error-keys';

@Component({
  selector: 'app-trash-workspaces',
  templateUrl: './trash-workspaces.component.html',
  imports: [
    MatTableModule,
    MatPaginatorModule,
    MatSortModule,
    MatFormFieldModule,
    MatInputModule,
    MatCheckboxModule,
    MatProgressSpinnerModule,
    MatIconModule,
    MatButtonModule,
    DatePipe,
    TranslocoDirective,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [{ provide: MatPaginatorIntl, useClass: TranslocoPaginatorIntl }],
})
export class TrashWorkspacesComponent {
  private workspaceService = inject(WorkspaceService);
  private destroyRef = inject(DestroyRef);
  private dialog = inject(MatDialog);
  private snackBar = inject(MatSnackBar);
  private transloco = inject(TranslocoService);

  deleted = this.workspaceService.allDeletedWorkspaces();

  table = new TableState<AdminWorkspace>({
    source: () => (this.deleted.hasValue() ? this.deleted.value() : []),
    searchText: (w) => `${w.name} ${w.createdByDisplayName ?? ''}`,
    sortValue: (w, column) => {
      switch (column) {
        case 'name':
          return w.name.toLowerCase();
        case 'createdBy':
          return w.createdByDisplayName?.toLowerCase() ?? '';
        case 'members':
          return w.memberCount;
        case 'deletedAt':
          return w.deletedAt ?? '';
        default:
          return '';
      }
    },
  });

  selection = new TableSelection(this.table.matching, this.table.pageRows);

  displayedColumns = [
    'select',
    'index',
    'name',
    'type',
    'members',
    'createdBy',
    'deletedAt',
    'actions',
  ];

  restore(workspace: AdminWorkspace): void {
    this.restoreMany([workspace]);
  }

  restoreSelected(): void {
    this.restoreMany(this.selection.selected());
  }

  private restoreMany(workspaces: readonly AdminWorkspace[]): void {
    if (workspaces.length === 0) return;

    const ids = workspaces.map((w) => w.id);
    this.workspaceService.restoreWorkspaces(ids).subscribe({
      next: ({ restoredCount }) => {
        this.deleted.update((list) => list.filter((w) => !ids.includes(w.id)));
        this.selection.deselect(workspaces);
        this.notify(
          restoredCount === 1
            ? 'admin.trashWorkspaces.restoredOne'
            : 'admin.trashWorkspaces.restoredMany',
          { count: restoredCount },
        );
      },
      error: (err) => this.notifyError(err, 'admin.trashWorkspaces.restoreFailed'),
    });
  }

  confirmPurge(workspace: AdminWorkspace): void {
    this.purge(
      [workspace],
      this.transloco.translate('admin.trashWorkspaces.purgeMessageNamed', {
        name: workspace.name,
      }),
    );
  }

  confirmPurgeSelected(): void {
    const selected = this.selection.selected();
    if (selected.length === 0) return;

    this.purge(
      selected,
      this.transloco.translate(
        selected.length === 1
          ? 'admin.trashWorkspaces.purgeMessageOne'
          : 'admin.trashWorkspaces.purgeMessageMany',
        { count: selected.length },
      ),
    );
  }

  private purge(workspaces: readonly AdminWorkspace[], message: string): void {
    this.dialog
      .open(ConfirmDialogComponent, {
        width: '420px',
        data: {
          title: this.transloco.translate(
            workspaces.length > 1
              ? 'admin.trashWorkspaces.purgeTitleMany'
              : 'admin.trashWorkspaces.purgeTitleOne',
          ),
          message,
          confirmLabel: this.transloco.translate('common.actions.purge'),
          warn: true,
          ...this.confirmPhrase(workspaces),
        },
      })
      .afterClosed()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((confirmed: boolean | undefined) => {
        if (!confirmed) return;

        const ids = workspaces.map((w) => w.id);
        this.workspaceService.purgeWorkspaces(ids).subscribe({
          next: ({ purgedCount }) => {
            this.deleted.update((list) => list.filter((w) => !ids.includes(w.id)));
            this.selection.deselect(workspaces);
            this.notify(
              purgedCount === 1
                ? 'admin.trashWorkspaces.purgedOne'
                : 'admin.trashWorkspaces.purgedMany',
              { count: purgedCount },
            );
          },
          error: (err) => this.notifyError(err, 'admin.trashWorkspaces.purgeFailed'),
        });
      });
  }

  private confirmPhrase(items: readonly AdminWorkspace[]) {
    return confirmPhraseFor(
      items,
      this.transloco.translate('common.confirmPhrase.workspaceName'),
      this.transloco.translate('common.confirmPhrase.count'),
    );
  }

  private notify(key: string, params?: Record<string, unknown>): void {
    this.snackBar.open(
      this.transloco.translate(key, params),
      this.transloco.translate('common.actions.close'),
      { duration: 3000 },
    );
  }

  private notifyError(err: unknown, fallbackKey: string): void {
    this.snackBar.open(
      this.transloco.translate(serverErrorKey(err, fallbackKey), serverErrorParams(err)),
      this.transloco.translate('common.actions.close'),
      { duration: 10000 },
    );
  }
}
