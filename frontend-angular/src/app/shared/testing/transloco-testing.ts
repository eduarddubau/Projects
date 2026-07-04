import { importProvidersFrom } from '@angular/core';
import { TranslocoTestingModule } from '@jsverse/transloco';
import en from '@core/i18n/en.json';
import ro from '@core/i18n/ro.json';

/** Real dictionaries, preloaded, for specs that render translated templates. */
export function provideTranslocoTesting() {
  return importProvidersFrom(
    TranslocoTestingModule.forRoot({
      langs: { en, ro },
      translocoConfig: {
        availableLangs: ['en', 'ro'],
        defaultLang: 'en',
        fallbackLang: 'en',
      },
      preloadLangs: true,
    }),
  );
}
