import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { toSignal } from '@angular/core/rxjs-interop';
import { map } from 'rxjs/operators';
import { TranslocoDirective } from '@jsverse/transloco';
import { TaskFilter, TaskService } from '@core/services/task.service';
import { WorkspaceTask } from '@core/models/task';
import { DUE_BUCKETS, DueBucket, dueBucket, todayIso } from '@core/utils/iso-date';
import { AuroraComponent } from '@shared/aurora/aurora.component';
import { TaskRowsComponent } from '@shared/task-rows/task-rows.component';
import { WorkspaceScopeComponent } from '@shared/workspace-scope/workspace-scope.component';

/** The chips, in the order they are shown. `mine` is the default and stays out of the URL. */
const TASK_FILTERS: readonly TaskFilter[] = ['mine', 'all', 'overdue', 'unassigned'];

interface TaskGroup {
  bucket: DueBucket;
  tasks: WorkspaceTask[];
}

/**
 * Every open task across one workspace's projects, banded by when it is due.
 *
 * No "new task" button: creating one needs a project and this page does not have one —
 * tasks are created on a board. The filter lives in the URL rather than in a signal, so
 * a filtered list is a link someone can send.
 */
@Component({
  selector: 'app-workspace-tasks',
  templateUrl: './workspace-tasks.component.html',
  styleUrl: './workspace-tasks.component.scss',
  imports: [
    MatButtonToggleModule,
    MatIconModule,
    MatProgressSpinnerModule,
    AuroraComponent,
    TaskRowsComponent,
    WorkspaceScopeComponent,
    TranslocoDirective,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class WorkspaceTasksComponent {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private taskService = inject(TaskService);

  // From the map, not the snapshot: switching workspace or filter navigates between two
  // instances of this same route, and Angular reuses the component.
  workspaceId = toSignal(this.route.paramMap.pipe(map((p) => p.get('workspaceId'))), {
    initialValue: this.route.snapshot.paramMap.get('workspaceId'),
  });

  filter = toSignal(this.route.queryParamMap.pipe(map((p) => asFilter(p.get('filter')))), {
    initialValue: asFilter(this.route.snapshot.queryParamMap.get('filter')),
  });

  tasks = this.taskService.workspaceTasks(this.workspaceId, this.filter);

  // Bands the server's order rather than re-sorting it: the response already arrives
  // soonest-due first with the undated last, so each band keeps that order for free.
  groups = computed<TaskGroup[]>(() => {
    const today = todayIso();
    const banded = new Map<DueBucket, WorkspaceTask[]>();

    for (const task of this.tasks.value()) {
      const bucket = dueBucket(task.dueDate, today);
      const group = banded.get(bucket);
      if (group) group.push(task);
      else banded.set(bucket, [task]);
    }

    return DUE_BUCKETS.filter((bucket) => banded.has(bucket)).map((bucket) => ({
      bucket,
      tasks: banded.get(bucket)!,
    }));
  });

  filters = TASK_FILTERS;

  setFilter(filter: TaskFilter): void {
    this.router.navigate([], {
      relativeTo: this.route,
      // The default stays out of the URL, as the project board's view toggle does.
      queryParams: { filter: filter === 'mine' ? null : filter },
      replaceUrl: true,
    });
  }
}

function asFilter(value: string | null): TaskFilter {
  return TASK_FILTERS.includes(value as TaskFilter) ? (value as TaskFilter) : 'mine';
}
