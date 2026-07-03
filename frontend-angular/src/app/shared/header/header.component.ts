import { Component, computed, inject, ChangeDetectionStrategy } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatMenuModule } from '@angular/material/menu';
import { MatDividerModule } from '@angular/material/divider';
import { AuthService } from '@core/services/auth.service';
import { HealthService } from '@core/services/health.service';
import { ThemeService } from '@core/services/theme.service';
import { HealthStatus } from '@core/models/health-status';
import { APP_NAME } from '@core/tokens/app.tokens';
import { toSignal } from '@angular/core/rxjs-interop';

const THEME_ICONS = { system: 'brightness_auto', light: 'light_mode', dark: 'dark_mode' } as const;
const THEME_LABELS = { system: 'System', light: 'Light', dark: 'Dark' } as const;

@Component({
  selector: 'app-header',
  imports: [
    RouterLink,
    RouterLinkActive,
    MatButtonModule,
    MatIconModule,
    MatTooltipModule,
    MatMenuModule,
    MatDividerModule,
  ],
  templateUrl: './header.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrls: ['./header.component.scss'],
})
export class HeaderComponent {
  private authService = inject(AuthService);
  private healthService = inject(HealthService);
  private themeService = inject(ThemeService);

  appName = APP_NAME;
  currentUser = this.authService.currentUser;
  isAuthenticated = this.authService.isAuthenticated;
  isAdmin = this.authService.isAdmin;
  health = toSignal(this.healthService.status$, {
    initialValue: { state: 'offline', error: 'Initializing...' } as HealthStatus,
  });

  initials = computed(() => {
    const user = this.currentUser();
    if (!user) return '';
    return `${user.firstName?.[0] ?? ''}${user.lastName?.[0] ?? ''}`.toUpperCase();
  });

  themeIcon = computed(() => THEME_ICONS[this.themeService.preference()]);
  themeTooltip = computed(() => `Theme: ${THEME_LABELS[this.themeService.preference()]}`);

  cycleTheme(): void {
    this.themeService.cycle();
  }

  logout(): void {
    this.authService.logout();
  }
}
