import { AfterViewInit, Component, ViewChild, inject, DestroyRef, ChangeDetectionStrategy, OnInit } from '@angular/core';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, MatPaginator } from '@angular/material/paginator';
import { MatSortModule, MatSort } from '@angular/material/sort';
import { MatFormFieldModule } from '@angular/material/form-field';
import { EntityListDataSource } from './entity-list-datasource';
import { combineLatestWith, debounceTime, distinctUntilChanged, startWith } from 'rxjs/operators';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatInputModule } from '@angular/material/input';
import { ChangeDetectorRef } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { DatePipe } from '@angular/common';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';

@Component({
  selector: 'app-entity-list',
  templateUrl: './entity-list.component.html',
  styleUrl: './entity-list.component.scss',
  imports: [MatTableModule, MatPaginatorModule, MatSortModule, MatFormFieldModule, ReactiveFormsModule, MatInputModule, DatePipe, MatSlideToggleModule],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class EntityListComponent implements OnInit, AfterViewInit {
  @ViewChild(MatPaginator) paginator!: MatPaginator;
  @ViewChild(MatSort) sort!: MatSort;

  dataSource = new EntityListDataSource();
  displayedColumns = ['index', 'id', 'name', 'createdAt'];
  searchControl = new FormControl('');
  filterActiveControl = new FormControl(false);

  private cdr = inject(ChangeDetectorRef);
  private destroyRef = inject(DestroyRef);

  ngOnInit() {

    // Combine search and filter changes
    this.searchControl.valueChanges.pipe(
      startWith(this.searchControl.value),
      debounceTime(300),
      distinctUntilChanged(),
      // Combine with the active filter changes
      combineLatestWith(
        this.filterActiveControl.valueChanges.pipe(startWith(this.filterActiveControl.value))
      ),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(([searchTerm, showOnlyActive]) => {
      
      // Update the data source state
      this.dataSource.state = {
        searchQuery: searchTerm?.trim().toLowerCase() || '',
        showOnlyActive: !!showOnlyActive
      };

      // Reset to the first page whenever filters change
      if (this.dataSource.paginator) {
        this.dataSource.paginator.pageIndex = 0;
      }

      this.cdr.markForCheck();
    });
  }

  ngAfterViewInit(): void {
    this.dataSource.paginator = this.paginator;
    this.dataSource.sort = this.sort;

    // Listen to sort changes
    this.sort.sortChange.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => {
      if (this.dataSource.paginator) this.dataSource.paginator.pageIndex = 0;
      this.dataSource.triggerUpdate();
    });

    // Listen to page changes
    this.paginator.page.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => {
      this.dataSource.triggerUpdate();
    });

    // Initial trigger to load data
    this.dataSource.triggerUpdate();
    
    // Ensure the view updates after setting up everything
    this.cdr.detectChanges();
  }
}