import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, map, of, switchMap, tap, throwError, timeout } from 'rxjs';
import { CurrentWeather, describeWeather } from '@core/models/weather';

// Two public, key-less endpoints, both CORS `*` so the browser calls them
// directly — no backend proxy needed (the IP geo must see the *user's* IP,
// which only the browser has). Swapping to a keyed/commercial provider later
// is the one thing that would justify moving this behind our own API.
const GEO_URL = 'https://ipwho.is/';
const METEO_URL = 'https://api.open-meteo.com/v1/forecast';
const CURRENT_FIELDS = 'temperature_2m,is_day,weather_code';

const REQUEST_TIMEOUT_MS = 8000;
const CACHE_TTL_MS = 10 * 60 * 1000;

interface GeoResponse {
  success: boolean;
  message?: string;
  city: string;
  country: string;
  latitude: number;
  longitude: number;
}

interface MeteoResponse {
  current: {
    temperature_2m: number;
    is_day: number;
    weather_code: number;
  };
}

@Injectable({ providedIn: 'root' })
export class WeatherService {
  private http = inject(HttpClient);
  private cache?: { at: number; data: CurrentWeather };

  /**
   * Current weather for the caller's IP-derived location: ipwho.is geolocates
   * this IP, then Open-Meteo returns the forecast for those coordinates.
   * Cached for CACHE_TTL_MS so revisits don't re-hit the free tiers; `force`
   * bypasses the cache (used by the retry button).
   */
  getCurrentWeather(force = false): Observable<CurrentWeather> {
    if (!force && this.cache && Date.now() - this.cache.at < CACHE_TTL_MS) {
      return of(this.cache.data);
    }

    return this.http.get<GeoResponse>(GEO_URL).pipe(
      timeout(REQUEST_TIMEOUT_MS),
      switchMap((geo) => {
        if (!geo?.success) {
          return throwError(() => new Error(geo?.message ?? 'IP geolocation failed'));
        }
        const url =
          `${METEO_URL}?latitude=${geo.latitude}&longitude=${geo.longitude}&current=${CURRENT_FIELDS}`;
        return this.http.get<MeteoResponse>(url).pipe(
          timeout(REQUEST_TIMEOUT_MS),
          map((weather) => this.toCurrentWeather(geo, weather)),
        );
      }),
      tap((data) => (this.cache = { at: Date.now(), data })),
    );
  }

  private toCurrentWeather(geo: GeoResponse, weather: MeteoResponse): CurrentWeather {
    const current = weather.current;
    const isDay = current.is_day === 1;
    const { conditionKey, icon } = describeWeather(current.weather_code, isDay);

    return {
      city: geo.city,
      country: geo.country,
      temperature: Math.round(current.temperature_2m),
      isDay,
      weatherCode: current.weather_code,
      conditionKey,
      icon,
    };
  }
}
