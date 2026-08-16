import { confirmPhraseFor } from './confirm-phrase';

const NAME_LABEL = 'Project name';
const COUNT_LABEL = 'Number of items';

function rows(...names: string[]) {
  return names.map((name) => ({ name }));
}

describe('confirmPhraseFor', () => {
  it('asks for the name when there is exactly one row', () => {
    expect(confirmPhraseFor(rows('Acme Website'), NAME_LABEL, COUNT_LABEL)).toEqual({
      confirmPhrase: 'Acme Website',
      confirmPhraseLabel: NAME_LABEL,
    });
  });

  it('asks for the count when there is more than one', () => {
    expect(confirmPhraseFor(rows('a', 'b', 'c'), NAME_LABEL, COUNT_LABEL)).toEqual({
      confirmPhrase: '3',
      confirmPhraseLabel: COUNT_LABEL,
    });
  });

  // Digits rather than a keyword, so the phrase never needs translating.
  it('gives the count as digits', () => {
    expect(confirmPhraseFor(rows('a', 'b'), NAME_LABEL, COUNT_LABEL).confirmPhrase).toBe('2');
  });

  // Guards against an empty batch producing a phrase nobody can type: '0' is at
  // least typeable, where items[0].name would have thrown.
  it('does not read a name off an empty batch', () => {
    expect(confirmPhraseFor([], NAME_LABEL, COUNT_LABEL)).toEqual({
      confirmPhrase: '0',
      confirmPhraseLabel: COUNT_LABEL,
    });
  });
});
