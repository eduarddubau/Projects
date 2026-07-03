import { DOCUMENT, Injectable, PLATFORM_ID, inject, signal } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';

export type ThemePreference = 'system' | 'light' | 'dark';

const STORAGE_KEY = 'theme';

// Forces a color scheme by stamping data-theme on <html>; index.html applies
// the stored value before first paint so reloads never flash the wrong theme.
@Injectable({ providedIn: 'root' })
export class ThemeService {
  private document = inject(DOCUMENT);
  private isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  readonly preference = signal<ThemePreference>(this.readStored());

  cycle(): void {
    const order: ThemePreference[] = ['system', 'light', 'dark'];
    this.set(order[(order.indexOf(this.preference()) + 1) % order.length]);
  }

  set(preference: ThemePreference): void {
    this.preference.set(preference);
    if (!this.isBrowser) return;

    if (preference === 'system') {
      delete this.document.documentElement.dataset['theme'];
      localStorage.removeItem(STORAGE_KEY);
    } else {
      this.document.documentElement.dataset['theme'] = preference;
      localStorage.setItem(STORAGE_KEY, preference);
    }
  }

  private readStored(): ThemePreference {
    if (!this.isBrowser) return 'system';
    const stored = localStorage.getItem(STORAGE_KEY);
    return stored === 'light' || stored === 'dark' ? stored : 'system';
  }
}
