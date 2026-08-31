/** One rules object per locale: callers hit this from templates, so a fresh
 * Intl.PluralRules per call would be a locale-data lookup per change-detection pass. */
const rulesByLocale = new Map<string, Intl.PluralRules>();

/** Transloco has no ICU plurals here, so keys carry a category suffix instead. */
export function pluralCategory(count: number, locale: string): 'one' | 'few' | 'other' {
  let rules = rulesByLocale.get(locale);
  if (!rules) {
    rules = new Intl.PluralRules(locale);
    rulesByLocale.set(locale, rules);
  }

  const category = rules.select(count);
  return category === 'one' || category === 'few' ? category : 'other';
}

/** A translation key with its plural category appended — the scheme every count-dependent
 * label in the app uses. */
export function pluralKey(base: string, count: number, locale: string): string {
  return `${base}.${pluralCategory(count, locale)}`;
}
