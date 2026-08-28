import { Component, ChangeDetectionStrategy, inject } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatDividerModule } from '@angular/material/divider';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { TranslocoPipe } from '@jsverse/transloco';
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
    TranslocoPipe,
  ],
  templateUrl: './workspace-switcher.component.html',
  styleUrl: './workspace-switcher.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class WorkspaceSwitcherComponent {
  private context = inject(WorkspaceContextService);
  private router = inject(Router);

  workspaces = this.context.workspaces;
  currentWorkspace = this.context.currentWorkspace;

  select(id: string): void {
    this.context.setCurrent(id);

    // Under /w/:workspaceId the page acts on the id in the URL, so the
    // selection has to move it too. Null elsewhere, where there is none.
    const tree = withWorkspaceId(this.router, this.router.url, id);
    if (tree) this.router.navigateByUrl(tree);
  }
}
