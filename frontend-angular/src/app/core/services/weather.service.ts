import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, map, of, switchMap, tap, throwError, timeout } from 'rxjs';
import { CurrentWeather, DailyForecast, describeWeather } from '@core/models/weather';

// Two public, key-less endpoints, both CORS `*` so the browser calls them
// directly — no backend proxy needed (the IP geo must see the *user's* IP,
// which only the browser has). Swapping to a keyed/commercial provider later
// is the one thing that would justify moving this behind our own API.
const GEO_URL = 'https://ipwho.is/';
const METEO_URL = 'https://api.open-meteo.com/v1/forecast';
const CURRENT_FIELDS =
  'temperature_2m,apparent_temperature,relative_humidity_2m,dew_point_2m,wind_speed_10m,' +
  'wind_gusts_10m,wind_direction_10m,is_day,weather_code';
const DAILY_FIELDS = 'weather_code,temperature_2m_max,temperature_2m_min';

/** Today plus the three days the strip shows. */
const FORECAST_DAYS = 4;

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
    apparent_temperature: number;
    relative_humidity_2m: number;
    dew_point_2m: number;
    wind_speed_10m: number;
    wind_gusts_10m: number;
    wind_direction_10m: number;
    is_day: number;
    weather_code: number;
  };
  daily: {
    time: string[];
    weather_code: number[];
    temperature_2m_max: number[];
    temperature_2m_min: number[];
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
        // timezone=auto so the daily buckets are the location's days, not UTC's.
        const url =
          `${METEO_URL}?latitude=${geo.latitude}&longitude=${geo.longitude}` +
          `&current=${CURRENT_FIELDS}&daily=${DAILY_FIELDS}` +
          `&forecast_days=${FORECAST_DAYS}&timezone=auto`;
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
      humidity: Math.round(current.relative_humidity_2m),
      dewPoint: Math.round(current.dew_point_2m),
      windSpeed: Math.round(current.wind_speed_10m),
      windGusts: Math.round(current.wind_gusts_10m),
      windDirection: Math.round(current.wind_direction_10m),
      feelsLike: Math.round(current.apparent_temperature),
      isDay,
      weatherCode: current.weather_code,
      conditionKey,
      icon,
      todayHigh: Math.round(weather.daily.temperature_2m_max[0]),
      todayLow: Math.round(weather.daily.temperature_2m_min[0]),
      forecast: this.toForecast(weather),
    };
  }

  /** Index 0 is today, which the readout beside the icon already reports. */
  private toForecast(weather: MeteoResponse): DailyForecast[] {
    return weather.daily.time.slice(1).map((date, index) => {
      const day = index + 1;
      // Daytime icons throughout: a forecast day has no hour to be night in.
      const { conditionKey, icon } = describeWeather(weather.daily.weather_code[day], true);

      return {
        date,
        high: Math.round(weather.daily.temperature_2m_max[day]),
        low: Math.round(weather.daily.temperature_2m_min[day]),
        conditionKey,
        icon,
      };
    });
  }
}
