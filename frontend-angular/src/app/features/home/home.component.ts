import { Component, inject, ChangeDetectionStrategy } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';
import { AuthService } from '@core/services/auth.service';

interface Feature {
  icon: string;
  title: string;
  description: string;
}

@Component({
  selector: 'app-home',
  imports: [RouterLink, MatButtonModule, MatIconModule, MatCardModule],
  templateUrl: './home.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './home.component.scss',
})
export class HomeComponent {
  private authService = inject(AuthService);

  /** Single place to rename the product. */
  appName = 'Projects';

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
        'Role-based access keeps members and admins in their lanes, so everyone sees exactly what they should.',
    },
    {
      icon: 'restore_from_trash',
      title: 'Nothing lost',
      description: 'Deleted a project by mistake? Restore it from Trash — no panic, no data loss.',
    },
    {
      icon: 'shield',
      title: 'Secure by design',
      description:
        'JWT authentication and GDPR-friendly anonymization protect your account and your users.',
    },
  ];
}
