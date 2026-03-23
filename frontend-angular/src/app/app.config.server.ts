import { mergeApplicationConfig, ApplicationConfig } from '@angular/core';
import { provideServerRendering, withRoutes } from '@angular/ssr';
import { appConfig } from './app.config';
import { serverRoutes } from './app.routes.server';
import { API_URL } from '@core/tokens/app.tokens';
import { env } from '@env/env.ssr';

const serverConfig: ApplicationConfig = {
  providers: [
    provideServerRendering(withRoutes(serverRoutes)),
    {
      provide: API_URL,
      useValue: env.apiUrl
    }
  ],
};

export const config = mergeApplicationConfig(appConfig, serverConfig);
