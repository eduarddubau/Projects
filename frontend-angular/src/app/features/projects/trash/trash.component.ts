import {
  AfterViewInit,
  Component,
  ViewChild,
  inject,
  effect,
  DestroyRef,
  ChangeDetectionStrategy,
  OnInit,
  ChangeDetectorRef,
} from '@angular/core';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, MatPaginator, MatPaginatorIntl } from '@angular/material/paginator';
import { MatSortModule, MatSort } from '@angular/material/sort';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatSnackBar } from '@angular/material/snack-bar';
import { DatePipe } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { debounceTime, distinctUntilChanged, map, startWith } from 'rxjs/operators';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { TranslocoPaginatorIntl } from '@core/i18n/transloco-paginator-intl';
import { ProjectsDataSource } from '../projects-datasource';
import { ProjectService } from '@core/services/project.service';
import { WorkspaceContextService } from '@core/services/workspace-context.service';
import { AuroraComponent } from '@shared/aurora/aurora.component';
import { Project } from '@core/models/project';

@Component({
  selector: 'app-trash',
  templateUrl: './trash.component.html',
  styleUrl: './trash.component.scss',
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
    ReactiveFormsModule,
    DatePipe,
    AuroraComponent,
    TranslocoDirective,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [{ provide: MatPaginatorIntl, useClass: TranslocoPaginatorIntl }],
})
export class TrashComponent implements OnInit, AfterViewInit {
  @ViewChild(MatPaginator) paginator!: MatPaginator;
  @ViewChild(MatSort) sort!: MatSort;

  private projectService = inject(ProjectService);
  private workspaceContext = inject(WorkspaceContextService);

  isOwner = this.workspaceContext.isOwner;
  private route = inject(ActivatedRoute);
  private cdr = inject(ChangeDetectorRef);
  private destroyRef = inject(DestroyRef);
  private snackBar = inject(MatSnackBar);
  private transloco = inject(TranslocoService);

  workspaceId = toSignal(this.route.paramMap.pipe(map((p) => p.get('workspaceId'))), {
    initialValue: this.route.snapshot.paramMap.get('workspaceId'),
  });

  deleted = this.projectService.workspaceDeletedProjects(this.workspaceId);

  dataSource = new ProjectsDataSource();
  displayedColumns = ['index', 'name', 'description', 'deletedAt', 'actions'];
  searchControl = new FormControl('');

  constructor() {
    // The table reads a plain property, not a signal, so the resource's value has
    // to be pushed across; every local edit below goes through the resource too,
    // which keeps this the single place the table is filled.
    effect(() => {
      // value() throws while the resource is in its error state, and an effect
      // that throws takes change detection down with it.
      if (!this.deleted.hasValue()) return;

      this.dataSource.data = this.deleted.value();
      this.dataSource.triggerUpdate();
      this.cdr.markForCheck();
    });
  }

  ngOnInit() {
    this.searchControl.valueChanges
      .pipe(
        startWith(this.searchControl.value),
        debounceTime(300),
        distinctUntilChanged(),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((searchTerm) => {
        this.dataSource.state = { searchQuery: searchTerm?.trim().toLowerCase() ?? '' };
        if (this.dataSource.paginator) {
          this.dataSource.paginator.pageIndex = 0;
        }
        this.cdr.markForCheck();
      });
  }

  ngAfterViewInit(): void {
    this.dataSource.sort = this.sort;
    this.dataSource.paginator = this.paginator;
    this.dataSource.triggerUpdate();
    this.cdr.detectChanges();
  }

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
