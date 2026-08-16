import { ConfirmDialogData } from './confirm-dialog.component';

type Phrase = Pick<ConfirmDialogData, 'confirmPhrase' | 'confirmPhraseLabel'>;

/**
 * What the caller must type back to confirm a destructive action on a batch.
 *
 * One row is confirmed by its name; a batch is confirmed by its size, because
 * there is no single name to type and the count is the blast radius. Digits also
 * spare the caller from typing a translated keyword.
 *
 * Labels arrive already translated — this stays free of DI so it can be tested
 * as the plain rule it is.
 */
export function confirmPhraseFor<T extends { name: string }>(
  items: readonly T[],
  nameLabel: string,
  countLabel: string,
): Phrase {
  return items.length === 1
    ? { confirmPhrase: items[0].name, confirmPhraseLabel: nameLabel }
    : { confirmPhrase: String(items.length), confirmPhraseLabel: countLabel };
}
