import { DataSource } from '@angular/cdk/collections';
import { MatPaginator } from '@angular/material/paginator';
import { MatSort } from '@angular/material/sort';
import { Subject, Observable } from 'rxjs';
import { map } from 'rxjs/operators';

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

export class EntityListDataSource extends DataSource<BaseEntity> {
  data: BaseEntity[] = EXAMPLE_DATA;
  paginator: MatPaginator | undefined;
  sort: MatSort | undefined;

  // State management for search and active filter
  private _state: TableState = { searchQuery: '', showOnlyActive: false };
  
  // Subject to trigger data updates when state changes
  private updateStream = new Subject<void>(); 

  set state(newState: TableState) {
    this._state = newState;
    this.triggerUpdate();
  }

  get state(): TableState {
    return this._state;
  }

  constructor() {
    super();
  }

  // Connect function called by the table to retrieve one stream containing the data to render.
  connect(): Observable<BaseEntity[]> {
    return this.updateStream.pipe(
      map(() => {
        if (!this.paginator || !this.sort) return [];
        return this._getProcessedData([...this.data]);
      })
    );
  }

  // Clean up any resources when the table is destroyed.
  disconnect(): void {}

  // Method to trigger data update when state changes
  triggerUpdate(): void {
    this.updateStream.next();
  }

  // Process the data based on current state, sorting, and pagination
  private _getProcessedData(data: BaseEntity[]): BaseEntity[] {
    let result = this.getFilteredData(data);
    result = this.getSortedData(result);

    if (this.paginator) {
      this.paginator.length = result.length;
      const maxPage = Math.ceil(result.length / this.paginator.pageSize) - 1;
      if (this.paginator.pageIndex > maxPage && maxPage >= 0) {
        this.paginator.pageIndex = 0;
      }
    }
    return this.getPagedData(result);
  }

  // Apply filtering based on search query and active status
  private getFilteredData(data: BaseEntity[]): BaseEntity[] {
    return data.filter(item => {
      const matchesSearch = item.name.toLowerCase().includes(this.state.searchQuery.toLowerCase());
      const matchesActive = this.state.showOnlyActive ? item.isActive : true;
      return matchesSearch && matchesActive;
    });
  }

  // Apply pagination based on the current page index and page size
  private getPagedData(data: BaseEntity[]): BaseEntity[] {
    if (this.paginator) {
      const startIndex = this.paginator.pageIndex * this.paginator.pageSize;
      return data.slice(startIndex, startIndex + this.paginator.pageSize);
    }
    return data;
  }

  // Apply sorting based on the active sort and direction
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

// Simple sort comparator for example ID and Name columns (for client-side sorting).
function compare(a: string | number, b: string | number, isAsc: boolean): number {
  return (a < b ? -1 : 1) * (isAsc ? 1 : -1);
}
