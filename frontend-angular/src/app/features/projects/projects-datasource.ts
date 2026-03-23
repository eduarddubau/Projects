import { DataSource } from '@angular/cdk/collections';
import { MatPaginator } from '@angular/material/paginator';
import { MatSort } from '@angular/material/sort';
import { Subject, Observable } from 'rxjs';
import { map, startWith } from 'rxjs/operators';
import { Project } from '@core/models/project';

export interface ProjectTableState {
  searchQuery: string;
  showDeleted: boolean;
}

export class ProjectsDataSource extends DataSource<Project> {
  data: Project[] = [];
  paginator: MatPaginator | undefined;
  sort: MatSort | undefined;

  private _state: ProjectTableState = { searchQuery: '', showDeleted: false };
  private updateStream = new Subject<void>();

  set state(newState: ProjectTableState) {
    this._state = newState;
    this.triggerUpdate();
  }

  get state(): ProjectTableState {
    return this._state;
  }

  connect(): Observable<Project[]> {
    return this.updateStream.pipe(
      startWith(undefined),
      map(() => this._getProcessedData([...this.data]))
    );
  }

  disconnect(): void {}

  triggerUpdate(): void {
    this.updateStream.next();
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

  private _getFilteredData(data: Project[]): Project[] {
    return data.filter(item => {
      const matchesSearch = item.name.toLowerCase().includes(this._state.searchQuery.toLowerCase());
      const matchesDeleted = this._state.showDeleted ? true : !item.isDeleted;
      return matchesSearch && matchesDeleted;
    });
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
        default:            return 0;
      }
    });
  }
}

function compare(a: string | number, b: string | number, isAsc: boolean): number {
  return (a < b ? -1 : 1) * (isAsc ? 1 : -1);
}