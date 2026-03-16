import { AfterViewInit, Component, ViewChild, inject, DestroyRef, ChangeDetectionStrategy } from '@angular/core';
import { MatTableModule, MatTable } from '@angular/material/table';;
import { MatPaginatorModule, MatPaginator } from '@angular/material/paginator';
import { MatSortModule, MatSort } from '@angular/material/sort';
import { MatFormFieldModule } from '@angular/material/form-field';
import { EntityListDataSource, BaseEntity } from './entity-list-datasource';
import { AsyncPipe, DatePipe} from '@angular/common';
import { debounceTime, distinctUntilChanged } from 'rxjs/operators';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatInputModule } from '@angular/material/input';
import { Observable } from 'rxjs/internal/Observable';
import { ChangeDetectorRef } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

@Component({
  selector: 'app-entity-list',
  templateUrl: './entity-list.component.html',
  styleUrl: './entity-list.component.scss',
  imports: [MatTableModule, MatPaginatorModule, MatSortModule, MatFormFieldModule, ReactiveFormsModule, MatInputModule, DatePipe, AsyncPipe],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class EntityListComponent implements AfterViewInit {
  @ViewChild(MatPaginator) paginator!: MatPaginator;
  @ViewChild(MatSort) sort!: MatSort;
  @ViewChild(MatTable) table!: MatTable<BaseEntity>;

  dataSource = new EntityListDataSource();
  displayedColumns = ['id', 'name', 'createdAt'];
  searchControl = new FormControl('');
  data$!: Observable<BaseEntity[]>;

  private cdr = inject(ChangeDetectorRef);
  private destroyRef = inject(DestroyRef);

  ngOnInit() {
  
    this.searchControl.valueChanges.pipe(
      debounceTime(300),
      distinctUntilChanged(),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(value => {

      this.dataSource.state.searchQuery = value?.trim().toLowerCase() || '';
      
      if (this.dataSource.paginator) {
        this.dataSource.paginator.pageIndex = 0;
      }
      
      this.dataSource.updateFilter();

      this.cdr.markForCheck();
    });
  }

  ngAfterViewInit(): void {
    this.dataSource.paginator = this.paginator;
    this.dataSource.sort = this.sort;
    
    this.data$ = this.dataSource.connect();

    this.table.dataSource = this.dataSource;
    
    this.cdr.detectChanges();
  }

}
