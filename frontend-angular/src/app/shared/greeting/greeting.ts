import { Signal, computed, inject, signal } from '@angular/core';
import { CurrentWeather } from '@core/models/weather';
import { TodayService } from '@core/services/today.service';

export interface Greeting {
  /** Transloco key for the mood line, under `greeting.taglines.*`. */
  taglineKey: Signal<string>;
  /** Wire the weather widget's `(loaded)` to this. */
  onWeatherLoaded(weather: CurrentWeather): void;
}

/**
 * The mood line both homes wear above their title: a weather line once the widget reports
 * in, a time-of-day one until then.
 *
 * Call it from a component's field initializer — it injects, so there is no injection
 * context for it anywhere else, and the weather it holds belongs to one page.
 */
export function createGreeting(): Greeting {
  const todayService = inject(TodayService);

  const weather = signal<CurrentWeather | null>(null);

  // Local part of day. Depends on the day so it re-reads at midnight — without that the
  // line still said "evening" while the task bands below it had already moved to the new
  // day. It does not re-read at noon or six: that would need an hourly tick, and a
  // greeting is not worth one.
  const partOfDay = computed(() => {
    todayService.today();
    const hour = new Date().getHours();
    if (hour < 12) return 'morning';
    if (hour < 18) return 'afternoon';
    return 'evening';
  });

  const taglineKey = computed(() => {
    const current = weather();
    return current && current.conditionKey !== 'unknown'
      ? `greeting.taglines.weather.${weatherMood(current)}`
      : `greeting.taglines.${partOfDay()}`;
  });

  return {
    taglineKey,
    // No markForCheck: the app is zoneless, so writing a signal the template reads is
    // itself what marks the view dirty.
    onWeatherLoaded(next: CurrentWeather): void {
      weather.set(next);
    },
  };
}

// Coarse mood bucket the tagline copy keys off; clear splits by day/night.
function weatherMood(weather: CurrentWeather): string {
  switch (weather.conditionKey) {
    case 'clear':
    case 'mostlyClear':
      return weather.isDay ? 'clear' : 'clearNight';
    case 'fog':
      return 'fog';
    case 'drizzle':
    case 'freezingDrizzle':
    case 'rain':
    case 'freezingRain':
    case 'showers':
      return 'rain';
    case 'snow':
    case 'snowShowers':
      return 'snow';
    case 'thunderstorm':
    case 'thunderstormHail':
      return 'storm';
    default:
      return 'cloudy';
  }
}
