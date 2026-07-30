import {
  AfterViewInit, Component, ViewChild, inject,
  DestroyRef, ChangeDetectionStrategy, OnInit,
  ChangeDetectorRef, signal, afterNextRender
} from '@angular/core';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, MatPaginator, MatPaginatorIntl } from '@angular/material/paginator';
import { MatSortModule, MatSort } from '@angular/material/sort';
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
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { debounceTime, distinctUntilChanged, startWith } from 'rxjs/operators';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { TranslocoPaginatorIntl } from '@core/i18n/transloco-paginator-intl';
import { serverErrorKey } from '@core/i18n/server-error-keys';
import { ProjectsDataSource } from './projects-datasource';
import { ProjectService } from '@core/services/project.service';
import { Project } from '@core/models/project';
import { ProjectFormDialogComponent, ProjectFormResult } from './project-form-dialog/project-form-dialog.component';
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
    ReactiveFormsModule,
    DatePipe,
    AuroraComponent,
    TranslocoDirective
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [{ provide: MatPaginatorIntl, useClass: TranslocoPaginatorIntl }]
})
export class ProjectsComponent implements OnInit, AfterViewInit {
  @ViewChild(MatPaginator) paginator!: MatPaginator;
  @ViewChild(MatSort) sort!: MatSort;

  private projectService = inject(ProjectService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private cdr = inject(ChangeDetectorRef);
  private destroyRef = inject(DestroyRef);
  private dialog = inject(MatDialog);
  private snackBar = inject(MatSnackBar);
  private transloco = inject(TranslocoService);

  isLoading = signal(true);
  hasError = signal(false);

  dataSource = new ProjectsDataSource();
  displayedColumns = ['index', 'name', 'description', 'createdBy', 'createdAt', 'actions'];
  searchControl = new FormControl('');

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

  ngOnInit() {
    this.projectService.getMyProjects()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (projects) => {
          this.dataSource.data = projects;
          this.isLoading.set(false);
          this.dataSource.triggerUpdate();
          this.cdr.markForCheck();
        },
        error: () => {
          this.hasError.set(true);
          this.isLoading.set(false);
          this.cdr.markForCheck();
        }
      });

    this.searchControl.valueChanges.pipe(
      startWith(this.searchControl.value),
      debounceTime(300),
      distinctUntilChanged(),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe((searchTerm) => {
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

  openCreateDialog(): void {
    this.dialog.open(ProjectFormDialogComponent, { width: '480px', data: {} })
      .afterClosed()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((result: ProjectFormResult | undefined) => {
        if (!result) return;

        this.projectService.createProject(result).subscribe({
          next: (project) => {
            this.dataSource.data = [project, ...this.dataSource.data];
            this.dataSource.triggerUpdate();
            this.cdr.markForCheck();
            this.snackBar.open(
              this.transloco.translate('projects.notifications.created'),
              this.transloco.translate('common.actions.close'),
              { duration: 3000 },
            );
          },
          error: (err) => this.snackBar.open(
            this.transloco.translate(serverErrorKey(err, 'projects.notifications.createFailed')),
            this.transloco.translate('common.actions.close'),
            { duration: 5000 },
          )
        });
      });
  }

  openDetail(project: Project): void {
    this.router.navigate(['/projects', project.id]);
  }

  confirmDelete(project: Project): void {
    this.dialog.open(ConfirmDialogComponent, {
      width: '400px',
      data: {
        title: this.transloco.translate('projects.confirmDelete.title'),
        message: this.transloco.translate('projects.confirmDelete.message', { name: project.name }),
        confirmLabel: this.transloco.translate('common.actions.delete'),
        warn: true
      }
    })
      .afterClosed()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((confirmed: boolean | undefined) => {
        if (!confirmed) return;

        this.projectService.deleteMyProject(project.id).subscribe({
          next: () => {
            this.dataSource.data = this.dataSource.data.filter(p => p.id !== project.id);
            this.dataSource.triggerUpdate();
            this.cdr.markForCheck();
            this.snackBar.open(
              this.transloco.translate('projects.notifications.deleted'),
              this.transloco.translate('common.actions.close'),
              { duration: 3000 },
            );
          },
          error: () => this.snackBar.open(
            this.transloco.translate('projects.notifications.deleteFailed'),
            this.transloco.translate('common.actions.close'),
            { duration: 5000 },
          )
        });
      });
  }
}
