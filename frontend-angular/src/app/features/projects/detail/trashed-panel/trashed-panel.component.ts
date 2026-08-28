import { ChangeDetectionStrategy, Component, inject, input, output } from '@angular/core';
import { DatePipe } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { TranslocoDirective } from '@jsverse/transloco';
import { LanguageService } from '@core/services/language.service';
import { Project } from '@core/models/project';

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
}
