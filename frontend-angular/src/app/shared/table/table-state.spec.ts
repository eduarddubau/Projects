import { WritableSignal, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';

import { TableState } from './table-state';

interface Row {
  id: string;
  name: string;
  age: number;
}

function rows(count: number): Row[] {
  return Array.from({ length: count }, (_, i) => ({
    id: `id-${i}`,
    name: `Row ${String(i).padStart(2, '0')}`,
    age: i,
  }));
}

describe('TableState', () => {
  let table: TableState<Row>;
  let source: WritableSignal<readonly Row[]>;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    source = signal<readonly Row[]>([]);

    // linkedSignal needs an injection context to be created in.
    table = TestBed.runInInjectionContext(
      () =>
        new TableState<Row>({
          source: () => source(),
          searchText: (row) => row.name,
          sortValue: (row, column) => (column === 'age' ? row.age : row.name),
          pageSize: 10,
        }),
    );
  });

  it('pages the source, exposing the full count separately', () => {
    source.set(rows(25));

    expect(table.pageRows().length).toBe(10);
    expect(table.total()).toBe(25);
    expect(table.pageRows()[0].name).toBe('Row 00');

    table.setPage({ pageIndex: 2, pageSize: 10, length: 25 });

    expect(table.pageRows().length).toBe(5);
    expect(table.pageRows()[0].name).toBe('Row 20');
  });

  it('searches case-insensitively and resets to the first page', () => {
    source.set(rows(25));
    table.setPage({ pageIndex: 2, pageSize: 10, length: 25 });

    table.setSearch('  ROW 1  ');

    expect(table.pageIndex()).toBe(0);
    // Names are zero-padded, so this is Row 10 through Row 19.
    expect(table.total()).toBe(10);
  });

  it('sorts by the column the caller resolves, both directions', () => {
    source.set(rows(3));

    table.setSort({ active: 'age', direction: 'desc' });
    expect(table.pageRows().map((r) => r.age)).toEqual([2, 1, 0]);

    table.setSort({ active: 'age', direction: 'asc' });
    expect(table.pageRows().map((r) => r.age)).toEqual([0, 1, 2]);
  });

  it('leaves the order alone when the sort is cleared', () => {
    source.set(rows(3));
    table.setSort({ active: 'age', direction: '' });

    expect(table.pageRows().map((r) => r.age)).toEqual([0, 1, 2]);
  });

  it('applies the extra filter alongside the search', () => {
    source.set(rows(10));
    table.setFilter((row) => row.age >= 8);

    expect(table.total()).toBe(2);

    table.setSearch('Row 09');
    expect(table.total()).toBe(1);

    table.setFilter(null);
    expect(table.total()).toBe(1);
  });

  // Purging the last row of a page should not throw the reader back to the start.
  it('steps back one page when the last page empties, rather than to the start', () => {
    source.set(rows(21));
    table.setPage({ pageIndex: 2, pageSize: 10, length: 21 });
    expect(table.pageRows().length).toBe(1);

    source.set(rows(20));

    expect(table.pageIndex()).toBe(1);
    expect(table.pageRows().length).toBe(10);
  });

  it('holds the page when the source changes without shrinking past it', () => {
    source.set(rows(25));
    table.setPage({ pageIndex: 1, pageSize: 10, length: 25 });

    source.set(rows(24));

    expect(table.pageIndex()).toBe(1);
  });

  it('separates an empty source from a search that matched nothing', () => {
    expect(table.isEmpty()).toBe(true);
    expect(table.hasNoMatches()).toBe(false);

    source.set(rows(3));
    table.setSearch('nothing here');

    expect(table.isEmpty()).toBe(false);
    expect(table.hasNoMatches()).toBe(true);
  });

  it('numbers rows continuously across pages', () => {
    source.set(rows(25));
    expect(table.rowNumber(0)).toBe(1);

    table.setPage({ pageIndex: 2, pageSize: 10, length: 25 });
    expect(table.rowNumber(0)).toBe(21);
  });
});
