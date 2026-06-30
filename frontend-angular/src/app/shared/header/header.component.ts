import { Component, inject, ChangeDetectionStrategy } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatMenuModule } from '@angular/material/menu';
import { MatDividerModule } from '@angular/material/divider';
import { AuthService } from '@core/services/auth.service';
import { HealthService } from '@core/services/health.service';
import { HealthStatus } from '@core/models/health-status';
import { toSignal } from '@angular/core/rxjs-interop';

@Component({
  selector: 'app-header',
  imports: [
    RouterLink,
    RouterLinkActive,
    MatToolbarModule,
    MatButtonModule,
    MatIconModule,
    MatTooltipModule,
    MatMenuModule,
    MatDividerModule,
  ],
  templateUrl: './header.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrls: ['./header.component.scss'],
})
export class HeaderComponent {
  private authService = inject(AuthService);
  private healthService = inject(HealthService);

  currentUser = this.authService.currentUser;
  isAuthenticated = this.authService.isAuthenticated;
  isAdmin = this.authService.isAdmin; // add this
  health = toSignal(this.healthService.status$, {
    initialValue: { state: 'offline', error: 'Initializing...' } as HealthStatus,
  });

  logout(): void {
    this.authService.logout();
  }
}
