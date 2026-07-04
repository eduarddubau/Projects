import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter } from '@angular/router';
import { routes } from './app.routes';
import {
  provideClientHydration,
  withEventReplay,
  withNoIncrementalHydration,
} from '@angular/platform-browser';
import { provideHttpClient, withFetch, withInterceptors } from '@angular/common/http';
import { API_URL, HEALTH_URL } from '@core/tokens/app.tokens';
import { env } from '@env/env';
import { authInterceptor } from '@core/interceptors/auth.interceptor';
import { provideI18n } from '@core/i18n/i18n.providers';

export const appConfig: ApplicationConfig = {
  providers: [
    provideHttpClient(withFetch(), withInterceptors([authInterceptor])),
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),
    provideClientHydration(withEventReplay(), withNoIncrementalHydration()),
    provideI18n(),
    { provide: API_URL, useValue: env.apiUrl },
    { provide: HEALTH_URL, useValue: env.healthUrl },
  ],
};
