import {
  AfterViewInit,
  Component,
  ViewChild,
  inject,
  DestroyRef,
  ChangeDetectionStrategy,
  OnInit,
  ChangeDetectorRef,
  effect,
} from '@angular/core';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, MatPaginator, MatPaginatorIntl } from '@angular/material/paginator';
import { MatSortModule, MatSort } from '@angular/material/sort';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { RouterLink } from '@angular/router';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { DatePipe } from '@angular/common';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { debounceTime, distinctUntilChanged, startWith } from 'rxjs/operators';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { TranslocoPaginatorIntl } from '@core/i18n/transloco-paginator-intl';
import { ProjectsDataSource } from '@features/projects/projects-datasource';
import { ProjectService } from '@core/services/project.service';
import { Project } from '@core/models/project';
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
    ReactiveFormsModule,
    DatePipe,
    TranslocoDirective,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [{ provide: MatPaginatorIntl, useClass: TranslocoPaginatorIntl }],
})
export class AdminProjectsComponent implements OnInit, AfterViewInit {
  @ViewChild(MatPaginator) paginator!: MatPaginator;
  @ViewChild(MatSort) sort!: MatSort;

  private projectService = inject(ProjectService);
  private cdr = inject(ChangeDetectorRef);
  private destroyRef = inject(DestroyRef);
  private dialog = inject(MatDialog);
  private snackBar = inject(MatSnackBar);
  private transloco = inject(TranslocoService);

  projects = this.projectService.allProjects();

  dataSource = new ProjectsDataSource();
  displayedColumns = ['index', 'name', 'createdBy', 'createdAt', 'actions'];
  searchControl = new FormControl('');

  constructor() {
    // The table reads a plain property, not a signal, so the resource's value has
    // to be pushed across; every local edit below goes through the resource too,
    // which keeps this the single place the table is filled.
    effect(() => {
      // value() throws while the resource is in its error state, and an effect
      // that throws takes change detection down with it.
      if (!this.projects.hasValue()) return;

      this.dataSource.data = this.projects.value().filter((p) => !p.isDeleted);
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
      .subscribe(() => this.applyFilters());
  }

  ngAfterViewInit(): void {
    this.dataSource.sort = this.sort;
    this.dataSource.paginator = this.paginator;
    this.dataSource.triggerUpdate();
    this.cdr.detectChanges();
  }

  private applyFilters(): void {
    this.dataSource.state = {
      searchQuery: this.searchControl.value?.trim().toLowerCase() ?? '',
    };
    if (this.dataSource.paginator) {
      this.dataSource.paginator.pageIndex = 0;
    }
    this.dataSource.triggerUpdate();
    this.cdr.markForCheck();
  }

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
            this.dataSource.triggerUpdate();
            this.cdr.markForCheck();
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
