import {
  Component,
  computed,
  inject,
  signal,
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  DestroyRef,
} from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatDialog } from '@angular/material/dialog';
import { MatMenuModule } from '@angular/material/menu';
import { MatSnackBar } from '@angular/material/snack-bar';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { map } from 'rxjs';
import { TranslocoDirective, TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { serverErrorKey } from '@core/i18n/server-error-keys';
import { ProjectService } from '@core/services/project.service';
import { TaskService } from '@core/services/task.service';
import { WorkspaceService } from '@core/services/workspace.service';
import { WorkspaceContextService } from '@core/services/workspace-context.service';
import { Project } from '@core/models/project';
import { Workspace } from '@core/models/workspace';
import { sortTasks } from '@core/models/task';
import { ConfirmDialogComponent } from '@shared/confirm-dialog/confirm-dialog.component';
import { AuroraComponent } from '@shared/aurora/aurora.component';
import {
  ProjectFormDialogComponent,
  ProjectFormResult,
} from '../project-form-dialog/project-form-dialog.component';
import {
  TaskFormDialogComponent,
  TaskFormResult,
} from '@features/tasks/task-form-dialog/task-form-dialog.component';
import { TaskListComponent } from '@features/tasks/task-list/task-list.component';
import { TaskBoardComponent } from '@features/tasks/board/task-board.component';

export type TaskView = 'board' | 'list';

@Component({
  selector: 'app-project-detail',
  templateUrl: './project-detail.component.html',
  styleUrl: './project-detail.component.scss',
  imports: [
    RouterLink,
    MatButtonModule,
    MatButtonToggleModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatMenuModule,
    AuroraComponent,
    TaskListComponent,
    TaskBoardComponent,
    TranslocoDirective,
    TranslocoPipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProjectDetailComponent {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private projectService = inject(ProjectService);
  private taskService = inject(TaskService);
  private workspaceService = inject(WorkspaceService);
  private workspaceContext = inject(WorkspaceContextService);
  private dialog = inject(MatDialog);
  private snackBar = inject(MatSnackBar);
  private cdr = inject(ChangeDetectorRef);
  private destroyRef = inject(DestroyRef);
  private transloco = inject(TranslocoService);

  // Route params are read once here rather than watched: this route is only
  // reachable per-id, so the component is rebuilt when the id changes.
  private projectId = signal(this.route.snapshot.paramMap.get('id') ?? undefined);
  private routeWorkspaceId = this.route.snapshot.paramMap.get('workspaceId');

  project = this.projectService.project(this.projectId);

  /** Owned here so the list and the board share one fetch. */
  tasks = this.taskService.projectTasks(this.projectId);

  // The project's own workspace, not the route's: moving a project reuses this
  // component, which would leave the back link and the delete redirect pointing
  // at the workspace it came from.
  workspaceId = computed(() =>
    this.project.hasValue() ? this.project.value().workspaceId : this.routeWorkspaceId,
  );

  private viewParam = toSignal(this.route.queryParamMap.pipe(map((params) => params.get('view'))), {
    initialValue: this.route.snapshot.queryParamMap.get('view'),
  });

  // A pure function of the URL and nothing else, so the server and the client's first
  // render agree and there is no hydration swap to design around.
  view = computed<TaskView>(() => (this.viewParam() === 'list' ? 'list' : 'board'));

  private membersResource = this.workspaceService.membersResource(
    computed(() => (this.project.hasValue() ? this.project.value().workspaceId : undefined)),
  );
  members = computed(() => (this.membersResource.hasValue() ? this.membersResource.value() : []));

  // Read off the project, not the URL: this route resolves a project by id alone,
  // so the workspace in the path is a display detail and can name a different one.
  isOwner = computed(() => {
    if (!this.project.hasValue()) return false;
    const holder = this.project.value().workspaceId;
    return this.workspaceContext.workspaces().find((w) => w.id === holder)?.myRole === 'Owner';
  });

  // Unfiltered by role on purpose: moving a project in only needs membership of
  // the destination. Ownership is required of the source, which isOwner covers.
  availableWorkspaces = computed<Workspace[]>(() => {
    if (!this.project.hasValue()) return [];
    const holder = this.project.value().workspaceId;
    return this.workspaceContext.workspaces().filter((w) => w.id !== holder);
  });

  setView(view: TaskView): void {
    // replaceUrl so Back leaves the project rather than walking your view changes.
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { view },
      queryParamsHandling: 'merge',
      replaceUrl: true,
    });
  }

  openProjectEdit(project: Project): void {
    this.dialog
      .open(ProjectFormDialogComponent, { width: '480px', data: { project } })
      .afterClosed()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((result: ProjectFormResult | undefined) => {
        if (!result) return;

        this.projectService.updateProject(project.id, result).subscribe({
          next: (updated) => {
            this.project.set(updated);
            this.cdr.markForCheck();
            this.notify('projects.notifications.updated');
          },
          error: (err) =>
            this.notify(serverErrorKey(err, 'projects.notifications.updateFailed'), 5000),
        });
      });
  }

  newTask(): void {
    const projectId = this.projectId();
    if (!projectId) return;

    this.dialog
      .open(TaskFormDialogComponent, { width: '560px', data: { members: this.members } })
      .afterClosed()
      .pipe(takeUntilDestroyed(this.destroyRef))
      // Only 'save' can arrive: the dialog offers no delete for a task that does not exist yet.
      .subscribe((result: TaskFormResult | undefined) => {
        if (result?.action !== 'save') return;

        this.taskService.createTask(projectId, result.payload).subscribe({
          next: (created) => {
            // value() throws while the resource is in its error state, and this button
            // is reachable there; refetch instead of splicing into a list we can't read.
            if (this.tasks.hasValue()) {
              this.tasks.update((list) => sortTasks([...list, created]));
            } else {
              this.tasks.reload();
            }
            this.cdr.markForCheck();
            this.notify('tasks.notifications.created');
          },
          error: (err) =>
            this.notify(serverErrorKey(err, 'tasks.notifications.createFailed'), 5000),
        });
      });
  }

  confirmDelete(project: Project): void {
    this.dialog
      .open(ConfirmDialogComponent, {
        width: '400px',
        data: {
          title: this.transloco.translate('projects.confirmDelete.title'),
          message: this.transloco.translate('projects.confirmDelete.message', {
            name: project.name,
          }),
          confirmLabel: this.transloco.translate('common.actions.delete'),
          warn: true,
        },
      })
      .afterClosed()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((confirmed: boolean | undefined) => {
        if (!confirmed) return;

        this.projectService.deleteProject(project.id).subscribe({
          next: () => {
            this.notify('projects.notifications.deleted');
            this.router.navigate(['/w', this.workspaceId()]);
          },
          error: () => this.notify('projects.notifications.deleteFailed', 5000),
        });
      });
  }

  moveTo(project: Project, target: Workspace): void {
    this.projectService
      .moveProject(project.id, target.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: ({ project: moved, unassignedTaskCount }) => {
          this.project.set(moved);

          // The server dropped assignees the target workspace has no member for.
          if (unassignedTaskCount > 0) this.tasks.reload();

          this.cdr.markForCheck();
          this.snackBar.open(
            this.movedMessage(moved.workspaceName, unassignedTaskCount),
            this.transloco.translate('common.actions.close'),
            { duration: unassignedTaskCount > 0 ? 5000 : 3000 },
          );

          // Keep the URL honest: the path still names the workspace it came from.
          this.router.navigate(['/w', moved.workspaceId, 'projects', moved.id], {
            replaceUrl: true,
          });
        },
        error: (err) => this.notify(serverErrorKey(err, 'projects.notifications.moveFailed'), 5000),
      });
  }

  private movedMessage(workspaceName: string, unassignedTaskCount: number): string {
    if (unassignedTaskCount === 0)
      return this.transloco.translate('projects.notifications.moved', { name: workspaceName });

    return this.transloco.translate(
      unassignedTaskCount === 1
        ? 'projects.notifications.movedAndUnassignedOne'
        : 'projects.notifications.movedAndUnassignedMany',
      { name: workspaceName, count: unassignedTaskCount },
    );
  }

  private notify(key: string, duration = 3000): void {
    this.snackBar.open(
      this.transloco.translate(key),
      this.transloco.translate('common.actions.close'),
      { duration },
    );
  }
}
