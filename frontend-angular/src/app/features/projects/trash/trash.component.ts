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
import { MatSnackBar } from '@angular/material/snack-bar';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { debounceTime, distinctUntilChanged, startWith } from 'rxjs/operators';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ProjectsDataSource } from '../projects-datasource';
import { ProjectService } from '@core/services/project.service';
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
    AuroraComponent
  ],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class TrashComponent implements OnInit, AfterViewInit {
  @ViewChild(MatPaginator) paginator!: MatPaginator;
  @ViewChild(MatSort) sort!: MatSort;

  private projectService = inject(ProjectService);
  private cdr = inject(ChangeDetectorRef);
  private destroyRef = inject(DestroyRef);
  private snackBar = inject(MatSnackBar);

  isLoading = signal(true);
  hasError = signal(false);

  dataSource = new ProjectsDataSource();
  displayedColumns = ['index', 'name', 'description', 'deletedAt', 'actions'];
  searchControl = new FormControl('');

  ngOnInit() {
    this.projectService.getMyDeletedProjects()
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

  restoreProject(project: Project): void {
    this.projectService.restoreMyProject(project.id).subscribe({
      next: () => {
        this.dataSource.data = this.dataSource.data.filter(p => p.id !== project.id);
        this.dataSource.triggerUpdate();
        this.cdr.markForCheck();
        this.snackBar.open('Project restored.', 'Close', { duration: 3000 });
      },
      error: () => this.snackBar.open('Failed to restore project.', 'Close', { duration: 5000 })
    });
  }
}
