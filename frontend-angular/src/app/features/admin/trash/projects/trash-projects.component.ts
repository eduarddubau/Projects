import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
  inject,
  signal,
} from '@angular/core';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, MatPaginatorIntl } from '@angular/material/paginator';
import { MatSortModule } from '@angular/material/sort';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
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
import { ProjectService } from '@core/services/project.service';
import { Project } from '@core/models/project';
import { TableState } from '@shared/table/table-state';
import { TableSelection } from '@shared/table/table-selection';
import { ConfirmDialogComponent } from '@shared/confirm-dialog/confirm-dialog.component';
import { confirmPhraseFor } from '@shared/confirm-dialog/confirm-phrase';

type AgeFilter = 'all' | '30' | '60' | '90';

const DAY_MS = 24 * 60 * 60 * 1000;

@Component({
  selector: 'app-trash-projects',
  templateUrl: './trash-projects.component.html',
  imports: [
    MatTableModule,
    MatPaginatorModule,
    MatSortModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonToggleModule,
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
export class TrashProjectsComponent {
  private projectService = inject(ProjectService);
  private destroyRef = inject(DestroyRef);
  private dialog = inject(MatDialog);
  private snackBar = inject(MatSnackBar);
  private transloco = inject(TranslocoService);

  deleted = this.projectService.allDeletedProjects();

  table = new TableState<Project>({
    source: () => (this.deleted.hasValue() ? this.deleted.value() : []),
    searchText: (p) => p.name,
    sortValue: (p, column) => {
      switch (column) {
        case 'name':
          return p.name.toLowerCase();
        case 'createdBy':
          return p.createdByDisplayName?.toLowerCase() ?? '';
        case 'deletedAt':
          return p.deletedAt ?? '';
        default:
          return '';
      }
    },
  });

  selection = new TableSelection(this.table.matching, this.table.pageRows);

  ageFilter = signal<AgeFilter>('all');
  displayedColumns = ['select', 'index', 'name', 'createdBy', 'deletedAt', 'actions'];

  /** Purge is refused server-side inside the retention window, so offer it only when every pick qualifies. */
  canPurgeSelected = computed(() => {
    const selected = this.selection.selected();
    return selected.length > 0 && selected.every((p) => p.isPurgeable);
  });

  setAgeFilter(value: AgeFilter): void {
    this.ageFilter.set(value);

    if (value === 'all') {
      this.table.setFilter(null);
      return;
    }

    const cutoff = Date.now() - Number(value) * DAY_MS;
    this.table.setFilter((p) => !!p.deletedAt && new Date(p.deletedAt).getTime() < cutoff);
  }

  restoreProject(project: Project): void {
    this.restoreMany([project]);
  }

  confirmRestoreSelected(): void {
    this.restoreMany(this.selection.selected());
  }

  private restoreMany(projects: readonly Project[]): void {
    if (projects.length === 0) return;

    const ids = projects.map((p) => p.id);
    this.projectService.restoreProjects(ids).subscribe({
      next: ({ restoredCount }) => {
        this.deleted.update((list) => list.filter((p) => !ids.includes(p.id)));
        this.selection.deselect(projects);
        this.notify(
          restoredCount === 1
            ? 'admin.trashProjects.restoredOne'
            : 'admin.trashProjects.restoredMany',
          { count: restoredCount },
        );
      },
      error: () => this.notify('admin.trashProjects.restoreFailed', undefined, 5000),
    });
  }

  confirmPurge(project: Project): void {
    this.purge(
      [project],
      this.transloco.translate('admin.trashProjects.purgeMessageNamed', { name: project.name }),
    );
  }

  confirmPurgeSelected(): void {
    const purgeable = this.selection.selected().filter((p) => p.isPurgeable);
    if (purgeable.length === 0) return;

    this.purge(
      purgeable,
      this.transloco.translate(
        purgeable.length === 1
          ? 'admin.trashProjects.purgeMessageOne'
          : 'admin.trashProjects.purgeMessageMany',
        { count: purgeable.length },
      ),
    );
  }

  private purge(projects: readonly Project[], message: string): void {
    this.dialog
      .open(ConfirmDialogComponent, {
        width: '420px',
        data: {
          title: this.transloco.translate(
            projects.length > 1
              ? 'admin.trashProjects.purgeTitleMany'
              : 'admin.trashProjects.purgeTitleOne',
          ),
          message,
          confirmLabel: this.transloco.translate('common.actions.purge'),
          warn: true,
          ...this.confirmPhrase(projects),
        },
      })
      .afterClosed()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((confirmed: boolean | undefined) => {
        if (!confirmed) return;

        const ids = projects.map((p) => p.id);
        this.projectService.purgeProjects(ids).subscribe({
          next: ({ purgedCount }) => {
            this.deleted.update((list) => list.filter((p) => !ids.includes(p.id)));
            this.selection.deselect(projects);
            this.notify(
              purgedCount === 1
                ? 'admin.trashProjects.purgedOne'
                : 'admin.trashProjects.purgedMany',
              { count: purgedCount },
            );
          },
          error: () => this.notify('admin.trashProjects.purgeFailed', undefined, 5000),
        });
      });
  }

  private confirmPhrase(items: readonly Project[]) {
    return confirmPhraseFor(
      items,
      this.transloco.translate('common.confirmPhrase.projectName'),
      this.transloco.translate('common.confirmPhrase.count'),
    );
  }

  private notify(key: string, params?: Record<string, unknown>, duration = 3000): void {
    this.snackBar.open(
      this.transloco.translate(key, params),
      this.transloco.translate('common.actions.close'),
      { duration },
    );
  }
}
