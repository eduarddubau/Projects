import { InjectionToken } from '@angular/core';

export const API_URL = new InjectionToken<string>('API_URL');
export const HEALTH_URL = new InjectionToken<string>('HEALTH_URL');

/** Provisional product name — single place to rebrand once a real name lands. */
export const APP_NAME = 'Projects';