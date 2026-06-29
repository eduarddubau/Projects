import {
  AfterViewInit, Component, ViewChild, inject,
  DestroyRef, ChangeDetectionStrategy, OnInit,
  ChangeDetectorRef, signal
} from '@angular/core';
import { MatTableModule, MatTableDataSource } from '@angular/material/table';
import { MatPaginatorModule, MatPaginator } from '@angular/material/paginator';
import { MatSortModule, MatSort } from '@angular/material/sort';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { RouterLink } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { DatePipe } from '@angular/common';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { debounceTime, distinctUntilChanged, startWith } from 'rxjs/operators';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { UserService } from '@core/services/user.service';
import { AdminUser } from '@core/models/admin-user';

@Component({
  selector: 'app-trash-users',
  templateUrl: './trash-users.component.html',
  styleUrl: './trash-users.component.scss',
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
export class TrashUsersComponent implements OnInit, AfterViewInit {
  @ViewChild(MatPaginator) paginator!: MatPaginator;
  @ViewChild(MatSort) sort!: MatSort;

  private userService = inject(UserService);
  private cdr = inject(ChangeDetectorRef);
  private destroyRef = inject(DestroyRef);
  private snackBar = inject(MatSnackBar);

  isLoading = signal(true);
  hasError = signal(false);

  dataSource = new MatTableDataSource<AdminUser>([]);
  displayedColumns = ['index', 'name', 'email', 'deletedAt', 'actions'];
  searchControl = new FormControl('');

  ngOnInit() {
    this.dataSource.filterPredicate = (user, filter) =>
      `${user.firstName} ${user.lastName} ${user.email}`.toLowerCase().includes(filter);
    this.dataSource.sortingDataAccessor = (user, column) => {
      switch (column) {
        case 'name': return `${user.firstName} ${user.lastName}`.toLowerCase();
        case 'email': return user.email.toLowerCase();
        case 'deletedAt': return user.deletedAt ?? '';
        default: return '';
      }
    };

    this.userService.getDeletedUsers()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (users) => {
          this.dataSource.data = users;
          this.isLoading.set(false);
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
    ).subscribe((term) => {
      this.dataSource.filter = term?.trim().toLowerCase() ?? '';
      if (this.dataSource.paginator) {
        this.dataSource.paginator.firstPage();
      }
      this.cdr.markForCheck();
    });
  }

  ngAfterViewInit(): void {
    this.dataSource.sort = this.sort;
    this.dataSource.paginator = this.paginator;
    this.cdr.detectChanges();
  }

  restoreUser(user: AdminUser): void {
    this.userService.restoreUser(user.id).subscribe({
      next: () => {
        this.dataSource.data = this.dataSource.data.filter(u => u.id !== user.id);
        this.cdr.markForCheck();
        this.snackBar.open(`"${user.firstName} ${user.lastName}" restored.`, 'Close', { duration: 3000 });
      },
      error: () => this.snackBar.open('Failed to restore user.', 'Close', { duration: 5000 })
    });
  }
}
