import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
  inject,
  input,
} from '@angular/core';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, MatPaginatorIntl } from '@angular/material/paginator';
import { MatSortModule } from '@angular/material/sort';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { Router, RouterLink } from '@angular/router';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { DatePipe } from '@angular/common';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { TranslocoPaginatorIntl } from '@core/i18n/transloco-paginator-intl';
import { serverErrorKey } from '@core/i18n/server-error-keys';
import { ProjectService } from '@core/services/project.service';
import { WorkspaceContextService } from '@core/services/workspace-context.service';
import { Project } from '@core/models/project';
import {
  ProjectFormDialogComponent,
  ProjectFormResult,
} from '../project-form-dialog/project-form-dialog.component';
import { TableState } from '@shared/table/table-state';
import { ClickableRowDirective } from '@shared/clickable-row/clickable-row.directive';

/**
 * The workspace's projects, as a card. Takes its workspace from the caller rather than
 * the route so it can sit inside the workspace home alongside that page's other content.
 */
@Component({
  selector: 'app-projects-card',
  templateUrl: './projects-card.component.html',
  imports: [
    ClickableRowDirective,
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
export class ProjectsCardComponent {
  private projectService = inject(ProjectService);
  private workspaceContext = inject(WorkspaceContextService);
  private router = inject(Router);
  private destroyRef = inject(DestroyRef);
  private dialog = inject(MatDialog);
  private snackBar = inject(MatSnackBar);
  private transloco = inject(TranslocoService);

  /** Null while the host is still resolving it; the resource stays idle until it isn't. */
  workspaceId = input.required<string | null>();

  // Against the workspace this card was handed, not the context's current one — the input
  // exists precisely so the card can render a workspace that isn't the selected one, and
  // the owner-only actions have to follow it there.
  isOwner = computed(() => {
    const id = this.workspaceId();
    return !!id && this.workspaceContext.workspaces().find((w) => w.id === id)?.myRole === 'Owner';
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

  displayedColumns = ['index', 'name', 'description', 'createdBy', 'createdAt'];

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
}
