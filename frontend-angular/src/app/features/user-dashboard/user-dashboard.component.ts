import {
  Component, computed, inject, signal, OnInit,
  ChangeDetectionStrategy, ChangeDetectorRef, DestroyRef
} from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { DatePipe } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTableModule } from '@angular/material/table';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { TranslocoDirective } from '@jsverse/transloco';
import { DashboardService } from '@core/services/dashboard.service';
import { AuthService } from '@core/services/auth.service';
import { LanguageService } from '@core/services/language.service';
import { UserDashboard } from '@core/models/user-dashboard';
import { Project } from '@core/models/project';
import { AuroraComponent } from '@shared/aurora/aurora.component';

@Component({
  selector: 'app-user-dashboard',
  templateUrl: './user-dashboard.component.html',
  styleUrl: './user-dashboard.component.scss',
  imports: [
    RouterLink,
    DatePipe,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatTableModule,
    AuroraComponent,
    TranslocoDirective
  ],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class UserDashboardComponent implements OnInit {
  private dashboardService = inject(DashboardService);
  private authService = inject(AuthService);
  private router = inject(Router);
  private cdr = inject(ChangeDetectorRef);
  private destroyRef = inject(DestroyRef);
  private languageService = inject(LanguageService);

  /** Locale for the date pipes; 'ro' locale data is registered in provideI18n. */
  dateLocale = computed(() => this.languageService.lang() === 'ro' ? 'ro' : 'en-US');

  isLoading = signal(true);
  hasError = signal(false);
  dashboard = signal<UserDashboard | null>(null);

  currentUser = this.authService.currentUser;
  isAdmin = this.authService.isAdmin;

  initials = computed(() => {
    const user = this.currentUser();
    if (!user) return '';
    return `${user.firstName?.[0] ?? ''}${user.lastName?.[0] ?? ''}`.toUpperCase();
  });

  // Greeting keyed to local time of day; resolved once per page load.
  greetingKey = computed(() => {
    const hour = new Date().getHours();
    if (hour < 12) return 'userDashboard.greeting.morning';
    if (hour < 18) return 'userDashboard.greeting.afternoon';
    return 'userDashboard.greeting.evening';
  });

  recentColumns = ['name', 'lastActivity'];

  ngOnInit(): void {
    this.dashboardService.getMyDashboard()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (dashboard) => {
          this.dashboard.set(dashboard);
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

  openProject(project: Project): void {
    this.router.navigate(['/projects', project.id]);
  }

  lastActivity(project: Project): string {
    return project.updatedAt ?? project.createdAt;
  }
}
