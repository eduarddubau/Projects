import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  afterNextRender,
  inject,
} from '@angular/core';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, MatPaginatorIntl } from '@angular/material/paginator';
import { MatSortModule } from '@angular/material/sort';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { DatePipe } from '@angular/common';
import { map } from 'rxjs/operators';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { TranslocoPaginatorIntl } from '@core/i18n/transloco-paginator-intl';
import { serverErrorKey } from '@core/i18n/server-error-keys';
import { ProjectService } from '@core/services/project.service';
import { WorkspaceContextService } from '@core/services/workspace-context.service';
import { Project } from '@core/models/project';
import {
  ProjectFormDialogComponent,
  ProjectFormResult,
} from './project-form-dialog/project-form-dialog.component';
import { TableState } from '@shared/table/table-state';
import { ConfirmDialogComponent } from '@shared/confirm-dialog/confirm-dialog.component';
import { AuroraComponent } from '@shared/aurora/aurora.component';

@Component({
  selector: 'app-projects',
  templateUrl: './projects.component.html',
  imports: [
    MatTableModule,
    MatPaginatorModule,
    MatSortModule,
    MatFormFieldModule,
    MatInputModule,
    MatProgressSpinnerModule,
    MatIconModule,
    MatButtonModule,
    MatTooltipModule,
    RouterLink,
    DatePipe,
    AuroraComponent,
    TranslocoDirective,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [{ provide: MatPaginatorIntl, useClass: TranslocoPaginatorIntl }],
})
export class ProjectsComponent {
  private projectService = inject(ProjectService);
  private workspaceContext = inject(WorkspaceContextService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private destroyRef = inject(DestroyRef);
  private dialog = inject(MatDialog);
  private snackBar = inject(MatSnackBar);
  private transloco = inject(TranslocoService);

  isOwner = this.workspaceContext.isOwner;

  // From paramMap, not the snapshot: switching workspace navigates between two
  // instances of this same route, and Angular reuses the component.
  workspaceId = toSignal(this.route.paramMap.pipe(map((p) => p.get('workspaceId'))), {
    initialValue: this.route.snapshot.paramMap.get('workspaceId'),
  });

  projects = this.projectService.workspaceProjects(this.workspaceId);

  table = new TableState<Project>({
    source: () => (this.projects.hasValue() ? this.projects.value() : []),
    searchText: (p) => p.name,
    sortValue: (p, column) => {
      switch (column) {
        case 'name':
          return p.name.toLowerCase();
        case 'description':
          return p.description?.toLowerCase() ?? '';
        case 'createdBy':
          return p.createdByDisplayName?.toLowerCase() ?? '';
        case 'createdAt':
          return p.createdAt;
        default:
          return '';
      }
    },
  });

  displayedColumns = ['index', 'name', 'description', 'createdBy', 'createdAt', 'actions'];

  constructor() {
    // Deep link from the dashboard's "New Project" button: open the create
    // dialog on arrival, then drop the flag so a reload won't reopen it.
    afterNextRender(() => {
      if (this.route.snapshot.queryParamMap.has('new')) {
        this.router.navigate([], { relativeTo: this.route, queryParams: {}, replaceUrl: true });
        this.openCreateDialog();
      }
    });
  }

  openCreateDialog(): void {
    this.dialog
      .open(ProjectFormDialogComponent, { width: '480px', data: {} })
      .afterClosed()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((result: ProjectFormResult | undefined) => {
        if (!result) return;

        const workspaceId = this.workspaceId();
        if (!workspaceId) return;

        this.projectService.createProject(workspaceId, result).subscribe({
          next: (project) => {
            this.projects.update((list) => [project, ...list]);
            this.snackBar.open(
              this.transloco.translate('projects.notifications.created'),
              this.transloco.translate('common.actions.close'),
              { duration: 3000 },
            );
          },
          error: (err) =>
            this.snackBar.open(
              this.transloco.translate(serverErrorKey(err, 'projects.notifications.createFailed')),
              this.transloco.translate('common.actions.close'),
              { duration: 5000 },
            ),
        });
      });
  }

  openDetail(project: Project): void {
    this.router.navigate(['/w', this.workspaceId(), 'projects', project.id]);
  }

  confirmDelete(project: Project): void {
    this.dialog
      .open(ConfirmDialogComponent, {
        width: '400px',
        data: {
          title: this.transloco.translate('projects.confirmDelete.title'),
          message: this.transloco.translate('projects.confirmDelete.message', {
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

        this.projectService.deleteProject(project.id).subscribe({
          next: () => {
            this.projects.update((list) => list.filter((p) => p.id !== project.id));
            this.snackBar.open(
              this.transloco.translate('projects.notifications.deleted'),
              this.transloco.translate('common.actions.close'),
              { duration: 3000 },
            );
          },
          error: () =>
            this.snackBar.open(
              this.transloco.translate('projects.notifications.deleteFailed'),
              this.transloco.translate('common.actions.close'),
              { duration: 5000 },
            ),
        });
      });
  }
}
