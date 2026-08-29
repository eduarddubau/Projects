import { signal } from '@angular/core';
import { AppConfigService, TrashWindow } from '@core/services/app-config.service';
import { pluralCategory } from '@core/utils/plural';

/**
 * A trash window that has already arrived, for specs whose subject is not the config.
 *
 * Without it every trash spec waits on GET /config forever, since `whenStable` counts the
 * pending resource. Pass null for the branch where the fetch never answered.
 */
export function provideAppConfigTesting(days: number | null = 30) {
  const window: TrashWindow | undefined =
    days === null ? undefined : { days, plural: pluralCategory(days, 'en-US') };

  return {
    provide: AppConfigService,
    useValue: {
      trashWindowDays: signal(days ?? undefined),
      trashWindow: signal(window),
    },
  };
}
