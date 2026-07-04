import {
  ApplicationRef, DOCUMENT, Injectable, PLATFORM_ID, REQUEST, inject, signal,
} from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Meta, Title } from '@angular/platform-browser';
import { TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { prefersReducedMotion, startViewTransition } from '@core/utils/view-transition';

export type Lang = 'en' | 'ro';

export const LANGUAGES: ReadonlyArray<{ id: Lang; label: string; flag: string }> = [
  { id: 'en', label: 'English', flag: 'flags/en.svg' },
  { id: 'ro', label: 'Română', flag: 'flags/ro.svg' },
];

const COOKIE_NAME = 'lang';
const COOKIE_MAX_AGE = 31536000;
const COOKIE_PATTERN = /(?:^|;\s*)lang=([A-Za-z-]+)/;

// Resolves the language on entry (cookie, else browser/Accept-Language, else
// English) and persists explicit choices as a cookie so the SSR'd landing
// page renders in the right language. Switches crossfade the whole page via
// the View Transitions API; the swap is instant under reduced motion.
@Injectable({ providedIn: 'root' })
export class LanguageService {
  private document = inject(DOCUMENT);
  private isBrowser = isPlatformBrowser(inject(PLATFORM_ID));
  private request = inject(REQUEST, { optional: true });
  private transloco = inject(TranslocoService);
  private appRef = inject(ApplicationRef);
  private title = inject(Title);
  private meta = inject(Meta);

  /** Active language; drives Transloco, <html lang> and the switcher UI. */
  readonly lang = signal<Lang>(this.initialLang());

  constructor() {
    this.apply(this.lang());
    this.transloco.selectTranslate('app.title')
      .pipe(takeUntilDestroyed())
      .subscribe((title) => {
        this.title.setTitle(title);
        const description = this.transloco.translate('app.description');
        this.meta.updateTag({ name: 'description', content: description });
      });
  }

  /** Switches language with a page-wide crossfade. */
  async set(lang: Lang): Promise<void> {
    if (lang === this.lang()) return;
    if (!this.isBrowser) {
      this.apply(lang);
      return;
    }

    // Warm the dictionary so the transition's update callback never waits on IO.
    await firstValueFrom(this.transloco.load(lang));

    // The callback must not resolve until the re-render lands, so the "new"
    // snapshot the browser animates to already shows the translated DOM.
    const update = async () => {
      this.apply(lang);
      await this.appRef.whenStable();
    };

    const transition = !prefersReducedMotion() && startViewTransition(this.document, update);
    if (transition) {
      transition.ready.then(() => this.animateCrossfade());
    } else {
      await update();
    }
  }

  private apply(lang: Lang): void {
    this.transloco.setActiveLang(lang);
    this.lang.set(lang);
    // Server-side too: the mutation serializes into the SSR HTML as <html lang>.
    this.document.documentElement.lang = lang;
    if (this.isBrowser) {
      this.document.cookie = `${COOKIE_NAME}=${lang}; path=/; max-age=${COOKIE_MAX_AGE}; SameSite=Lax`;
    }
  }

  // styles.scss disables the default view-transition animation (the theme
  // reveal needs that), so the crossfade is driven here via WAAPI instead.
  private animateCrossfade(): void {
    const root = this.document.documentElement;
    const timing: KeyframeAnimationOptions = {
      duration: 320,
      easing: 'cubic-bezier(0.22, 1, 0.36, 1)',
    };
    root.animate({ opacity: [1, 0] }, { ...timing, pseudoElement: '::view-transition-old(root)' });
    root.animate(
      { opacity: [0, 1], transform: ['translateY(10px)', 'translateY(0)'] },
      { ...timing, pseudoElement: '::view-transition-new(root)' },
    );
  }

  private initialLang(): Lang {
    if (this.isBrowser) {
      return (
        this.parseLang(COOKIE_PATTERN.exec(this.document.cookie)?.[1]) ??
        this.parseLang(navigator.language) ??
        'en'
      );
    }
    const cookie = this.request?.headers.get('cookie') ?? '';
    const acceptLanguage = this.request?.headers.get('accept-language') ?? '';
    return (
      this.parseLang(COOKIE_PATTERN.exec(cookie)?.[1]) ??
      this.acceptedLang(acceptLanguage) ??
      'en'
    );
  }

  private parseLang(value: string | null | undefined): Lang | null {
    if (!value) return null;
    const prefix = value.toLowerCase().split('-')[0];
    return LANGUAGES.some((l) => l.id === prefix) ? (prefix as Lang) : null;
  }

  private acceptedLang(header: string): Lang | null {
    for (const part of header.split(',')) {
      const lang = this.parseLang(part.split(';')[0].trim());
      if (lang) return lang;
    }
    return null;
  }
}
