export interface CurrentWeather {
  city: string;
  country: string;
  /** Rounded °C. */
  temperature: number;
  /** Relative humidity, %. */
  humidity: number;
  /** Rounded wind speed, km/h. */
  windSpeed: number;
  isDay: boolean;
  weatherCode: number;
  /** i18n suffix under `weather.conditions.*`. */
  conditionKey: string;
  /** Meteocons icon base name; resolved to `weather/<icon>.svg`. */
  icon: string;
}

interface CodeDescriptor {
  key: string;
  day: string;
  /** Falls back to `day` when the icon doesn't read differently at night. */
  night?: string;
}

// WMO interpretation codes (Open-Meteo `weather_code`) folded into the handful
// of conditions we surface. Icon names are Meteocons (bundled under
// public/weather/) — full-colour animated SVGs.
const WMO: Record<number, CodeDescriptor> = {
  0: { key: 'clear', day: 'clear-day', night: 'clear-night' },
  1: { key: 'mostlyClear', day: 'clear-day', night: 'clear-night' },
  2: { key: 'partlyCloudy', day: 'partly-cloudy-day', night: 'partly-cloudy-night' },
  3: { key: 'overcast', day: 'overcast-day', night: 'overcast-night' },
  45: { key: 'fog', day: 'fog-day', night: 'fog-night' },
  48: { key: 'fog', day: 'fog-day', night: 'fog-night' },
  51: { key: 'drizzle', day: 'drizzle' },
  53: { key: 'drizzle', day: 'drizzle' },
  55: { key: 'drizzle', day: 'drizzle' },
  56: { key: 'freezingDrizzle', day: 'sleet' },
  57: { key: 'freezingDrizzle', day: 'sleet' },
  61: { key: 'rain', day: 'rain' },
  63: { key: 'rain', day: 'rain' },
  65: { key: 'rain', day: 'rain' },
  66: { key: 'freezingRain', day: 'sleet' },
  67: { key: 'freezingRain', day: 'sleet' },
  71: { key: 'snow', day: 'snow' },
  73: { key: 'snow', day: 'snow' },
  75: { key: 'snow', day: 'snow' },
  77: { key: 'snow', day: 'snow' },
  80: { key: 'showers', day: 'rain' },
  81: { key: 'showers', day: 'rain' },
  82: { key: 'showers', day: 'rain' },
  85: { key: 'snowShowers', day: 'snow' },
  86: { key: 'snowShowers', day: 'snow' },
  95: { key: 'thunderstorm', day: 'thunderstorms-day-rain', night: 'thunderstorms-night-rain' },
  96: { key: 'thunderstormHail', day: 'thunderstorms-day-rain', night: 'thunderstorms-night-rain' },
  99: { key: 'thunderstormHail', day: 'thunderstorms-day-rain', night: 'thunderstorms-night-rain' },
};

/** Maps a WMO code to its condition i18n key and a day/night-aware icon name. */
export function describeWeather(code: number, isDay: boolean): { conditionKey: string; icon: string } {
  const descriptor = WMO[code] ?? { key: 'unknown', day: 'overcast-day', night: 'overcast-night' };
  const icon = !isDay && descriptor.night ? descriptor.night : descriptor.day;
  return { conditionKey: descriptor.key, icon };
}
