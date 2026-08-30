import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ApplicationRef, signal } from '@angular/core';

import { AppConfigService } from './app-config.service';
import { LanguageService } from './language.service';
import { API_URL } from '@core/tokens/app.tokens';

const apiUrl = 'http://api.test';

describe('AppConfigService', () => {
  let httpMock: HttpTestingController;
  let dateLocale: ReturnType<typeof signal<string>>;

  function setup(): AppConfigService {
    dateLocale = signal('en-US');

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: API_URL, useValue: apiUrl },
        { provide: LanguageService, useValue: { dateLocale } },
      ],
    });

    httpMock = TestBed.inject(HttpTestingController);
    const service = TestBed.inject(AppConfigService);
    TestBed.tick();
    return service;
  }

  async function respondWith(days: number): Promise<void> {
    httpMock.expectOne(`${apiUrl}/config`).flush({ trashWindowDays: days });
    await TestBed.inject(ApplicationRef).whenStable();
  }

  afterEach(() => httpMock.verify());

  it('publishes the window the server sent', async () => {
    const service = setup();
    await respondWith(14);

    expect(service.trashWindowDays()).toBe(14);
    expect(service.trashWindow()).toEqual({ days: 14, plural: 'other' });
  });

  // Nothing may invent a window: copy naming one is dropped instead.
  it('has no window until the server answers', async () => {
    const service = setup();

    expect(service.trashWindowDays()).toBeUndefined();
    expect(service.trashWindow()).toBeUndefined();

    await respondWith(30);
  });

  it('leaves no window behind when the fetch fails', async () => {
    const service = setup();
    httpMock.expectOne(`${apiUrl}/config`).error(new ProgressEvent('failed'));
    await TestBed.inject(ApplicationRef).whenStable();

    expect(service.trashWindow()).toBeUndefined();
  });

  // A blip must not cost the session: the surfaces that quote the window ask again.
  it('asks again after a failed fetch, and keeps what the retry returns', async () => {
    const service = setup();
    httpMock.expectOne(`${apiUrl}/config`).error(new ProgressEvent('failed'));
    await TestBed.inject(ApplicationRef).whenStable();

    service.reloadIfFailed();
    TestBed.tick();
    await respondWith(21);

    expect(service.trashWindowDays()).toBe(21);
  });

  it('does not re-fetch a window it already has', async () => {
    const service = setup();
    await respondWith(30);

    service.reloadIfFailed();
    TestBed.tick();

    expect(service.trashWindowDays()).toBe(30);
    httpMock.verify();
  });

  it('picks the plural category for the reader’s language', async () => {
    const service = setup();
    await respondWith(7);
    expect(service.trashWindow()?.plural).toBe('other');

    dateLocale.set('ro');
    expect(service.trashWindow()?.plural).toBe('few');
  });
});
