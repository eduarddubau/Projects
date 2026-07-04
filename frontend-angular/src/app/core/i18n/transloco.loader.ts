import { Injectable } from '@angular/core';
import { Translation, TranslocoLoader } from '@jsverse/transloco';
import { of } from 'rxjs';
import en from './en.json';
import ro from './ro.json';

const DICTIONARIES: Record<string, Translation> = { en, ro };

// Dictionaries are bundled statically so SSR and the first client paint
// always have translations available without an HTTP round-trip.
@Injectable({ providedIn: 'root' })
export class TranslocoStaticLoader implements TranslocoLoader {
  getTranslation(lang: string) {
    return of(DICTIONARIES[lang] ?? DICTIONARIES['en']);
  }
}
