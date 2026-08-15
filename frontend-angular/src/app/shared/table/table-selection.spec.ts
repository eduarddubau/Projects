import { signal } from '@angular/core';

import { TableSelection } from './table-selection';

interface Row {
  id: string;
}

const all: Row[] = [{ id: 'a' }, { id: 'b' }, { id: 'c' }, { id: 'd' }];

describe('TableSelection', () => {
  let matching: ReturnType<typeof signal<readonly Row[]>>;
  let onPage: ReturnType<typeof signal<readonly Row[]>>;
  let selection: TableSelection<Row>;

  beforeEach(() => {
    matching = signal<readonly Row[]>(all);
    onPage = signal<readonly Row[]>(all.slice(0, 2));
    selection = new TableSelection(matching, onPage);
  });

  it('toggles a row on and back off', () => {
    selection.toggle(all[0]);
    expect(selection.isSelected(all[0])).toBe(true);
    expect(selection.count()).toBe(1);

    selection.toggle(all[0]);
    expect(selection.isSelected(all[0])).toBe(false);
    expect(selection.isEmpty()).toBe(true);
  });

  it('select-all reaches only the rows on screen', () => {
    selection.toggleAllOnPage();

    expect(selection.count()).toBe(2);
    expect(selection.allOnPageSelected()).toBe(true);
    expect(selection.isSelected(all[2])).toBe(false);
  });

  it('a second select-all clears the page again', () => {
    selection.toggleAllOnPage();
    selection.toggleAllOnPage();

    expect(selection.isEmpty()).toBe(true);
  });

  // Selecting on page 1 then paging away must not silently drop the selection,
  // or a bulk action would act on less than the count promised.
  it('keeps a selection made on another page', () => {
    selection.toggleAllOnPage();
    onPage.set(all.slice(2, 4));

    expect(selection.count()).toBe(2);
    expect(selection.allOnPageSelected()).toBe(false);
    expect(selection.indeterminate()).toBe(true);
  });

  // The rows arrive from an httpResource, so a refetch replaces every object.
  it('survives the source being replaced with equal-but-not-identical rows', () => {
    selection.toggle(all[0]);
    matching.set(all.map((row) => ({ ...row })));

    expect(selection.count()).toBe(1);
  });

  it('drops a row that no longer matches', () => {
    selection.toggle(all[0]);
    matching.set(all.slice(1));

    expect(selection.count()).toBe(0);
  });

  it('deselects the rows an action consumed', () => {
    selection.toggleAllOnPage();
    selection.deselect([all[0]]);

    expect(selection.count()).toBe(1);
    expect(selection.isSelected(all[1])).toBe(true);
  });

  it('is not indeterminate when nothing is selected', () => {
    expect(selection.indeterminate()).toBe(false);
    expect(selection.allOnPageSelected()).toBe(false);
  });
});
