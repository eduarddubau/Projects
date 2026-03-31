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
import { DatePipe } from '@angular/common';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { combineLatestWith, debounceTime, distinctUntilChanged, startWith } from 'rxjs/operators';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ProjectsDataSource } from './projects-datasource';
import { ProjectService } from '@core/services/project.service';
import { AuthService } from '@core/services/auth.service';

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
  private authService = inject(AuthService);
  private cdr = inject(ChangeDetectorRef);
  private destroyRef = inject(DestroyRef);

  isAdmin = this.authService.isAuthenticated;
  isLoading = signal(true);
  hasError = signal(false);

  dataSource = new ProjectsDataSource();
  displayedColumns = ['index', 'name', 'description', 'createdBy', 'createdAt'];
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
}