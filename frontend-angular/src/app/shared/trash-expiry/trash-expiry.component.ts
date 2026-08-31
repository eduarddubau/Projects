import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { AppConfigService } from '@core/services/app-config.service';
import { LanguageService } from '@core/services/language.service';
import { TodayService } from '@core/services/today.service';
import { pluralKey } from '@core/utils/plural';
import { daysUntilExpiry, expiryIso } from '@core/utils/trash-expiry';

/** How close to the end of the window a row starts reading as urgent. */
const SOON_DAYS = 3;

/**
 * When a trashed row leaves the window, in the reader's own days.
 *
 * Every trash surface shows this, so the date math, the plural selection and the urgency
 * threshold live here once. The day comes from TodayService, so a page left open over
 * midnight re-counts itself.
 */
@Component({
  selector: 'app-trash-expiry',
  imports: [TranslocoPipe],
  template: `
    @if (label(); as key) {
      <span class="expiry" [class.expiry-soon]="isSoon()">
        {{ key | transloco: { days: days() } }}
      </span>
    }
  `,
  styles: `
    .expiry-soon {
      color: var(--mat-sys-error);
      font-weight: 600;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TrashExpiryComponent {
  private config = inject(AppConfigService);
  private language = inject(LanguageService);
  private today = inject(TodayService).today;

  deletedAt = input<string | undefined>();

  /** Restorable counts down to the row leaving the trash; purgeable counts up to an admin being able to erase it. */
  variant = input<'restorable' | 'purgeable'>('restorable');

  days = computed(() => {
    const deletedAt = this.deletedAt();
    const windowDays = this.config.trashWindowDays();
    if (!deletedAt || !windowDays) return undefined;

    return daysUntilExpiry(expiryIso(deletedAt, windowDays), this.today());
  });

  isSoon = computed(() => {
    const days = this.days();
    return this.variant() === 'restorable' && days !== undefined && days <= SOON_DAYS;
  });

  label = computed(() => {
    const days = this.days();
    if (days === undefined) return undefined;

    const base = `trash.expiry.${this.variant()}`;
    if (days < 0) return `${base}.expired`;
    if (days === 0) return `${base}.due`;
    if (days === 1) return `${base}.tomorrow`;

    return pluralKey(`${base}.inDays`, days, this.language.dateLocale());
  });
}
