import { DataSource } from '@angular/cdk/collections';
import { MatPaginator } from '@angular/material/paginator';
import { MatSort } from '@angular/material/sort';
import { map } from 'rxjs/operators';
import { Observable, of as observableOf, merge, Subject } from 'rxjs';

export interface BaseEntity {
  id: number;
  name: string;
  createdAt: string;
  isActive: boolean;
}

export interface TableState {
  searchQuery: string;
  showOnlyActive: boolean;
}

const EXAMPLE_DATA: BaseEntity[] = [
  { id: 1, name: 'Hydrogen', createdAt: '2023-01-01', isActive: true },
  { id: 2, name: 'Helium', createdAt: '2023-01-02', isActive: true },
  { id: 3, name: 'Lithium', createdAt: '2023-01-03', isActive: false },
  { id: 4, name: 'Beryllium', createdAt: '2023-01-04', isActive: true },
  { id: 5, name: 'Boron', createdAt: '2023-01-05', isActive: true },
  { id: 6, name: 'Carbon', createdAt: '2023-01-06', isActive: true },
  { id: 7, name: 'Nitrogen', createdAt: '2023-01-07', isActive: false },
  { id: 8, name: 'Oxygen', createdAt: '2023-01-08', isActive: true },
  { id: 9, name: 'Fluorine', createdAt: '2023-01-09', isActive: true },
  { id: 10, name: 'Neon', createdAt: '2023-01-10', isActive: false },
  { id: 11, name: 'Sodium', createdAt: '2023-01-11', isActive: true },
  { id: 12, name: 'Magnesium', createdAt: '2023-01-12', isActive: true },
  { id: 13, name: 'Aluminum', createdAt: '2023-01-13', isActive: false },
  { id: 14, name: 'Silicon', createdAt: '2023-01-14', isActive: true },
  { id: 15, name: 'Phosphorus', createdAt: '2023-01-15', isActive: true },
  { id: 16, name: 'Sulfur', createdAt: '2023-01-16', isActive: true },
  { id: 17, name: 'Chlorine', createdAt: '2023-01-17', isActive: false },
  { id: 18, name: 'Argon', createdAt: '2023-01-18', isActive: true },
  { id: 19, name: 'Potassium', createdAt: '2023-01-19', isActive: true },
  { id: 20, name: 'Calcium', createdAt: '2023-01-20', isActive: false },
];

/**
 * Data source for the EntityList view. This class should
 * encapsulate all logic for fetching and manipulating the displayed data
 * (including sorting, pagination, and filtering).
 */
export class EntityListDataSource extends DataSource<BaseEntity> {

  data: BaseEntity[] = EXAMPLE_DATA;
  paginator: MatPaginator | undefined;
  sort: MatSort | undefined;
  state: TableState = { searchQuery: '', showOnlyActive: false };
  private filterChange = new Subject<void>();

  constructor() {
    super();
  }

  /**
   * Connect this data source to the table. The table will only update when
   * the returned stream emits new items.
   * @returns A stream of the items to be rendered.
   */
  connect(): Observable<BaseEntity[]> {
    if (!this.paginator || !this.sort) {
      throw Error('Please set the paginator and sort on the data source before connecting.');
    }

    return merge(
      observableOf(this.data), 
      this.paginator.page, 
      this.sort.sortChange,
      this.filterChange
    ).pipe(
      map(() => this._getProcessedData([...this.data]))
    );
  }

  private _getProcessedData(data: BaseEntity[]): BaseEntity[] {
    let result = data;

    result = result.filter(item => {
      const matchesSearch = item.name.toLowerCase().includes(this.state.searchQuery.toLowerCase());
      const matchesActive = this.state.showOnlyActive ? item.isActive : true;
      return matchesSearch && matchesActive;
    });

    result = this.getSortedData(result);

    result = this.getPagedData(result);

    return result;
  }

  /**
   * Triggers a filter update and refreshes the data.
   */
  updateFilter() {
    if (!this.paginator || !this.sort) {
      return;
    }
    this.filterChange.next();
  }

  /**
   *  Called when the table is being destroyed. Use this function, to clean up
   * any open connections or free any held resources that were set up during connect.
   */
  disconnect(): void {}

  /**
   * Paginate the data (client-side). If you're using server-side pagination,
   * this would be replaced by requesting the appropriate data from the server.
   */
  private getPagedData(data: BaseEntity[]): BaseEntity[] {
    if (this.paginator) {
      const startIndex = this.paginator.pageIndex * this.paginator.pageSize;
      return data.splice(startIndex, this.paginator.pageSize);
    } else {
      return data;
    }
  }

  /**
   * Sort the data (client-side). If you're using server-side sorting,
   * this would be replaced by requesting the appropriate data from the server.
   */
  private getSortedData(data: BaseEntity[]): BaseEntity[] {
    if (!this.sort || !this.sort.active || this.sort.direction === '') {
      return data;
    }

    return data.sort((a, b) => {
      const isAsc = this.sort?.direction === 'asc';
      switch (this.sort?.active) {
        case 'name':
          return compare(a.name, b.name, isAsc);
        case 'id':
          return compare(+a.id, +b.id, isAsc);
        default:
          return 0;
      }
    });
  }
}

/** Simple sort comparator for example ID/Name columns (for client-side sorting). */
function compare(a: string | number, b: string | number, isAsc: boolean): number {
  return (a < b ? -1 : 1) * (isAsc ? 1 : -1);
}
