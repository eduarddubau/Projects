import { Component, ChangeDetectionStrategy, computed, inject, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { TranslocoPipe } from '@jsverse/transloco';
import { WorkspaceContextService } from '@core/services/workspace-context.service';

/** Eyebrow naming the workspace a page belongs to; sits above its title. */
@Component({
  selector: 'app-workspace-scope',
  imports: [MatIconModule, TranslocoPipe],
  template: `
    @if (workspace(); as w) {
      <p class="page-eyebrow">
        <mat-icon aria-hidden="true">workspaces</mat-icon>
        <!-- The stored personal name is an English possessive; untranslatable. -->
        {{ w.isPersonal ? ('workspaces.personal' | transloco) : w.name }}
      </p>
    }
  `,
  styles: `
    :host {
      display: block;
    }

    .page-eyebrow {
      display: inline-flex;
      align-items: center;
      gap: 6px;
      margin: 0 0 6px;
      font-size: 0.72rem;
      font-weight: 600;
      letter-spacing: 0.08em;
      text-transform: uppercase;
      color: var(--mat-sys-on-surface-variant);

      mat-icon {
        width: 16px;
        height: 16px;
        font-size: 16px;
        color: var(--mat-sys-primary);
      }
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class WorkspaceScopeComponent {
  private context = inject(WorkspaceContextService);

  /**
   * Names this workspace instead of the selected one. Project detail resolves a
   * project by id alone, so the workspace in its path can name a different one.
   */
  workspaceId = input<string | null>(null);

  workspace = computed(() => {
    const id = this.workspaceId();
    if (!id) return this.context.currentWorkspace();
    return this.context.workspaces().find((w) => w.id === id) ?? null;
  });
}
