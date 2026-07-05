import { Component, inject, OnInit, PLATFORM_ID, ChangeDetectionStrategy } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { TranslocoDirective } from '@jsverse/transloco';
import { AuthService } from '@core/services/auth.service';
import { APP_NAME } from '@core/tokens/app.tokens';

interface Feature {
  icon: string;
  key: string;
}

@Component({
  selector: 'app-home',
  imports: [RouterLink, MatButtonModule, MatIconModule, TranslocoDirective],
  templateUrl: './home.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './home.component.scss',
})
export class HomeComponent implements OnInit {
  private authService = inject(AuthService);
  private router = inject(Router);
  private platformId = inject(PLATFORM_ID);

  appName = APP_NAME;
  currentYear = new Date().getFullYear();

  features: Feature[] = [
    { icon: 'folder_open', key: 'organize' },
    { icon: 'groups', key: 'teams' },
    { icon: 'restore_from_trash', key: 'nothingLost' },
    { icon: 'shield', key: 'secure' },
  ];

  ngOnInit(): void {
    // The landing is for visitors; signed-in users get the app home. Auth state
    // isn't known during SSR, so this resolves client-side after hydration.
    if (isPlatformBrowser(this.platformId) && this.authService.isAuthenticated()) {
      this.router.navigateByUrl('/dashboard');
    }
  }
}
