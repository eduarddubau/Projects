import { ChangeDetectionStrategy, Component, DestroyRef, inject } from '@angular/core';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, MatPaginatorIntl } from '@angular/material/paginator';
import { MatSortModule } from '@angular/material/sort';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatTooltipModule } from '@angular/material/tooltip';
import { RouterLink } from '@angular/router';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { DatePipe } from '@angular/common';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { TranslocoPaginatorIntl } from '@core/i18n/transloco-paginator-intl';
import { UserService } from '@core/services/user.service';
import { AuthService } from '@core/services/auth.service';
import { AdminUser } from '@core/models/admin-user';
import { TableState } from '@shared/table/table-state';
import { ConfirmDialogComponent } from '@shared/confirm-dialog/confirm-dialog.component';

@Component({
  selector: 'app-admin-users',
  templateUrl: './admin-users.component.html',
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
    DatePipe,
    TranslocoDirective,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [{ provide: MatPaginatorIntl, useClass: TranslocoPaginatorIntl }],
})
export class AdminUsersComponent {
  private userService = inject(UserService);
  private authService = inject(AuthService);
  private destroyRef = inject(DestroyRef);
  private dialog = inject(MatDialog);
  private snackBar = inject(MatSnackBar);
  private transloco = inject(TranslocoService);

  users = this.userService.allUsers();

  table = new TableState<AdminUser>({
    source: () => (this.users.hasValue() ? this.users.value().filter((u) => !u.isDeleted) : []),
    searchText: (u) => `${u.firstName} ${u.lastName} ${u.email}`,
    sortValue: (u, column) => {
      switch (column) {
        case 'name':
          return `${u.firstName} ${u.lastName}`.toLowerCase();
        case 'email':
          return u.email.toLowerCase();
        case 'createdAt':
          return u.createdAt;
        default:
          return '';
      }
    },
  });

  displayedColumns = ['index', 'name', 'email', 'createdAt', 'actions'];

  private currentUserId = this.authService.currentUser()?.id;

  isSelf(user: AdminUser): boolean {
    return user.id === this.currentUserId;
  }

  confirmDelete(user: AdminUser): void {
    const name = `${user.firstName} ${user.lastName}`;

    this.dialog
      .open(ConfirmDialogComponent, {
        width: '420px',
        data: {
          title: this.transloco.translate('admin.users.confirmDelete.title'),
          message: this.transloco.translate('admin.users.confirmDelete.message', { name }),
          confirmLabel: this.transloco.translate('common.actions.delete'),
          warn: true,
        },
      })
      .afterClosed()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((confirmed: boolean | undefined) => {
        if (!confirmed) return;

        this.userService.deleteUser(user.id).subscribe({
          next: () => {
            this.users.update((list) => list.filter((u) => u.id !== user.id));
            this.snackBar.open(
              this.transloco.translate('admin.users.deletedNamed', { name }),
              this.transloco.translate('common.actions.close'),
              { duration: 3000 },
            );
          },
          error: () =>
            this.snackBar.open(
              this.transloco.translate('admin.users.deleteFailed'),
              this.transloco.translate('common.actions.close'),
              { duration: 5000 },
            ),
        });
      });
  }
}
