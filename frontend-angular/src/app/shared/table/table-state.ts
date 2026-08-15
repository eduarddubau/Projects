import { Signal, computed, linkedSignal, signal } from '@angular/core';
import { PageEvent } from '@angular/material/paginator';
import { Sort } from '@angular/material/sort';

export interface TableOptions<T> {
  /**
   * The rows to show, read reactively — the table then follows a refetch or a
   * local edit with no effect() copying anything across. Guard a resource with
   * `hasValue()`: `value()` throws while the resource is in its error state.
   */
  source: () => readonly T[];
  /** The text the search box matches against, already concatenated. */
  searchText: (row: T) => string;
  /** Return the value to sort a row by. Columns this doesn't handle stay unsorted. */
  sortValue?: (row: T, column: string) => string | number;
  pageSize?: number;
}

/**
 * Client-side filter, sort and page for a Material table, as signals.
 *
 * The table binds `pageRows()` directly, so there is no ViewChild to wire up in
 * ngAfterViewInit and no ChangeDetectorRef to nudge afterwards. Currently used by
 * the admin tables; the user-facing project pages still use ProjectsDataSource.
 */
export class TableState<T> {
  private readonly options: TableOptions<T>;

  readonly source: Signal<readonly T[]>;
  readonly search = signal('');
  readonly sort = signal<Sort>({ active: '', direction: '' });
  readonly pageSize = signal(10);

  /** An extra predicate the page owns, such as the trash age filter. */
  readonly filter = signal<((row: T) => boolean) | null>(null);

  constructor(options: TableOptions<T>) {
    this.options = options;
    this.source = computed(options.source);
    if (options.pageSize) this.pageSize.set(options.pageSize);
  }

  /** Everything matching the search and filter, sorted — across all pages. */
  readonly matching: Signal<readonly T[]> = computed(() => {
    const term = this.search();
    const extra = this.filter();

    const matched = this.source().filter(
      (row) =>
        (term === '' || this.options.searchText(row).toLowerCase().includes(term)) &&
        (extra === null || extra(row)),
    );

    return this.sorted(matched);
  });

  readonly total = computed(() => this.matching().length);

  /**
   * Clamps rather than resets, so purging the last row of page 3 lands on page 2
   * instead of throwing the reader back to the start. Narrowing the result set
   * through search or sort resets to 0 explicitly, in the setters below.
   */
  readonly pageIndex = linkedSignal<number, number>({
    source: () => Math.max(0, Math.ceil(this.total() / this.pageSize()) - 1),
    computation: (lastPage, previous) => Math.min(previous?.value ?? 0, lastPage),
  });

  /** The rows actually on screen. Bind this as the table's dataSource. */
  readonly pageRows: Signal<readonly T[]> = computed(() => {
    const start = this.pageIndex() * this.pageSize();
    return this.matching().slice(start, start + this.pageSize());
  });

  /** True when the source holds rows but the search or filter hid all of them. */
  readonly hasNoMatches = computed(() => this.source().length > 0 && this.total() === 0);

  readonly isEmpty = computed(() => this.source().length === 0);

  setSearch(term: string | null): void {
    this.search.set(term?.trim().toLowerCase() ?? '');
    this.pageIndex.set(0);
  }

  setSort(sort: Sort): void {
    this.sort.set(sort);
    this.pageIndex.set(0);
  }

  setFilter(predicate: ((row: T) => boolean) | null): void {
    this.filter.set(predicate);
    this.pageIndex.set(0);
  }

  setPage(event: PageEvent): void {
    this.pageSize.set(event.pageSize);
    this.pageIndex.set(event.pageIndex);
  }

  /** The 1-based number to show in an index column, accounting for the page. */
  rowNumber(indexOnPage: number): number {
    return this.pageIndex() * this.pageSize() + indexOnPage + 1;
  }

  private sorted(rows: readonly T[]): readonly T[] {
    const { active, direction } = this.sort();
    const sortValue = this.options.sortValue;

    if (!active || direction === '' || !sortValue) return rows;

    const factor = direction === 'asc' ? 1 : -1;

    return [...rows].sort((a, b) => {
      const left = sortValue(a, active);
      const right = sortValue(b, active);
      if (left === right) return 0;
      return (left < right ? -1 : 1) * factor;
    });
  }
}
