import { Signal, computed, signal } from '@angular/core';

/**
 * Row selection for bulk table actions.
 *
 * Holds ids rather than the row objects the CDK SelectionModel would: the tables
 * are fed by an httpResource, so a refetch replaces every object and identity-based
 * selection would silently empty itself.
 */
export class TableSelection<T extends { id: string }> {
  private readonly ids = signal<ReadonlySet<string>>(new Set());

  /**
   * @param matching every row passing the current filters, so a selection made on
   *   one page survives a visit to another.
   * @param onPage the rows on screen, which is as far as "select all" may reach.
   */
  constructor(
    private readonly matching: Signal<readonly T[]>,
    private readonly onPage: Signal<readonly T[]>,
  ) {}

  readonly selected = computed(() => this.matching().filter((row) => this.ids().has(row.id)));

  readonly count = computed(() => this.selected().length);

  readonly isEmpty = computed(() => this.count() === 0);

  readonly allOnPageSelected = computed(() => {
    const page = this.onPage();
    const ids = this.ids();
    return page.length > 0 && page.every((row) => ids.has(row.id));
  });

  readonly indeterminate = computed(() => this.count() > 0 && !this.allOnPageSelected());

  isSelected(row: T): boolean {
    return this.ids().has(row.id);
  }

  toggle(row: T): void {
    this.ids.update((ids) => {
      const next = new Set(ids);
      if (!next.delete(row.id)) next.add(row.id);
      return next;
    });
  }

  toggleAllOnPage(): void {
    const page = this.onPage();
    const selectAll = !this.allOnPageSelected();

    this.ids.update((ids) => {
      const next = new Set(ids);
      for (const row of page) {
        if (selectAll) next.add(row.id);
        else next.delete(row.id);
      }
      return next;
    });
  }

  deselect(rows: readonly T[]): void {
    this.ids.update((ids) => {
      const next = new Set(ids);
      for (const row of rows) next.delete(row.id);
      return next;
    });
  }

  clear(): void {
    this.ids.set(new Set());
  }
}
