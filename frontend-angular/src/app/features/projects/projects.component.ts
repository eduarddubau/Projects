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
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { DatePipe } from '@angular/common';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { combineLatestWith, debounceTime, distinctUntilChanged, startWith } from 'rxjs/operators';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ProjectsDataSource } from './projects-datasource';
import { ProjectService } from '@core/services/project.service';
import { Project } from '@core/models/project';
import { ProjectFormDialogComponent, ProjectFormResult } from './project-form-dialog/project-form-dialog.component';
import { ConfirmDialogComponent } from '@shared/confirm-dialog/confirm-dialog.component';

@Component({
  selector: 'app-projects',
  templateUrl: './projects.component.html',
  styleUrl: './projects.component.scss',
  imports: [
    MatTableModule,
    MatPaginatorModule,
    MatSortModule,
    MatFormFieldModule,
    MatInputModule,
    MatSlideToggleModule,
    MatProgressSpinnerModule,
    MatIconModule,
    MatButtonModule,
    MatTooltipModule,
    ReactiveFormsModule,
    DatePipe
  ],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ProjectsComponent implements OnInit, AfterViewInit {
  @ViewChild(MatPaginator) paginator!: MatPaginator;
  @ViewChild(MatSort) sort!: MatSort;

  private projectService = inject(ProjectService);
  private cdr = inject(ChangeDetectorRef);
  private destroyRef = inject(DestroyRef);
  private dialog = inject(MatDialog);
  private snackBar = inject(MatSnackBar);

  isLoading = signal(true);
  hasError = signal(false);

  dataSource = new ProjectsDataSource();
  displayedColumns = ['index', 'name', 'description', 'createdBy', 'createdAt', 'status', 'actions'];
  searchControl = new FormControl('');
  showDeletedControl = new FormControl(false);

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
      combineLatestWith(
        this.showDeletedControl.valueChanges.pipe(startWith(this.showDeletedControl.value))
      ),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(([searchTerm, showDeleted]) => {
      this.dataSource.state = {
        searchQuery: searchTerm?.trim().toLowerCase() ?? '',
        showDeleted: !!showDeleted
      };
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

  openEditDialog(project: Project): void {
    this.dialog.open(ProjectFormDialogComponent, { width: '480px', data: { project } })
      .afterClosed()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((result: ProjectFormResult | undefined) => {
        if (!result) return;

        this.projectService.updateProject(project.id, result).subscribe({
          next: (updated) => {
            this.dataSource.data = this.dataSource.data.map(p => p.id === updated.id ? updated : p);
            this.dataSource.triggerUpdate();
            this.cdr.markForCheck();
            this.snackBar.open('Project updated.', 'Close', { duration: 3000 });
          },
          error: () => this.snackBar.open('Failed to update project.', 'Close', { duration: 5000 })
        });
      });
  }

  confirmDelete(project: Project): void {
    this.dialog.open(ConfirmDialogComponent, {
      width: '400px',
      data: {
        title: 'Delete Project',
        message: `Are you sure you want to delete "${project.name}"? This can be undone by an administrator.`,
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
            this.dataSource.data = this.dataSource.data.map(p =>
              p.id === project.id ? { ...p, isDeleted: true } : p
            );
            this.dataSource.triggerUpdate();
            this.cdr.markForCheck();
            this.snackBar.open('Project deleted.', 'Close', { duration: 3000 });
          },
          error: () => this.snackBar.open('Failed to delete project.', 'Close', { duration: 5000 })
        });
      });
  }

  restoreProject(project: Project): void {
    this.projectService.restoreMyProject(project.id).subscribe({
      next: (updated) => {
        this.dataSource.data = this.dataSource.data.map(p => p.id === updated.id ? updated : p);
        this.dataSource.triggerUpdate();
        this.cdr.markForCheck();
        this.snackBar.open('Project restored.', 'Close', { duration: 3000 });
      },
      error: () => this.snackBar.open('Failed to restore project.', 'Close', { duration: 5000 })
    });
  }
}