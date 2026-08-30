import {
  Component,
  computed,
  inject,
  signal,
  ChangeDetectionStrategy,
  ChangeDetectorRef,
} from '@angular/core';
import { DatePipe } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { EMPTY } from 'rxjs';
import { catchError, map } from 'rxjs/operators';
import { TranslocoDirective } from '@jsverse/transloco';
import { DashboardService } from '@core/services/dashboard.service';
import { ProjectService } from '@core/services/project.service';
import { TaskFilter, TaskService } from '@core/services/task.service';
import { LanguageService } from '@core/services/language.service';
import { TodayService } from '@core/services/today.service';
import { AuthService } from '@core/services/auth.service';
import { WorkspaceContextService } from '@core/services/workspace-context.service';
import { CurrentWeather } from '@core/models/weather';
import { pluralCategory } from '@core/utils/plural';
import { AuroraComponent } from '@shared/aurora/aurora.component';
import { WeatherWidgetComponent } from '@shared/weather-widget/weather-widget.component';
import { TaskRowsComponent } from '@shared/task-rows/task-rows.component';

/**
 * The home of one workspace: who you are, how that workspace is doing, and its projects.
 *
 * Everything on it is scoped to the workspace in the path — the switcher in the header
 * governs the whole page, and no number here counts a workspace you are looking away
 * from. The account-level view across workspaces is /workspaces, which lists workspaces
 * rather than merging their contents.
 */
@Component({
  selector: 'app-workspace-home',
  templateUrl: './workspace-home.component.html',
  styleUrl: './workspace-home.component.scss',
  imports: [
    DatePipe,
    RouterLink,
    MatIconModule,
    MatProgressSpinnerModule,
    AuroraComponent,
    WeatherWidgetComponent,
    TaskRowsComponent,
    TranslocoDirective,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class WorkspaceHomeComponent {
  private dashboardService = inject(DashboardService);
  private projectService = inject(ProjectService);
  private taskService = inject(TaskService);
  private languageService = inject(LanguageService);
  private todayService = inject(TodayService);
  private authService = inject(AuthService);
  private cdr = inject(ChangeDetectorRef);
  private route = inject(ActivatedRoute);
  private workspaceContext = inject(WorkspaceContextService);

  // From paramMap, not the snapshot: switching workspace navigates between two
  // instances of this same route, and Angular reuses the component.
  workspaceId = toSignal(this.route.paramMap.pipe(map((p) => p.get('workspaceId'))), {
    initialValue: this.route.snapshot.paramMap.get('workspaceId'),
  });

  // The workspace in the path, not the selected one: the two agree in practice, but the
  // path is what the rest of this page reads.
  workspace = computed(
    () => this.workspaceContext.workspaces().find((w) => w.id === this.workspaceId()) ?? null,
  );

  dashboard = this.dashboardService.workspaceDashboard(this.workspaceId);

  constructor() {
    // The workspace store loads once per session, so a member added or a name edited
    // elsewhere would otherwise show stale here for as long as the tab lives.
    // Swallowed: the store keeps whatever it already had, and the page reads that.
    this.workspaceContext
      .refresh()
      .pipe(
        catchError(() => EMPTY),
        takeUntilDestroyed(),
      )
      .subscribe();
  }

  /**
   * What the page loaded, falling back to what the store remembers.
   *
   * hasValue(), not value(): a resource in an error state throws when read, and this tile
   * renders outside the error branch that guards the project grid below it.
   */
  projectCount = computed(() =>
    this.projects.hasValue() ? this.projects.value().length : (this.workspace()?.projectCount ?? 0),
  );

  // Each metric's label agrees with its own number — Transloco has no ICU plurals here,
  // so the count picks the key.
  memberLabel = computed(() => this.countLabel('members', this.workspace()?.memberCount ?? 0));
  projectLabel = computed(() => this.countLabel('projects', this.projectCount()));
  openTaskLabel = computed(() =>
    this.countLabel(
      'openTasks',
      this.dashboard.hasValue() ? this.dashboard.value().openTaskCount : 0,
    ),
  );

  private countLabel(metric: string, count: number): string {
    return `workspaceHome.stats.${metric}.${pluralCategory(count, this.languageService.dateLocale())}`;
  }

  // Home is the personal digest, so this half is always "assigned to me"; the Tasks
  // page is where the filter is a control.
  private myWork = signal<TaskFilter>('mine');
  tasks = this.taskService.workspaceTasks(this.workspaceId, this.myWork);
  projects = this.projectService.workspaceProjects(this.workspaceId);

  // The API already returns soonest-due first with the undated last, so the head of
  // the list is the most urgent work without re-sorting it here.
  myTasks = computed(() => this.tasks.value().slice(0, 5));

  recentProjects = computed(() =>
    [...this.projects.value()]
      .sort((a, b) => (b.updatedAt ?? b.createdAt).localeCompare(a.updatedAt ?? a.createdAt))
      .slice(0, 4),
  );

  dateLocale = this.languageService.dateLocale;

  currentUser = this.authService.currentUser;
  displayName = this.authService.displayName;

  initials = computed(() => {
    const user = this.currentUser();
    if (!user) return '';
    return `${user.firstName?.[0] ?? ''}${user.lastName?.[0] ?? ''}`.toUpperCase();
  });

  weather = signal<CurrentWeather | null>(null);

  // Local part of day; drives the greeting and the fallback tagline. Depends on the day so
  // it re-reads at midnight — without that the greeting still said "evening" while the task
  // bands below it had already moved to the new day. It does not re-read at noon or six:
  // that would need an hourly tick, and a greeting is not worth one.
  private partOfDay = computed(() => {
    this.todayService.today();
    const hour = new Date().getHours();
    if (hour < 12) return 'morning';
    if (hour < 18) return 'afternoon';
    return 'evening';
  });

  greetingKey = computed(() => `workspaceHome.greeting.${this.partOfDay()}`);

  // A weather-mood line once the widget reports in, else a time-of-day one.
  taglineKey = computed(() => {
    const weather = this.weather();
    return weather && weather.conditionKey !== 'unknown'
      ? `workspaceHome.taglines.weather.${weatherMood(weather)}`
      : `workspaceHome.taglines.${this.partOfDay()}`;
  });

  onWeatherLoaded(weather: CurrentWeather): void {
    this.weather.set(weather);
    this.cdr.markForCheck();
  }
}

// Coarse mood bucket the tagline copy keys off; clear splits by day/night.
function weatherMood(weather: CurrentWeather): string {
  switch (weather.conditionKey) {
    case 'clear':
    case 'mostlyClear':
      return weather.isDay ? 'clear' : 'clearNight';
    case 'fog':
      return 'fog';
    case 'drizzle':
    case 'freezingDrizzle':
    case 'rain':
    case 'freezingRain':
    case 'showers':
      return 'rain';
    case 'snow':
    case 'snowShowers':
      return 'snow';
    case 'thunderstorm':
    case 'thunderstormHail':
      return 'storm';
    default:
      return 'cloudy';
  }
}
