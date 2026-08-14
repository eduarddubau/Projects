import { Component, ChangeDetectionStrategy, effect, inject } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatDividerModule } from '@angular/material/divider';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatTooltipModule } from '@angular/material/tooltip';
import { TranslocoPipe } from '@jsverse/transloco';
import { AuthService } from '@core/services/auth.service';
import { WorkspaceContextService } from '@core/services/workspace-context.service';
import { withWorkspaceId } from '@core/utils/workspace-url';

@Component({
  selector: 'app-workspace-switcher',
  imports: [
    RouterLink,
    MatButtonModule,
    MatDividerModule,
    MatIconModule,
    MatMenuModule,
    MatTooltipModule,
    TranslocoPipe,
  ],
  templateUrl: './workspace-switcher.component.html',
  styleUrl: './workspace-switcher.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class WorkspaceSwitcherComponent {
  private auth = inject(AuthService);
  private context = inject(WorkspaceContextService);
  private router = inject(Router);

  isAuthenticated = this.auth.isAuthenticated;
  workspaces = this.context.workspaces;
  currentWorkspace = this.context.currentWorkspace;
  canManage = this.context.canManageCurrent;

  constructor() {
    // An effect, not a constructor call: the header is built once at app start,
    // so a signed-out user would never load the list. This re-runs when auth
    // flips, and ensureLoaded's cache makes the repeat calls free.
    effect(() => {
      if (!this.isAuthenticated()) return;

      this.context.ensureLoaded().subscribe({
        next: () => {
          const target = this.context.resolve(null);
          if (target) this.context.setCurrent(target);
        },
        error: () => {
          /* Nothing to switch between; the guard reports a real load failure. */
        },
      });
    });
  }

  select(id: string): void {
    this.context.setCurrent(id);

    // Under /w/:workspaceId the page acts on the id in the URL, so the
    // selection has to move it too. Null elsewhere, where there is none.
    const tree = withWorkspaceId(this.router, this.router.url, id);
    if (tree) this.router.navigateByUrl(tree);
  }
}
