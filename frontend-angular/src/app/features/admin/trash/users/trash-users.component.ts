import {
  AfterViewInit, Component, ViewChild, inject,
  DestroyRef, ChangeDetectionStrategy, OnInit,
  ChangeDetectorRef, effect
} from '@angular/core';
import { MatTableModule, MatTableDataSource } from '@angular/material/table';
import { MatPaginatorModule, MatPaginator, MatPaginatorIntl } from '@angular/material/paginator';
import { MatSortModule, MatSort } from '@angular/material/sort';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { DatePipe } from '@angular/common';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { debounceTime, distinctUntilChanged, startWith } from 'rxjs/operators';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { TranslocoPaginatorIntl } from '@core/i18n/transloco-paginator-intl';
import { UserService } from '@core/services/user.service';
import { AdminUser } from '@core/models/admin-user';
import { ConfirmDialogComponent } from '@shared/confirm-dialog/confirm-dialog.component';
import { serverErrorKey, serverErrorParams } from '@core/i18n/server-error-keys';

@Component({
  selector: 'app-trash-users',
  templateUrl: './trash-users.component.html',
  imports: [
    MatTableModule,
    MatPaginatorModule,
    MatSortModule,
    MatFormFieldModule,
    MatInputModule,
    MatProgressSpinnerModule,
    MatIconModule,
    MatButtonModule,
    ReactiveFormsModule,
    DatePipe,
    TranslocoDirective
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [{ provide: MatPaginatorIntl, useClass: TranslocoPaginatorIntl }]
})
export class TrashUsersComponent implements OnInit, AfterViewInit {
  @ViewChild(MatPaginator) paginator!: MatPaginator;
  @ViewChild(MatSort) sort!: MatSort;

  private userService = inject(UserService);
  private cdr = inject(ChangeDetectorRef);
  private destroyRef = inject(DestroyRef);
  private dialog = inject(MatDialog);
  private snackBar = inject(MatSnackBar);
  private transloco = inject(TranslocoService);

  deleted = this.userService.allDeletedUsers();

  dataSource = new MatTableDataSource<AdminUser>([]);
  displayedColumns = ['index', 'name', 'email', 'deletedAt', 'actions'];
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
      this.cdr.markForCheck();
    });
  }

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
        this.deleted.update((list) => list.filter((u) => u.id !== user.id));
        this.cdr.markForCheck();
        this.snackBar.open(
          this.transloco.translate('admin.trashUsers.restoredNamed', {
            name: `${user.firstName} ${user.lastName}`,
          }),
          this.transloco.translate('common.actions.close'),
          { duration: 3000 },
        );
      },
      error: (err) => this.snackBar.open(
        this.transloco.translate(
          serverErrorKey(err, 'admin.trashUsers.restoreFailed'),
          serverErrorParams(err)
        ),
        this.transloco.translate('common.actions.close'),
        { duration: 10000 },
      )
    });
  }

  confirmErase(user: AdminUser): void {
    this.dialog.open(ConfirmDialogComponent, {
      width: '460px',
      data: {
        title: this.transloco.translate('admin.trashUsers.eraseTitle'),
        message: this.transloco.translate('admin.trashUsers.eraseMessage', {
          name: `${user.firstName} ${user.lastName}`,
          email: user.email,
        }),
        confirmLabel: this.transloco.translate('common.actions.erase'),
        warn: true
      }
    })
      .afterClosed()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((confirmed: boolean | undefined) => {
        if (!confirmed) return;

        this.userService.anonymizeUser(user.id).subscribe({
          next: () => {
            this.deleted.update((list) => list.filter((u) => u.id !== user.id));
            this.cdr.markForCheck();
            this.snackBar.open(
              this.transloco.translate('admin.trashUsers.erased'),
              this.transloco.translate('common.actions.close'),
              { duration: 3000 },
            );
          },
          error: (err) => this.snackBar.open(
            this.transloco.translate(
              serverErrorKey(err, 'admin.trashUsers.eraseFailed'),
              serverErrorParams(err)
            ),
            this.transloco.translate('common.actions.close'),
            { duration: 10000 },
          )
        });
      });
  }
}
