import { ChangeDetectionStrategy, Component, DestroyRef, inject } from '@angular/core';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, MatPaginatorIntl } from '@angular/material/paginator';
import { MatSortModule } from '@angular/material/sort';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { DatePipe } from '@angular/common';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { TranslocoPaginatorIntl } from '@core/i18n/transloco-paginator-intl';
import { UserService } from '@core/services/user.service';
import { AdminUser } from '@core/models/admin-user';
import { TableState } from '@shared/table/table-state';
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
    DatePipe,
    TranslocoDirective,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [{ provide: MatPaginatorIntl, useClass: TranslocoPaginatorIntl }],
})
export class TrashUsersComponent {
  private userService = inject(UserService);
  private destroyRef = inject(DestroyRef);
  private dialog = inject(MatDialog);
  private snackBar = inject(MatSnackBar);
  private transloco = inject(TranslocoService);

  deleted = this.userService.allDeletedUsers();

  table = new TableState<AdminUser>({
    source: () => (this.deleted.hasValue() ? this.deleted.value() : []),
    searchText: (u) => `${u.firstName} ${u.lastName} ${u.email}`,
    sortValue: (u, column) => {
      switch (column) {
        case 'name':
          return `${u.firstName} ${u.lastName}`.toLowerCase();
        case 'email':
          return u.email.toLowerCase();
        case 'deletedAt':
          return u.deletedAt ?? '';
        default:
          return '';
      }
    },
  });

  displayedColumns = ['index', 'name', 'email', 'deletedAt', 'actions'];

  restoreUser(user: AdminUser): void {
    this.userService.restoreUser(user.id).subscribe({
      next: () => {
        this.deleted.update((list) => list.filter((u) => u.id !== user.id));
        this.notify('admin.trashUsers.restoredNamed', { name: fullName(user) });
      },
      error: (err) => this.notifyError(err, 'admin.trashUsers.restoreFailed'),
    });
  }

  confirmErase(user: AdminUser): void {
    this.dialog
      .open(ConfirmDialogComponent, {
        width: '460px',
        data: {
          title: this.transloco.translate('admin.trashUsers.eraseTitle'),
          message: this.transloco.translate('admin.trashUsers.eraseMessage', {
            name: fullName(user),
            email: user.email,
          }),
          confirmLabel: this.transloco.translate('common.actions.erase'),
          warn: true,
        },
      })
      .afterClosed()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((confirmed: boolean | undefined) => {
        if (!confirmed) return;

        this.userService.anonymizeUser(user.id).subscribe({
          next: () => {
            this.deleted.update((list) => list.filter((u) => u.id !== user.id));
            this.notify('admin.trashUsers.erased');
          },
          error: (err) => this.notifyError(err, 'admin.trashUsers.eraseFailed'),
        });
      });
  }

  private notify(key: string, params?: Record<string, unknown>): void {
    this.snackBar.open(
      this.transloco.translate(key, params),
      this.transloco.translate('common.actions.close'),
      { duration: 3000 },
    );
  }

  private notifyError(err: unknown, fallbackKey: string): void {
    this.snackBar.open(
      this.transloco.translate(serverErrorKey(err, fallbackKey), serverErrorParams(err)),
      this.transloco.translate('common.actions.close'),
      { duration: 10000 },
    );
  }
}

function fullName(user: AdminUser): string {
  return `${user.firstName} ${user.lastName}`;
}
