import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';

import { signal } from '@angular/core';
import { By } from '@angular/platform-browser';
import { MatTooltip } from '@angular/material/tooltip';

import { WeatherWidgetComponent } from './weather-widget.component';
import { LanguageService } from '@core/services/language.service';
import { provideTranslocoTesting } from '@shared/testing/transloco-testing';

const GEO_URL = 'https://ipwho.is/';
const isMeteo = (url: string) => url.startsWith('https://api.open-meteo.com');

describe('WeatherWidgetComponent', () => {
  let fixture: ComponentFixture<WeatherWidgetComponent>;
  let httpMock: HttpTestingController;
  let host: HTMLElement;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [WeatherWidgetComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideTranslocoTesting(),
        // The real one pulls ThemeService, which reads window.matchMedia — absent in jsdom.
        { provide: LanguageService, useValue: { dateLocale: signal('en-US') } },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(WeatherWidgetComponent);
    httpMock = TestBed.inject(HttpTestingController);
    host = fixture.nativeElement as HTMLElement;
    fixture.detectChanges();
  });

  afterEach(() => httpMock.verify());

  /** Answers both hops the widget makes: geolocate the IP, then read that location. */
  function respondWithWeather(): void {
    httpMock.expectOne(GEO_URL).flush({
      success: true,
      city: 'Oradea',
      country: 'Romania',
      latitude: 47.05,
      longitude: 21.92,
    });
    httpMock
      .expectOne((r) => isMeteo(r.url))
      .flush({
        current: {
          temperature_2m: 26.3,
          apparent_temperature: 24.4,
          relative_humidity_2m: 64.7,
          dew_point_2m: 14.2,
          wind_speed_10m: 11.6,
          wind_gusts_10m: 23.7,
          wind_direction_10m: 315,
          is_day: 1,
          weather_code: 1,
        },
        daily: {
          time: ['2026-08-30', '2026-08-31', '2026-09-01', '2026-09-02'],
          weather_code: [1, 61, 3, 0],
          temperature_2m_max: [27.4, 21.2, 23.8, 25.1],
          temperature_2m_min: [14.6, 12.9, 13.4, 15.2],
        },
      });
    fixture.detectChanges();
  }

  it('renders temperature, city and the matching animated icon', () => {
    respondWithWeather();

    const text = host.textContent ?? '';
    expect(text).toContain('26°');
    expect(text).toContain('Oradea');
    expect(text).toContain('65%');
    expect(text).toContain('12 km/h');

    const icon = host.querySelector('img.weather-icon');
    expect(icon?.getAttribute('src')).toBe('weather/clear-day.svg');

    // Decorative: the day it belongs to carries the condition in its own label, and two
    // readings of the same fact is worse than one.
    expect(host.querySelector('img.forecast-icon')?.getAttribute('alt')).toBe('');
  });

  // The face stays a glance; every number behind it is one hover away.
  it('explains each reading in a tooltip', () => {
    respondWithWeather();

    const tooltips = fixture.debugElement
      .queryAll(By.directive(MatTooltip))
      .map((el) => el.injector.get(MatTooltip).message);

    expect(tooltips).toEqual([
      // "Now" and its temperature share one target, so hovering either answers. Two lines,
      // because one wraps mid-number inside the tooltip's 200px.
      'Feels like 24°\nHigh 27°, low 15°',
      'Oradea, Romania\nLocated via IP address',
      'Mainly clear',
      'High 21°, low 13°',
      'High 24°, low 13°',
      'High 25°, low 15°',
      // Says what 65% means rather than reading it back: a 14° dew point is comfortable.
      'Comfortable\nDew point 14°',
      // Banded on the rounded speed the face shows: 12 km/h is where a breeze starts.
      // 315° is north-west, and the gust is the number the face cannot show.
      'A breeze from the north-west\nGusts to 24 km/h',
    ]);
  });

  // Tooltips only answer a mouse, so the same facts are labels too.
  it('offers every hover-only fact to a screen reader as well', () => {
    respondWithWeather();

    expect(host.querySelector('img.weather-icon')?.getAttribute('alt')).toBe('Mainly clear');
    expect(host.querySelector('.weather-now')?.getAttribute('aria-label')).toBe(
      'Now 26° · Feels like 24°\nHigh 27°, low 15°',
    );
    expect(host.querySelector('.forecast-day')?.getAttribute('aria-label')).toBe(
      'Monday · Rain · High 21°, low 13°',
    );
    expect(host.querySelectorAll('.weather-stat')[0].getAttribute('aria-label')).toBe(
      'Comfortable\nDew point 14°',
    );
  });

  it('names the days after today, and leaves today to the readout', () => {
    respondWithWeather();

    // Three days ahead, today excluded: the sample starts on Sunday, so the strip opens
    // on Monday and today's weather is already the readout's job.
    const days = host.querySelectorAll('.forecast-day');
    expect(days).toHaveLength(3);
    expect(days[0].textContent).toContain('Mon');
    expect(days[1].textContent).toContain('Tue');
    expect(days[2].textContent).toContain('Wed');
    // The current reading is labelled "Now", so no weekday sits beside the days ahead to
    // be misread as the first of them.
    expect(host.querySelector('.weather-today')?.textContent).toContain('Now');
    expect(host.querySelector('.weather-forecast')?.textContent).not.toContain('Sun');
    expect(host.querySelectorAll('img.forecast-icon')[0].getAttribute('src')).toBe(
      'weather/rain.svg',
    );
  });

  it('renders nothing when the weather fails to load', () => {
    httpMock.expectOne(GEO_URL).flush({ success: false, message: 'quota exceeded' });
    fixture.detectChanges();

    expect((host.textContent ?? '').trim()).toBe('');
  });
});
