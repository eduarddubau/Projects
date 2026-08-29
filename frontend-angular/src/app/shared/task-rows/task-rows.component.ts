import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { TranslocoDirective } from '@jsverse/transloco';
import { LanguageService } from '@core/services/language.service';
import { TodayService } from '@core/services/today.service';
import { WorkspaceTask } from '@core/models/task';
import { TaskRow, taskRows } from '@core/utils/task-row';

/** Tasks read away from their board — the workspace list and the home digest share these rows. */
@Component({
  selector: 'app-task-rows',
  templateUrl: './task-rows.component.html',
  styleUrl: './task-rows.component.scss',
  imports: [DatePipe, RouterLink, MatIconModule, MatTooltipModule, TranslocoDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TaskRowsComponent {
  private languageService = inject(LanguageService);
  private todayService = inject(TodayService);

  tasks = input.required<WorkspaceTask[]>();
  workspaceId = input.required<string | null>();

  rows = computed<TaskRow<WorkspaceTask>[]>(() =>
    taskRows(this.tasks(), this.todayService.today()),
  );

  dateLocale = this.languageService.dateLocale;
}
