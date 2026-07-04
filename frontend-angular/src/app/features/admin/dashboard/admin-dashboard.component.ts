import { Component, inject, signal, ChangeDetectionStrategy, OnInit, ChangeDetectorRef } from '@angular/core';
import { RouterLink } from '@angular/router';
import { DatePipe } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTableModule } from '@angular/material/table';
import { MatDividerModule } from '@angular/material/divider';
import { TranslocoDirective } from '@jsverse/transloco';
import { DashboardService } from '@core/services/dashboard.service';
import { AdminDashboard } from '@core/models/admin-dashboard';

@Component({
  selector: 'app-admin-dashboard',
  templateUrl: './admin-dashboard.component.html',
  styleUrl: './admin-dashboard.component.scss',
  imports: [
    RouterLink,
    DatePipe,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatTableModule,
    MatDividerModule,
    TranslocoDirective
  ],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class AdminDashboardComponent implements OnInit {
  private dashboardService = inject(DashboardService);
  private cdr = inject(ChangeDetectorRef);

  isLoading = signal(true);
  hasError = signal(false);
  stats = signal<AdminDashboard | null>(null);

  recentProjectColumns = ['name', 'createdBy', 'createdAt'];
  recentUserColumns = ['name', 'email', 'createdAt'];

  ngOnInit() {
    this.dashboardService.getAdminDashboard().subscribe({
      next: (data) => {
        this.stats.set(data);
        this.isLoading.set(false);
        this.cdr.markForCheck();
      },
      error: () => {
        this.hasError.set(true);
        this.isLoading.set(false);
        this.cdr.markForCheck();
      }
    });
  }
}