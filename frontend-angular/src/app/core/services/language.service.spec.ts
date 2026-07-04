import { TestBed } from '@angular/core/testing';
import { PLATFORM_ID, REQUEST, signal } from '@angular/core';
import { LanguageService } from './language.service';
import { ThemeService } from './theme.service';
import { provideTranslocoTesting } from '@shared/testing/transloco-testing';

// The real ThemeService touches window.matchMedia, which jsdom lacks.
const themeStub = { provide: ThemeService, useValue: { theme: signal('light') } };

function clearLangCookie(): void {
  document.cookie = 'lang=; path=/; max-age=0';
}

function serverRequest(headers: Record<string, string>): Pick<Request, 'headers'> {
  return { headers: new Headers(headers) };
}

describe('LanguageService', () => {
  beforeEach(() => clearLangCookie());
  afterEach(() => clearLangCookie());

  describe('in the browser', () => {
    function create(): LanguageService {
      TestBed.configureTestingModule({ providers: [provideTranslocoTesting(), themeStub] });
      return TestBed.inject(LanguageService);
    }

    it('defaults to English without a cookie', () => {
      const service = create();
      expect(service.lang()).toBe('en');
      expect(document.documentElement.lang).toBe('en');
    });

    it('resolves the language from the lang cookie', () => {
      document.cookie = 'lang=ro; path=/';
      const service = create();
      expect(service.lang()).toBe('ro');
      expect(document.documentElement.lang).toBe('ro');
    });

    it('ignores unsupported cookie values', () => {
      document.cookie = 'lang=de; path=/';
      const service = create();
      expect(service.lang()).toBe('en');
    });

    it('persists the resolved language as a cookie', () => {
      document.cookie = 'lang=ro; path=/';
      create();
      expect(document.cookie).toContain('lang=ro');
    });
  });

  describe('on the server', () => {
    function create(headers: Record<string, string>): LanguageService {
      TestBed.configureTestingModule({
        providers: [
          provideTranslocoTesting(),
          themeStub,
          { provide: PLATFORM_ID, useValue: 'server' },
          { provide: REQUEST, useValue: serverRequest(headers) },
        ],
      });
      return TestBed.inject(LanguageService);
    }

    it('reads the lang cookie from the request', () => {
      const service = create({ cookie: 'theme=dark; lang=ro' });
      expect(service.lang()).toBe('ro');
      expect(document.documentElement.lang).toBe('ro');
    });

    it('falls back to Accept-Language when there is no cookie', () => {
      const service = create({ 'accept-language': 'ro-RO,ro;q=0.9,en;q=0.8' });
      expect(service.lang()).toBe('ro');
    });

    it('skips unsupported Accept-Language entries', () => {
      const service = create({ 'accept-language': 'de-DE,de;q=0.9,ro;q=0.8' });
      expect(service.lang()).toBe('ro');
    });

    it('defaults to English with no signals at all', () => {
      const service = create({});
      expect(service.lang()).toBe('en');
    });

    it('set() applies directly without touching browser APIs', async () => {
      const service = create({});
      await service.set('ro');
      expect(service.lang()).toBe('ro');
      expect(document.documentElement.lang).toBe('ro');
    });
  });
});
