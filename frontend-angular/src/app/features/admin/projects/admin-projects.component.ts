import { ChangeDetectionStrategy, Component, DestroyRef, inject } from '@angular/core';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, MatPaginatorIntl } from '@angular/material/paginator';
import { MatSortModule } from '@angular/material/sort';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { RouterLink } from '@angular/router';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { DatePipe } from '@angular/common';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { TranslocoPaginatorIntl } from '@core/i18n/transloco-paginator-intl';
import { ProjectService } from '@core/services/project.service';
import { Project } from '@core/models/project';
import { TableState } from '@shared/table/table-state';
import { ConfirmDialogComponent } from '@shared/confirm-dialog/confirm-dialog.component';

@Component({
  selector: 'app-admin-projects',
  templateUrl: './admin-projects.component.html',
  imports: [
    MatTableModule,
    MatPaginatorModule,
    MatSortModule,
    MatFormFieldModule,
    MatInputModule,
    MatProgressSpinnerModule,
    MatIconModule,
    MatButtonModule,
    RouterLink,
    DatePipe,
    TranslocoDirective,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [{ provide: MatPaginatorIntl, useClass: TranslocoPaginatorIntl }],
})
export class AdminProjectsComponent {
  private projectService = inject(ProjectService);
  private destroyRef = inject(DestroyRef);
  private dialog = inject(MatDialog);
  private snackBar = inject(MatSnackBar);
  private transloco = inject(TranslocoService);

  projects = this.projectService.allProjects();

  table = new TableState<Project>({
    source: () =>
      this.projects.hasValue() ? this.projects.value().filter((p) => !p.isDeleted) : [],
    searchText: (p) => p.name,
    sortValue: (p, column) => {
      switch (column) {
        case 'name':
          return p.name.toLowerCase();
        case 'createdBy':
          return p.createdByDisplayName?.toLowerCase() ?? '';
        case 'createdAt':
          return p.createdAt;
        default:
          return '';
      }
    },
  });

  displayedColumns = ['index', 'name', 'createdBy', 'createdAt', 'actions'];

  confirmDelete(project: Project): void {
    this.dialog
      .open(ConfirmDialogComponent, {
        width: '420px',
        data: {
          title: this.transloco.translate('admin.projects.confirmDelete.title'),
          message: this.transloco.translate('admin.projects.confirmDelete.message', {
            name: project.name,
          }),
          confirmLabel: this.transloco.translate('common.actions.delete'),
          warn: true,
        },
      })
      .afterClosed()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((confirmed: boolean | undefined) => {
        if (!confirmed) return;

        this.projectService.deleteAnyProject(project.id).subscribe({
          next: () => {
            this.projects.update((list) => list.filter((p) => p.id !== project.id));
            this.snackBar.open(
              this.transloco.translate('admin.projects.deletedNamed', { name: project.name }),
              this.transloco.translate('common.actions.close'),
              { duration: 3000 },
            );
          },
          error: () =>
            this.snackBar.open(
              this.transloco.translate('admin.projects.deleteFailed'),
              this.transloco.translate('common.actions.close'),
              { duration: 5000 },
            ),
        });
      });
  }
}
