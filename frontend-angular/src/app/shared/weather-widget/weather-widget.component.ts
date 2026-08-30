import {
  Component,
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  DestroyRef,
  OnInit,
  inject,
  output,
  signal,
} from '@angular/core';
import { DatePipe } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { TranslocoDirective } from '@jsverse/transloco';
import { LanguageService } from '@core/services/language.service';
import { fromIsoDate } from '@core/utils/iso-date';
import { WeatherService } from '@core/services/weather.service';
import { CurrentWeather } from '@core/models/weather';

const COMPASS = ['n', 'ne', 'e', 'se', 's', 'sw', 'w', 'nw'] as const;

/** Beaufort, folded into the six bands a tooltip can say in one word. km/h upper bounds. */
const WIND_BANDS: readonly [number, string][] = [
  [1, 'calm'],
  [12, 'light'],
  [29, 'breeze'],
  [50, 'windy'],
  [75, 'gale'],
];

/** Dew point °C upper bounds; the scale forecasters use for how the air feels. */
const COMFORT_BANDS: readonly [number, string][] = [
  [10, 'dry'],
  [16, 'comfortable'],
  [19, 'sticky'],
  [22, 'humid'],
];

// Compact, chrome-less ambient weather: temperature with a big animated icon and
// the place below, with the numbers behind tooltips so the strip stays quiet. Self-fetches on init (location auto-detected via IP) and
// renders nothing until — or unless — data arrives, so it drops into any row.
@Component({
  selector: 'app-weather-widget',
  templateUrl: './weather-widget.component.html',
  styleUrl: './weather-widget.component.scss',
  imports: [DatePipe, MatIconModule, MatTooltipModule, TranslocoDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class WeatherWidgetComponent implements OnInit {
  private weatherService = inject(WeatherService);
  private cdr = inject(ChangeDetectorRef);
  private destroyRef = inject(DestroyRef);

  weather = signal<CurrentWeather | null>(null);

  dateLocale = inject(LanguageService).dateLocale;

  /**
   * A forecast day as a local Date, never the raw string.
   *
   * DatePipe parses `yyyy-MM-dd` at *local* midnight, so formatting it in UTC renders the
   * day before for anyone east of Greenwich — which showed tomorrow's forecast under
   * today's name. Parsing and formatting both local is the only pair that agrees.
   */
  weekdayOf(date: string): Date | null {
    return fromIsoDate(date);
  }

  /** Wind direction as an eight-point key; the API gives degrees clockwise from north. */
  compassOf(degrees: number): string {
    return COMPASS[Math.round(degrees / 45) % 8];
  }

  /** What the sustained speed is called, so the tooltip adds to the number rather than repeating it. */
  strengthOf(kmh: number): string {
    return WIND_BANDS.find(([limit]) => kmh < limit)?.[1] ?? 'storm';
  }

  /** How the air feels at this dew point, which is what the humidity percentage cannot say. */
  comfortOf(dewPoint: number): string {
    return COMFORT_BANDS.find(([limit]) => dewPoint < limit)?.[1] ?? 'oppressive';
  }

  /** Emits once weather resolves, so a host can react (e.g. a mood tagline). */
  loaded = output<CurrentWeather>();

  ngOnInit(): void {
    this.weatherService
      .getCurrentWeather()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (weather) => {
          this.weather.set(weather);
          this.loaded.emit(weather);
          this.cdr.markForCheck();
        },
        error: () => {
          /* Non-essential ornament: on failure the widget stays absent, no fuss. */
        },
      });
  }
}
