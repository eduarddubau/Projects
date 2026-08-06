import { Component, inject, ChangeDetectionStrategy } from '@angular/core';
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
export class AdminDashboardComponent {
  private dashboardService = inject(DashboardService);

  stats = this.dashboardService.adminDashboard();

  recentProjectColumns = ['name', 'createdBy', 'createdAt'];
  recentUserColumns = ['name', 'email', 'createdAt'];

}