import {
  EnvironmentProviders, Provider, inject, isDevMode, provideAppInitializer,
} from '@angular/core';
import { registerLocaleData } from '@angular/common';
import localeRo from '@angular/common/locales/ro';
import { provideTransloco } from '@jsverse/transloco';
import { TranslocoStaticLoader } from './transloco.loader';
import { LANGUAGES, LanguageService } from '@core/services/language.service';

// Locale data for the two date:'medium' bindings in project-detail.
registerLocaleData(localeRo);

export function provideI18n(): (Provider | EnvironmentProviders)[] {
  return [
    provideTransloco({
      config: {
        availableLangs: LANGUAGES.map((l) => l.id),
        defaultLang: 'en',
        fallbackLang: 'en',
        reRenderOnLangChange: true,
        prodMode: !isDevMode(),
        missingHandler: {
          useFallbackTranslation: true,
          logMissingKey: isDevMode(),
        },
      },
      loader: TranslocoStaticLoader,
    }),
    // MatPaginatorIntl is overridden per table component (see
    // TranslocoPaginatorIntl) so Material's paginator stays out of the
    // initial bundle.
    // Resolve the language before first render (SSR included).
    provideAppInitializer(() => {
      inject(LanguageService);
    }),
  ];
}
