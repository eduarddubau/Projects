import { DOCUMENT, Injectable, PLATFORM_ID, inject, signal } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';

export type Theme = 'light' | 'dark';

const STORAGE_KEY = 'theme';

// Resolves the OS scheme on entry and follows it until the user explicitly
// toggles; a choice is then stamped as data-theme on <html> and persisted
// (index.html re-applies it before first paint so reloads never flash).
// Switches animate as a circular reveal from the toggle (View Transitions API)
// or a brief color crossfade where the API is unavailable.
@Injectable({ providedIn: 'root' })
export class ThemeService {
  private document = inject(DOCUMENT);
  private isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  /** Effective theme: the user's stored choice, else the OS scheme. */
  readonly theme = signal<Theme>(this.initialTheme());

  constructor() {
    if (this.isBrowser && !localStorage.getItem(STORAGE_KEY)) {
      window.matchMedia('(prefers-color-scheme: dark)').addEventListener('change', (event) => {
        if (!localStorage.getItem(STORAGE_KEY)) {
          this.theme.set(event.matches ? 'dark' : 'light');
        }
      });
    }
  }

  /** Switches to the other theme; `origin` centers the reveal animation. */
  toggle(origin?: { x: number; y: number }): void {
    this.set(this.theme() === 'dark' ? 'light' : 'dark', origin);
  }

  set(theme: Theme, origin?: { x: number; y: number }): void {
    if (!this.isBrowser) {
      this.theme.set(theme);
      return;
    }

    const reduceMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    const doc = this.document as Document & {
      startViewTransition?: (update: () => void) => { ready: Promise<void> };
    };

    if (theme === this.theme() || reduceMotion) {
      this.apply(theme);
    } else if (doc.startViewTransition) {
      const transition = doc.startViewTransition(() => this.apply(theme));
      if (origin) {
        transition.ready.then(() => this.animateReveal(origin));
      }
    } else {
      const root = this.document.documentElement;
      root.classList.add('theme-transition');
      this.apply(theme);
      window.setTimeout(() => root.classList.remove('theme-transition'), 450);
    }
  }

  private apply(theme: Theme): void {
    this.theme.set(theme);
    this.document.documentElement.dataset['theme'] = theme;
    localStorage.setItem(STORAGE_KEY, theme);
  }

  private animateReveal(origin: { x: number; y: number }): void {
    const radius = Math.hypot(
      Math.max(origin.x, window.innerWidth - origin.x),
      Math.max(origin.y, window.innerHeight - origin.y),
    );
    this.document.documentElement.animate(
      {
        clipPath: [
          `circle(0px at ${origin.x}px ${origin.y}px)`,
          `circle(${radius}px at ${origin.x}px ${origin.y}px)`,
        ],
      },
      {
        duration: 550,
        easing: 'cubic-bezier(0.22, 1, 0.36, 1)',
        pseudoElement: '::view-transition-new(root)',
      },
    );
  }

  private initialTheme(): Theme {
    if (!this.isBrowser) return 'light';
    const stored = localStorage.getItem(STORAGE_KEY);
    if (stored === 'light' || stored === 'dark') return stored;
    return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
  }
}
