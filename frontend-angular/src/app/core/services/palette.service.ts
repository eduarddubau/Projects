import { DOCUMENT, Injectable, PLATFORM_ID, inject, signal } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';

export type Palette = 'violet' | 'indigo' | 'emerald' | 'rose' | 'slate';

export const PALETTES: Palette[] = ['violet', 'indigo', 'emerald', 'rose', 'slate'];

// Swatch previews hold the scheme's primary across the first ~45% then blend to
// the accent, so the coin reads as its main colour (violet is violet) while
// still showing a gradient. Shared by the profile picker and the menu coins.
export const PALETTE_PREVIEWS: Record<Palette, string> = {
  violet: 'linear-gradient(135deg, #8b5cf6 0%, #8b5cf6 45%, #06b6d4 100%)',
  indigo: 'linear-gradient(135deg, #4f46e5 0%, #4f46e5 45%, #38bdf8 100%)',
  emerald: 'linear-gradient(135deg, #059669 0%, #059669 45%, #2dd4bf 100%)',
  rose: 'linear-gradient(135deg, #e11d48 0%, #e11d48 45%, #fbbf24 100%)',
  slate: 'linear-gradient(135deg, #334155 0%, #334155 45%, #94a3b8 100%)',
};

const STORAGE_KEY = 'palette';

// Accent color scheme, independent of light/dark. 'violet' is the built-in
// mat.theme default (no data-palette attribute); the others are token overrides
// in styles.scss. index.html re-applies the stored choice before first paint.
@Injectable({ providedIn: 'root' })
export class PaletteService {
  private document = inject(DOCUMENT);
  private isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  readonly palette = signal<Palette>(this.initial());

  set(palette: Palette): void {
    this.palette.set(palette);
    if (!this.isBrowser) return;

    const root = this.document.documentElement;
    if (palette === 'violet') {
      root.removeAttribute('data-palette');
    } else {
      root.dataset['palette'] = palette;
    }
    localStorage.setItem(STORAGE_KEY, palette);
  }

  private initial(): Palette {
    if (!this.isBrowser) return 'violet';
    const stored = localStorage.getItem(STORAGE_KEY);
    return PALETTES.includes(stored as Palette) ? (stored as Palette) : 'violet';
  }
}
