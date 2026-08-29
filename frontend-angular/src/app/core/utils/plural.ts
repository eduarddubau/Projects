/** Transloco has no ICU plurals here, so keys carry a category suffix instead. */
export function pluralCategory(count: number, locale: string): 'one' | 'few' | 'other' {
  const category = new Intl.PluralRules(locale).select(count);
  return category === 'one' || category === 'few' ? category : 'other';
}
