import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, MatPaginatorIntl } from '@angular/material/paginator';
import { MatSortModule } from '@angular/material/sort';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatIconModule } from '@angular/material/icon';
import { DatePipe } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { map } from 'rxjs/operators';
import { toSignal } from '@angular/core/rxjs-interop';
import { TranslocoDirective } from '@jsverse/transloco';
import { TranslocoPaginatorIntl } from '@core/i18n/transloco-paginator-intl';
import { ProjectService } from '@core/services/project.service';
import { TableState } from '@shared/table/table-state';
import { ClickableRowDirective } from '@shared/clickable-row/clickable-row.directive';
import { Project } from '@core/models/project';

@Component({
  selector: 'app-trash',
  templateUrl: './trash.component.html',
  imports: [
    ClickableRowDirective,
    MatTableModule,
    MatPaginatorModule,
    MatSortModule,
    MatFormFieldModule,
    MatInputModule,
    MatProgressSpinnerModule,
    MatIconModule,
    DatePipe,
    TranslocoDirective,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [{ provide: MatPaginatorIntl, useClass: TranslocoPaginatorIntl }],
})
export class TrashComponent {
  private projectService = inject(ProjectService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);

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

  displayedColumns = ['index', 'name', 'description', 'deletedAt'];

  // Restoring lives on the project's own page now, so this table only has to get you there.
  openDetail(project: Project): void {
    this.router.navigate(['/w', this.workspaceId(), 'projects', project.id]);
  }
}
