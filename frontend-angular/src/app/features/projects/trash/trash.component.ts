import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, MatPaginatorIntl } from '@angular/material/paginator';
import { MatSortModule } from '@angular/material/sort';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatSnackBar } from '@angular/material/snack-bar';
import { DatePipe } from '@angular/common';
import { ActivatedRoute } from '@angular/router';
import { map } from 'rxjs/operators';
import { toSignal } from '@angular/core/rxjs-interop';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { TranslocoPaginatorIntl } from '@core/i18n/transloco-paginator-intl';
import { ProjectService } from '@core/services/project.service';
import { WorkspaceContextService } from '@core/services/workspace-context.service';
import { TableState } from '@shared/table/table-state';
import { AuroraComponent } from '@shared/aurora/aurora.component';
import { WorkspaceScopeComponent } from '@shared/workspace-scope/workspace-scope.component';
import { Project } from '@core/models/project';

@Component({
  selector: 'app-trash',
  templateUrl: './trash.component.html',
  imports: [
    MatTableModule,
    MatPaginatorModule,
    MatSortModule,
    MatFormFieldModule,
    MatInputModule,
    MatProgressSpinnerModule,
    MatIconModule,
    MatButtonModule,
    DatePipe,
    AuroraComponent,
    WorkspaceScopeComponent,
    TranslocoDirective,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [{ provide: MatPaginatorIntl, useClass: TranslocoPaginatorIntl }],
})
export class TrashComponent {
  private projectService = inject(ProjectService);
  private workspaceContext = inject(WorkspaceContextService);
  private route = inject(ActivatedRoute);
  private snackBar = inject(MatSnackBar);
  private transloco = inject(TranslocoService);

  isOwner = this.workspaceContext.isOwner;

  workspaceId = toSignal(this.route.paramMap.pipe(map((p) => p.get('workspaceId'))), {
    initialValue: this.route.snapshot.paramMap.get('workspaceId'),
  });

  deleted = this.projectService.workspaceDeletedProjects(this.workspaceId);

  table = new TableState<Project>({
    source: () => (this.deleted.hasValue() ? this.deleted.value() : []),
    searchText: (p) => p.name,
    sortValue: (p, column) => {
      switch (column) {
        case 'name':
          return p.name.toLowerCase();
        case 'description':
          return p.description?.toLowerCase() ?? '';
        case 'deletedAt':
          return p.deletedAt ?? '';
        default:
          return '';
      }
    },
  });

  displayedColumns = ['index', 'name', 'description', 'deletedAt', 'actions'];

  restoreProject(project: Project): void {
    this.projectService.restoreProject(project.id).subscribe({
      next: () => {
        this.deleted.update((list) => list.filter((p) => p.id !== project.id));
        this.snackBar.open(
          this.transloco.translate('projects.notifications.restored'),
          this.transloco.translate('common.actions.close'),
          { duration: 3000 },
        );
      },
      error: () =>
        this.snackBar.open(
          this.transloco.translate('projects.notifications.restoreFailed'),
          this.transloco.translate('common.actions.close'),
          { duration: 5000 },
        ),
    });
  }
}
