import { ChangeDetectionStrategy, Component, computed, inject, input, output } from '@angular/core';
import { DatePipe } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { TranslocoDirective } from '@jsverse/transloco';
import { AppConfigService } from '@core/services/app-config.service';
import { LanguageService } from '@core/services/language.service';
import { TodayService } from '@core/services/today.service';
import { Project } from '@core/models/project';
import { expiryInstant, expiryIso } from '@core/utils/trash-expiry';

/**
 * What a trashed project shows where its board would be: why it is here, a way back, and
 * the whole record — the board cannot stand in, since a trashed project's tasks endpoint
 * refuses it and would only ever render an error.
 *
 * Presentational: restoring is the page's job, because that is where the reload lives.
 */
@Component({
  selector: 'app-project-trashed-panel',
  templateUrl: './trashed-panel.component.html',
  styleUrl: './trashed-panel.component.scss',
  imports: [DatePipe, MatButtonModule, MatIconModule, TranslocoDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProjectTrashedPanelComponent {
  project = input.required<Project>();
  canRestore = input.required<boolean>();

  restore = output<void>();

  dateLocale = inject(LanguageService).dateLocale;

  private trashWindowDays = inject(AppConfigService).trashWindowDays;
  private today = inject(TodayService).today;

  /** The day this project stops being restorable, once the window is known. */
  expiry = computed(() => {
    const deletedAt = this.project().deletedAt;
    const days = this.trashWindowDays();
    return deletedAt && days ? expiryIso(deletedAt, days) : undefined;
  });

  /**
   * Instants, not days: the server refuses a restore from the deletion's time of day, so a
   * day-level check would keep offering the button for hours after it started failing.
   */
  isExpired = computed(() => {
    const deletedAt = this.project().deletedAt;
    const days = this.trashWindowDays();

    // Read for its dependency, not its value: Date.now() is not reactive, so without the
    // day signal this answer is computed once and a page left open never withdraws Restore.
    this.today();

    return !!deletedAt && !!days && Date.now() >= expiryInstant(deletedAt, days);
  });
}
