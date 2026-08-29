import { Injectable, computed, inject } from '@angular/core';
import { httpResource } from '@angular/common/http';
import { API_URL } from '@core/tokens/app.tokens';
import { AppConfig } from '@core/models/app-config';
import { LanguageService } from '@core/services/language.service';
import { pluralCategory } from '@core/utils/plural';

/** The trash window, ready for copy that names it. */
export interface TrashWindow {
  days: number;
  plural: 'one' | 'few' | 'other';
}

/**
 * Server policy the UI quotes, fetched once per app load.
 *
 * No client-side default: a fallback window would be a second source of truth for a number
 * only the server knows, so a surface that cannot read it says nothing rather than
 * promising a window nobody confirmed.
 */
@Injectable({ providedIn: 'root' })
export class AppConfigService {
  private apiUrl = inject(API_URL);
  private language = inject(LanguageService);

  private config = httpResource<AppConfig>(() => `${this.apiUrl}/config`);

  // hasValue(), not value(): a resource in an error state throws when read, which would
  // take down every page quoting the window instead of dropping the sentence.
  readonly trashWindowDays = computed(() =>
    this.config.hasValue() ? this.config.value().trashWindowDays : undefined,
  );

  readonly trashWindow = computed<TrashWindow | undefined>(() => {
    const days = this.trashWindowDays();
    return days ? { days, plural: pluralCategory(days, this.language.dateLocale()) } : undefined;
  });
}
