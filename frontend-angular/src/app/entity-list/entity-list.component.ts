import { AfterViewInit, Component, ViewChild, inject, DestroyRef, ChangeDetectionStrategy, OnInit } from '@angular/core';
import { MatTableModule, MatTable } from '@angular/material/table';
import { MatPaginatorModule, MatPaginator } from '@angular/material/paginator';
import { MatSortModule, MatSort } from '@angular/material/sort';
import { MatFormFieldModule } from '@angular/material/form-field';
import { EntityListDataSource, BaseEntity } from './entity-list-datasource';
import { debounceTime, distinctUntilChanged } from 'rxjs/operators';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatInputModule } from '@angular/material/input';
import { ChangeDetectorRef } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { DatePipe } from '@angular/common';

@Component({
  selector: 'app-entity-list',
  templateUrl: './entity-list.component.html',
  styleUrl: './entity-list.component.scss',
  imports: [MatTableModule, MatPaginatorModule, MatSortModule, MatFormFieldModule, ReactiveFormsModule, MatInputModule, DatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class EntityListComponent implements OnInit, AfterViewInit {
  @ViewChild(MatPaginator) paginator!: MatPaginator;
  @ViewChild(MatSort) sort!: MatSort;

  dataSource = new EntityListDataSource();
  displayedColumns = ['id', 'name', 'createdAt'];
  searchControl = new FormControl('');

  private cdr = inject(ChangeDetectorRef);
  private destroyRef = inject(DestroyRef);

  ngOnInit() {
    this.searchControl.valueChanges.pipe(
      debounceTime(300),
      distinctUntilChanged(),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(value => {
      this.dataSource.state = { 
        ...this.dataSource.state, 
        searchQuery: value?.trim().toLowerCase() || '' 
      };
      
      if (this.dataSource.paginator) {
        this.dataSource.paginator.pageIndex = 0;
      }

      this.dataSource.triggerUpdate();

      this.cdr.markForCheck();
    });
  }

  ngAfterViewInit(): void {
    this.dataSource.paginator = this.paginator;
    this.dataSource.sort = this.sort;

    this.sort.sortChange.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => {
      if (this.dataSource.paginator) this.dataSource.paginator.pageIndex = 0;
      this.dataSource.triggerUpdate();
    });

    this.paginator.page.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => {
      this.dataSource.triggerUpdate();
    });

    this.dataSource.triggerUpdate();
    
    this.cdr.detectChanges();
  }
}