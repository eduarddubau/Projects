import { Component, ChangeDetectionStrategy, inject } from '@angular/core';
import { ActivatedRoute, RouterOutlet, RouterLink, RouterLinkActive } from '@angular/router';
import { MatSidenavModule, MatDrawer } from '@angular/material/sidenav';
import { MatListModule } from '@angular/material/list';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { BreakpointObserver, Breakpoints } from '@angular/cdk/layout';
import { toSignal } from '@angular/core/rxjs-interop';
import { map } from 'rxjs';
import { TranslocoDirective } from '@jsverse/transloco';
import { WorkspaceContextService } from '@core/services/workspace-context.service';
import { WorkspaceSwitcherComponent } from '@shared/workspace-switcher/workspace-switcher.component';

/**
 * Frame for everything under /w/:workspaceId: a sidebar carrying the switcher and the
 * workspace's own destinations, and the routed page beside it.
 */
@Component({
  selector: 'app-workspace-shell',
  imports: [
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    MatSidenavModule,
    MatListModule,
    MatIconModule,
    MatButtonModule,
    TranslocoDirective,
    WorkspaceSwitcherComponent,
  ],
  templateUrl: './workspace-shell.component.html',
  styleUrl: './workspace-shell.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class WorkspaceShellComponent {
  private breakpoints = inject(BreakpointObserver);
  private route = inject(ActivatedRoute);
  private context = inject(WorkspaceContextService);

  // From paramMap, not the snapshot: switching workspace navigates between two
  // instances of this same route, and Angular reuses the component.
  workspaceId = toSignal(this.route.paramMap.pipe(map((p) => p.get('workspaceId'))), {
    initialValue: this.route.snapshot.paramMap.get('workspaceId'),
  });

  canManage = this.context.canManageCurrent;

  isHandset = toSignal(
    this.breakpoints
      .observe([Breakpoints.Handset, '(max-width: 720px)'])
      .pipe(map((result) => result.matches)),
    { initialValue: false },
  );

  /** On mobile the drawer overlays content, so close it after navigating. */
  closeIfHandset(drawer: MatDrawer): void {
    if (this.isHandset()) {
      drawer.close();
    }
  }
}
