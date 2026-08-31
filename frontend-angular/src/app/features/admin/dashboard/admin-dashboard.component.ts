import { Component, computed, inject, ChangeDetectionStrategy } from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTooltipModule } from '@angular/material/tooltip';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { AdminDashboard } from '@core/models/admin-dashboard';
import { AppConfigService } from '@core/services/app-config.service';
import { AuthService } from '@core/services/auth.service';
import { DashboardService } from '@core/services/dashboard.service';
import { LanguageService } from '@core/services/language.service';
import { APP_NAME } from '@core/tokens/app.tokens';
import { fullNameOf, initialsOf } from '@core/utils/person';
import { pluralKey } from '@core/utils/plural';
import { WeatherWidgetComponent } from '@shared/weather-widget/weather-widget.component';
import { createGreeting } from '@shared/greeting/greeting';

/** One line of small print: a count, and where its rows are if that page exists. */
interface ContextItem {
  /** Suffix under `admin.dashboard.context.*`. */
  key: string;
  count: number;
  link: string | null;
}

/** One open decision, with somewhere to go and act on it. */
interface AttentionItem {
  /** Suffix under `admin.dashboard.attention.*`. */
  key: string;
  count: number;
  icon: string;
  link: string;
}

/**
 * The platform admin's home: what the instance holds, what is waiting on a decision, and
 * who signed up last.
 *
 * Projects and tasks appear only as counts — the platform admin acts on accounts and on
 * data lifecycle, never on a workspace's content.
 */
@Component({
  selector: 'app-admin-dashboard',
  templateUrl: './admin-dashboard.component.html',
  styleUrl: './admin-dashboard.component.scss',
  imports: [
    DatePipe,
    RouterLink,
    MatIconModule,
    MatProgressSpinnerModule,
    MatTooltipModule,
    WeatherWidgetComponent,
    TranslocoDirective,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AdminDashboardComponent {
  private dashboardService = inject(DashboardService);
  private authService = inject(AuthService);
  private languageService = inject(LanguageService);
  private appConfig = inject(AppConfigService);
  private transloco = inject(TranslocoService);

  appName = APP_NAME;

  stats = this.dashboardService.adminDashboard();

  greeting = createGreeting();

  initials = this.authService.initials;
  displayName = this.authService.displayName;
  dateLocale = this.languageService.dateLocale;
  trashWindow = this.appConfig.trashWindow;

  constructor() {
    this.appConfig.reloadIfFailed();
  }

  // hasValue(), not value(): a resource in an error state throws when read.
  private counts = computed(() => (this.stats.hasValue() ? this.stats.value() : null));

  /**
   * The open decisions, strongest first, with the settled ones dropped.
   *
   * Locked-out accounts are deliberately not here: every card promises a verb, and
   * /admin/users has no lockout column and no unlock to send anyone to.
   */
  attention = computed<AttentionItem[]>(() => {
    const s = this.counts();
    if (!s) return [];

    return [
      {
        key: 'purgeableProjects',
        count: s.purgeableProjectCount,
        icon: 'delete_forever',
        link: '/admin/trash/projects',
      },
      {
        key: 'deletedUsers',
        count: s.deletedUserCount,
        icon: 'person_off',
        link: '/admin/trash/users',
      },
    ].filter((item) => item.count > 0);
  });

  /** The lifecycle numbers with no decision attached, each linking where its rows are. */
  context = computed<ContextItem[]>(() => {
    const s = this.counts();
    if (!s) return [];

    return [
      {
        key: 'deletedProjects',
        count: s.deletedProjectCount,
        link: '/admin/trash/projects',
      },
      {
        key: 'deletedWorkspaces',
        count: s.deletedWorkspaceCount,
        link: '/admin/trash/workspaces',
      },
      // No link: there is no unlock surface to send anyone to. See attention() above.
      { key: 'lockedOut', count: s.lockedOutUserCount, link: null },
    ].filter((item) => item.count > 0);
  });

  /** A metric's label, agreeing with its own number. */
  countLabel(metric: string, count: number): string {
    return this.key(`admin.dashboard.stats.${metric}`, count);
  }

  /** Same shape as countLabel, for the small print. */
  contextLabel(item: ContextItem): string {
    return this.key(`admin.dashboard.context.${item.key}`, item.count);
  }

  /** An attention card's heading, which is the noun its count agrees with. */
  attentionTitle(item: AttentionItem): string {
    return this.key(`admin.dashboard.attention.${item.key}.title`, item.count);
  }

  private key(base: string, count: number): string {
    return pluralKey(base, count, this.languageService.dateLocale());
  }

  /**
   * Carried by both the tooltip and the visually-hidden text, so they cannot drift apart.
   *
   * Inflected on the window, not the signup count: "days" is the sentence's only noun, and
   * Romanian needs "de zile" above 19.
   */
  newUsersLabel(stats: AdminDashboard): string {
    return this.transloco.translate(
      this.key('admin.dashboard.stats.newUsers', stats.newUserWindowDays),
      { count: stats.newUserCount, days: stats.newUserWindowDays },
    );
  }

  fullName = fullNameOf;
  signupInitials = initialsOf;
}
