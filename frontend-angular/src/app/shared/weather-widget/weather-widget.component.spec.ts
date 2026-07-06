import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';

import { WeatherWidgetComponent } from './weather-widget.component';
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
      providers: [provideHttpClient(), provideHttpClientTesting(), provideTranslocoTesting()],
    }).compileComponents();

    fixture = TestBed.createComponent(WeatherWidgetComponent);
    httpMock = TestBed.inject(HttpTestingController);
    host = fixture.nativeElement as HTMLElement;
    fixture.detectChanges();
  });

  afterEach(() => httpMock.verify());

  it('renders temperature, condition, city and the matching animated icon', () => {
    httpMock.expectOne(GEO_URL).flush({
      success: true, city: 'Oradea', country: 'Romania', latitude: 47.05, longitude: 21.92,
    });
    httpMock.expectOne((r) => isMeteo(r.url)).flush({
      current: { temperature_2m: 26.3, is_day: 1, weather_code: 1 },
    });
    fixture.detectChanges();

    const text = host.textContent ?? '';
    expect(text).toContain('26°');
    expect(text).toContain('Oradea');

    // Condition is conveyed by the icon (visually + via alt for screen readers).
    const icon = host.querySelector('img.weather-icon');
    expect(icon?.getAttribute('src')).toBe('weather/clear-day.svg');
    expect(icon?.getAttribute('alt')).toBe('Mainly clear');
  });

  it('renders nothing when the weather fails to load', () => {
    httpMock.expectOne(GEO_URL).flush({ success: false, message: 'quota exceeded' });
    fixture.detectChanges();

    expect((host.textContent ?? '').trim()).toBe('');
  });
});
