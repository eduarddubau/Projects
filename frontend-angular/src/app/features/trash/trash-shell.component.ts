import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { MatTabsModule } from '@angular/material/tabs';
import { TranslocoDirective } from '@jsverse/transloco';
import { AppConfigService } from '@core/services/app-config.service';
import { WorkspaceContextService } from '@core/services/workspace-context.service';
import { AuroraComponent } from '@shared/aurora/aurora.component';
import { WorkspaceScopeComponent } from '@shared/workspace-scope/workspace-scope.component';

/**
 * One destination for everything a workspace has thrown away.
 *
 * The two tabs answer to different roles, which is why the page itself is no longer
 * owner-guarded: any member may delete a task and so must be able to get it back, while
 * only an owner can delete a project and so only an owner has a project trash to read.
 * The Projects route keeps the guard; hiding the tab is presentation, not authorization.
 */
@Component({
  selector: 'app-trash-shell',
  templateUrl: './trash-shell.component.html',
  styleUrl: './trash-shell.component.scss',
  imports: [
    RouterLink,
    RouterLinkActive,
    RouterOutlet,
    MatTabsModule,
    AuroraComponent,
    WorkspaceScopeComponent,
    TranslocoDirective,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TrashShellComponent {
  private appConfig = inject(AppConfigService);

  isOwner = inject(WorkspaceContextService).isOwner;
  trashWindow = this.appConfig.trashWindow;

  constructor() {
    this.appConfig.reloadIfFailed();
  }
}
