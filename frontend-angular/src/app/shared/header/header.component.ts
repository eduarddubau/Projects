import { Component, computed, inject, ChangeDetectionStrategy } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatMenuModule } from '@angular/material/menu';
import { MatDividerModule } from '@angular/material/divider';
import { TranslocoDirective, TranslocoPipe } from '@jsverse/transloco';
import { AuthService } from '@core/services/auth.service';
import { HealthService } from '@core/services/health.service';
import { ThemeService } from '@core/services/theme.service';
import { HealthStatus } from '@core/models/health-status';
import { APP_NAME } from '@core/tokens/app.tokens';
import { LanguageSwitcherComponent } from '@shared/language-switcher/language-switcher.component';
import { toSignal } from '@angular/core/rxjs-interop';

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
    TranslocoDirective,
    TranslocoPipe,
    LanguageSwitcherComponent,
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
    initialValue: { state: 'offline', errorKey: 'header.health.initializing' } as HealthStatus,
  });

  initials = computed(() => {
    const user = this.currentUser();
    if (!user) return '';
    return `${user.firstName?.[0] ?? ''}${user.lastName?.[0] ?? ''}`.toUpperCase();
  });

  theme = this.themeService.theme;

  /** Flips the theme, centering the reveal animation on the toggle. */
  toggleTheme(event: MouseEvent): void {
    const rect = (event.currentTarget as HTMLElement | null)?.getBoundingClientRect();
    const origin = rect
      ? { x: rect.left + rect.width / 2, y: rect.top + rect.height / 2 }
      : undefined;
    this.themeService.toggle(origin);
  }

  logout(): void {
    this.authService.logout();
  }
}
