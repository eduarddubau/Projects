import { Component, ChangeDetectionStrategy, effect, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatTooltipModule } from '@angular/material/tooltip';
import { TranslocoPipe } from '@jsverse/transloco';
import { AuthService } from '@core/services/auth.service';
import { WorkspaceContextService } from '@core/services/workspace-context.service';

@Component({
  selector: 'app-workspace-switcher',
  imports: [MatButtonModule, MatIconModule, MatMenuModule, MatTooltipModule, TranslocoPipe],
  templateUrl: './workspace-switcher.component.html',
  styleUrl: './workspace-switcher.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class WorkspaceSwitcherComponent {
  private auth = inject(AuthService);
  private context = inject(WorkspaceContextService);

  isAuthenticated = this.auth.isAuthenticated;
  workspaces = this.context.workspaces;
  currentWorkspace = this.context.currentWorkspace;

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
        error: () => {},
      });
    });
  }

  select(id: string): void {
    this.context.setCurrent(id);
  }
}
