import {
  AfterViewInit, Component, ViewChild, inject,
  DestroyRef, ChangeDetectionStrategy, OnInit,
  ChangeDetectorRef, signal
} from '@angular/core';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, MatPaginator } from '@angular/material/paginator';
import { MatSortModule, MatSort } from '@angular/material/sort';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatTooltipModule } from '@angular/material/tooltip';
import { Router, RouterLink } from '@angular/router';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { DatePipe } from '@angular/common';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { debounceTime, distinctUntilChanged, startWith } from 'rxjs/operators';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
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
    AuroraComponent
  ],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ProjectsComponent implements OnInit, AfterViewInit {
  @ViewChild(MatPaginator) paginator!: MatPaginator;
  @ViewChild(MatSort) sort!: MatSort;

  private projectService = inject(ProjectService);
  private router = inject(Router);
  private cdr = inject(ChangeDetectorRef);
  private destroyRef = inject(DestroyRef);
  private dialog = inject(MatDialog);
  private snackBar = inject(MatSnackBar);

  isLoading = signal(true);
  hasError = signal(false);

  dataSource = new ProjectsDataSource();
  displayedColumns = ['index', 'name', 'description', 'createdBy', 'createdAt', 'actions'];
  searchControl = new FormControl('');

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
            this.snackBar.open('Project created.', 'Close', { duration: 3000 });
          },
          error: () => this.snackBar.open('Failed to create project.', 'Close', { duration: 5000 })
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
        title: 'Delete Project',
        message: `Are you sure you want to delete "${project.name}"? You can restore it from Trash later.`,
        confirmLabel: 'Delete',
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
            this.snackBar.open('Project deleted.', 'Close', { duration: 3000 });
          },
          error: () => this.snackBar.open('Failed to delete project.', 'Close', { duration: 5000 })
        });
      });
  }
}
