import { DataSource } from '@angular/cdk/collections';
import { MatPaginator } from '@angular/material/paginator';
import { MatSort } from '@angular/material/sort';
import { Subject, Observable, Subscription } from 'rxjs';
import { map, startWith } from 'rxjs/operators';
import { Project } from '@core/models/project';

export interface ProjectTableState {
  searchQuery: string;
  minAgeDays?: number;
}

export class ProjectsDataSource extends DataSource<Project> {
  data: Project[] = [];

  private _paginator: MatPaginator | undefined;
  private _sort: MatSort | undefined;
  private _state: ProjectTableState = { searchQuery: '' };
  private _updateStream = new Subject<void>();
  private _sortSub: Subscription | undefined;
  private _pageSub: Subscription | undefined;

  set paginator(paginator: MatPaginator | undefined) {
    this._paginator = paginator;
    this._rewireControls();
  }

  get paginator(): MatPaginator | undefined {
    return this._paginator;
  }

  set sort(sort: MatSort | undefined) {
    this._sort = sort;
    this._rewireControls();
  }

  get sort(): MatSort | undefined {
    return this._sort;
  }

  set state(newState: ProjectTableState) {
    this._state = newState;
    this._updateStream.next();
  }

  get state(): ProjectTableState {
    return this._state;
  }

  connect(): Observable<Project[]> {
    return this._updateStream.pipe(
      startWith(undefined),
      map(() => this._getProcessedData([...this.data]))
    );
  }

  disconnect(): void {
    this._sortSub?.unsubscribe();
    this._pageSub?.unsubscribe();
    this._updateStream.complete();
  }

  triggerUpdate(): void {
    this._updateStream.next();
  }

  /**
   * Re-subscribes whenever sort or paginator are (re)assigned. Sort and page
   * changes are handled separately because only a sort change should reset
   * back to the first page — resetting on every page-change event would
   * undo the page change itself.
   */
  private _rewireControls(): void {
    this._sortSub?.unsubscribe();
    this._pageSub?.unsubscribe();

    this._sortSub = this._sort?.sortChange.subscribe(() => {
      if (this._paginator) this._paginator.pageIndex = 0;
      this._updateStream.next();
    });

    this._pageSub = this._paginator?.page.subscribe(() => {
      this._updateStream.next();
    });
  }

  private _getProcessedData(data: Project[]): Project[] {
    let result = this._getFilteredData(data);

    if (this.sort?.active && this.sort.direction !== '') {
      result = this._getSortedData(result);
    }

    if (this.paginator) {
      this.paginator.length = result.length;
      const maxPage = Math.ceil(result.length / this.paginator.pageSize) - 1;
      if (this.paginator.pageIndex > maxPage && maxPage >= 0) {
        this.paginator.pageIndex = 0;
      }
      return this._getPagedData(result);
    }

    return result;
  }

  /**
   * Returns exactly what's currently rendered in the table — filtered,
   * sorted, and paginated. "Select all" style actions should use this
   * rather than the full filtered set, so the count never exceeds what's
   * actually visible/checked on screen.
   */
  getCurrentPageData(): Project[] {
    return this._getProcessedData([...this.data]);
  }

  private _getFilteredData(data: Project[]): Project[] {
    return data.filter(item => {
      const matchesSearch = item.name.toLowerCase().includes(this._state.searchQuery.toLowerCase());
      const matchesAge = this._state.minAgeDays == null || this._isOlderThan(item, this._state.minAgeDays);
      return matchesSearch && matchesAge;
    });
  }

  private _isOlderThan(item: Project, days: number): boolean {
    if (!item.deletedAt) return false;
    const cutoff = Date.now() - days * 24 * 60 * 60 * 1000;
    return new Date(item.deletedAt).getTime() < cutoff;
  }

  private _getPagedData(data: Project[]): Project[] {
    if (this.paginator) {
      const startIndex = this.paginator.pageIndex * this.paginator.pageSize;
      return data.slice(startIndex, startIndex + this.paginator.pageSize);
    }
    return data;
  }

  private _getSortedData(data: Project[]): Project[] {
    if (!this.sort?.active || this.sort.direction === '') return data;

    return [...data].sort((a, b) => {
      const isAsc = this.sort?.direction === 'asc';
      switch (this.sort?.active) {
        case 'name':        return compare(a.name, b.name, isAsc);
        case 'createdAt':   return compare(a.createdAt, b.createdAt, isAsc);
        case 'createdBy':   return compare(a.createdByDisplayName ?? '', b.createdByDisplayName ?? '', isAsc);
        case 'deletedAt':   return compare(a.deletedAt ?? '', b.deletedAt ?? '', isAsc);
        default:            return 0;
      }
    });
  }
}

function compare(a: string | number, b: string | number, isAsc: boolean): number {
  return (a < b ? -1 : 1) * (isAsc ? 1 : -1);
}