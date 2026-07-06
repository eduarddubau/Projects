import {
  Component, ChangeDetectionStrategy, ChangeDetectorRef, DestroyRef,
  OnInit, inject, output, signal
} from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { TranslocoDirective } from '@jsverse/transloco';
import { WeatherService } from '@core/services/weather.service';
import { CurrentWeather } from '@core/models/weather';

// Compact, chrome-less ambient weather: temperature with a big animated icon and
// the place below. Self-fetches on init (location auto-detected via IP) and
// renders nothing until — or unless — data arrives, so it drops into any row.
@Component({
  selector: 'app-weather-widget',
  templateUrl: './weather-widget.component.html',
  styleUrl: './weather-widget.component.scss',
  imports: [MatIconModule, TranslocoDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class WeatherWidgetComponent implements OnInit {
  private weatherService = inject(WeatherService);
  private cdr = inject(ChangeDetectorRef);
  private destroyRef = inject(DestroyRef);

  weather = signal<CurrentWeather | null>(null);

  /** Emits once weather resolves, so a host can react (e.g. a mood tagline). */
  loaded = output<CurrentWeather>();

  ngOnInit(): void {
    this.weatherService.getCurrentWeather()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (weather) => {
          this.weather.set(weather);
          this.loaded.emit(weather);
          this.cdr.markForCheck();
        },
        // Non-essential ornament: on failure the widget stays absent, no fuss.
        error: () => {},
      });
  }
}
