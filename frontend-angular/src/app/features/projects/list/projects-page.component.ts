import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { toSignal } from '@angular/core/rxjs-interop';
import { map } from 'rxjs/operators';
import { TranslocoDirective } from '@jsverse/transloco';
import { AuroraComponent } from '@shared/aurora/aurora.component';
import { WorkspaceScopeComponent } from '@shared/workspace-scope/workspace-scope.component';
import { ProjectsCardComponent } from './projects-card.component';

/** Page chrome around the projects table, which owns its own toolbar and states. */
@Component({
  selector: 'app-projects-page',
  templateUrl: './projects-page.component.html',
  imports: [AuroraComponent, WorkspaceScopeComponent, ProjectsCardComponent, TranslocoDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProjectsPageComponent {
  private route = inject(ActivatedRoute);

  workspaceId = toSignal(this.route.paramMap.pipe(map((p) => p.get('workspaceId'))), {
    initialValue: this.route.snapshot.paramMap.get('workspaceId'),
  });
}
