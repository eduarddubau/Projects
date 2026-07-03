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
import { RouterLink } from '@angular/router';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { DatePipe } from '@angular/common';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { debounceTime, distinctUntilChanged, startWith } from 'rxjs/operators';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
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
    DatePipe
  ],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class AdminProjectsComponent implements OnInit, AfterViewInit {
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
  displayedColumns = ['index', 'name', 'createdBy', 'createdAt', 'actions'];
  searchControl = new FormControl('');

  ngOnInit() {
    this.projectService.getAllProjects()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (projects) => {
          this.dataSource.data = projects.filter(p => !p.isDeleted);
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
    ).subscribe(() => this.applyFilters());
  }

  ngAfterViewInit(): void {
    this.dataSource.sort = this.sort;
    this.dataSource.paginator = this.paginator;
    this.dataSource.triggerUpdate();
    this.cdr.detectChanges();
  }

  private applyFilters(): void {
    this.dataSource.state = {
      searchQuery: this.searchControl.value?.trim().toLowerCase() ?? ''
    };
    if (this.dataSource.paginator) {
      this.dataSource.paginator.pageIndex = 0;
    }
    this.dataSource.triggerUpdate();
    this.cdr.markForCheck();
  }

  confirmDelete(project: Project): void {
    this.dialog.open(ConfirmDialogComponent, {
      width: '420px',
      data: {
        title: 'Delete Project',
        message: `"${project.name}" will be moved to trash. It can be restored later from Projects Trash.`,
        confirmLabel: 'Delete',
        warn: true
      }
    })
      .afterClosed()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((confirmed: boolean | undefined) => {
        if (!confirmed) return;

        this.projectService.deleteAnyProject(project.id).subscribe({
          next: () => {
            this.dataSource.data = this.dataSource.data.filter(p => p.id !== project.id);
            this.dataSource.triggerUpdate();
            this.cdr.markForCheck();
            this.snackBar.open(`"${project.name}" deleted.`, 'Close', { duration: 3000 });
          },
          error: () => this.snackBar.open('Failed to delete project.', 'Close', { duration: 5000 })
        });
      });
  }
}
