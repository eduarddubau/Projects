import { Component, inject, ChangeDetectionStrategy } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { AuthService } from '@core/services/auth.service';
import { APP_NAME } from '@core/tokens/app.tokens';

interface Feature {
  icon: string;
  title: string;
  description: string;
}

@Component({
  selector: 'app-home',
  imports: [RouterLink, MatButtonModule, MatIconModule],
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
    {
      icon: 'folder_open',
      title: 'Organize your work',
      description:
        'Create and manage projects in one place, with full ownership and audit history baked in.',
    },
    {
      icon: 'groups',
      title: 'Built for teams',
      description:
        'Admins manage, members build. Role-based access makes sure everyone sees exactly what they should.',
    },
    {
      icon: 'restore_from_trash',
      title: 'Nothing lost',
      description:
        'Deleted a project by mistake? Bring it back from Trash with one click — no panic, no data loss.',
    },
    {
      icon: 'shield',
      title: 'Secure by design',
      description:
        'JWT authentication and GDPR-compliant erasure keep your account — and your users — protected.',
    },
  ];
}
