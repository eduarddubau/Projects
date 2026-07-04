import { Component, inject, ChangeDetectionStrategy } from '@angular/core';
import { RouterLink } from '@angular/router';
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
export class HomeComponent {
  private authService = inject(AuthService);

  appName = APP_NAME;
  currentYear = new Date().getFullYear();

  isAuthenticated = this.authService.isAuthenticated;
  currentUser = this.authService.currentUser;

  features: Feature[] = [
    { icon: 'folder_open', key: 'organize' },
    { icon: 'groups', key: 'teams' },
    { icon: 'restore_from_trash', key: 'nothingLost' },
    { icon: 'shield', key: 'secure' },
  ];
}
