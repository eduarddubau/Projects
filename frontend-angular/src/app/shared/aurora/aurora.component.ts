import { Component, ChangeDetectionStrategy } from '@angular/core';

// Decorative top-corner glow behind page content. Drop <app-aurora /> as the
// first child of a positioned scroll surface; content painted after it wins.
// Eased multi-stop falloffs + the noise overlay keep it from banding.
@Component({
  selector: 'app-aurora',
  template: '<div class="aurora" aria-hidden="true"></div>',
  styles: `
    :host {
      display: contents;
    }

    .aurora {
      position: absolute;
      inset: 0 0 auto;
      height: 520px;
      pointer-events: none;
      background:
        radial-gradient(
          680px 420px at 10% -12%,
          color-mix(in srgb, var(--mat-sys-primary) 11%, transparent) 0%,
          color-mix(in srgb, var(--mat-sys-primary) 8%, transparent) 28%,
          color-mix(in srgb, var(--mat-sys-primary) 4%, transparent) 48%,
          color-mix(in srgb, var(--mat-sys-primary) 1%, transparent) 64%,
          transparent 82%
        ),
        radial-gradient(
          760px 460px at 94% -16%,
          color-mix(in srgb, var(--mat-sys-tertiary) 9%, transparent) 0%,
          color-mix(in srgb, var(--mat-sys-tertiary) 6%, transparent) 30%,
          color-mix(in srgb, var(--mat-sys-tertiary) 3%, transparent) 52%,
          color-mix(in srgb, var(--mat-sys-tertiary) 1%, transparent) 68%,
          transparent 84%
        );
      mask-image: linear-gradient(to bottom, black 35%, transparent 94%);

      &::after {
        content: '';
        position: absolute;
        inset: 0;
        background: var(--app-noise);
        mask-image: inherit;
      }
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AuroraComponent {}
