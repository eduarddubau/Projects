import {
  AfterViewInit, Component, ViewChild, inject,
  DestroyRef, ChangeDetectionStrategy, OnInit,
  ChangeDetectorRef, effect
} from '@angular/core';
import { SelectionModel } from '@angular/cdk/collections';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, MatPaginator, MatPaginatorIntl } from '@angular/material/paginator';
import { MatSortModule, MatSort } from '@angular/material/sort';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { DatePipe } from '@angular/common';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { debounceTime, distinctUntilChanged, startWith } from 'rxjs/operators';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { TranslocoPaginatorIntl } from '@core/i18n/transloco-paginator-intl';
import { ProjectsDataSource } from '@features/projects/projects-datasource';
import { ProjectService } from '@core/services/project.service';
import { Project } from '@core/models/project';
import { ConfirmDialogComponent } from '@shared/confirm-dialog/confirm-dialog.component';

type AgeFilter = 'all' | '30' | '60' | '90';

@Component({
  selector: 'app-trash-projects',
  templateUrl: './trash-projects.component.html',
  imports: [
    MatTableModule,
    MatPaginatorModule,
    MatSortModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonToggleModule,
    MatCheckboxModule,
    MatProgressSpinnerModule,
    MatIconModule,
    MatButtonModule,
    ReactiveFormsModule,
    DatePipe,
    TranslocoDirective
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [{ provide: MatPaginatorIntl, useClass: TranslocoPaginatorIntl }]
})
export class TrashProjectsComponent implements OnInit, AfterViewInit {
  @ViewChild(MatPaginator) paginator!: MatPaginator;
  @ViewChild(MatSort) sort!: MatSort;

  private projectService = inject(ProjectService);
  private cdr = inject(ChangeDetectorRef);
  private destroyRef = inject(DestroyRef);
  private dialog = inject(MatDialog);
  private snackBar = inject(MatSnackBar);
  private transloco = inject(TranslocoService);

  deleted = this.projectService.allDeletedProjects();
  selection = new SelectionModel<Project>(true, []);

  dataSource = new ProjectsDataSource();
  displayedColumns = ['select', 'index', 'name', 'createdBy', 'deletedAt', 'actions'];
  searchControl = new FormControl('');
  ageFilterControl = new FormControl<AgeFilter>('all');

  constructor() {
    // The table reads a plain property, not a signal, so the resource's value has
    // to be pushed across; every local edit below goes through the resource too,
    // which keeps this the single place the table is filled.
    effect(() => {
      // value() throws while the resource is in its error state, and an effect
      // that throws takes change detection down with it.
      if (!this.deleted.hasValue()) return;

      this.dataSource.data = this.deleted.value();
      this.dataSource.triggerUpdate();
      this.cdr.markForCheck();
    });
  }

  ngOnInit() {
    this.searchControl.valueChanges.pipe(
      startWith(this.searchControl.value),
      debounceTime(300),
      distinctUntilChanged(),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(() => this.applyFilters());

    this.ageFilterControl.valueChanges.pipe(
      startWith(this.ageFilterControl.value),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(() => this.applyFilters());
  }

  ngAfterViewInit(): void {
    this.dataSource.sort = this.sort;
    this.dataSource.paginator = this.paginator;
    this.dataSource.triggerUpdate();
    this.cdr.detectChanges();
  }

  private applyFilters(): void {
    const ageFilter = this.ageFilterControl.value;
    this.dataSource.state = {
      searchQuery: this.searchControl.value?.trim().toLowerCase() ?? '',
      minAgeDays: ageFilter && ageFilter !== 'all' ? Number(ageFilter) : undefined
    };
    if (this.dataSource.paginator) {
      this.dataSource.paginator.pageIndex = 0;
    }
    this.cdr.markForCheck();
  }

  /** Rows currently rendered on screen — "select all" must never reach past this into other pages. */
  private get rowsInView(): Project[] {
    return this.dataSource.getCurrentPageData();
  }

  private get purgeableSelected(): Project[] {
    return this.selection.selected.filter(p => p.isPurgeable);
  }

  canPurgeSelected(): boolean {
    const selected = this.selection.selected;
    return selected.length > 0 && selected.every(p => p.isPurgeable);
  }

  isAllSelected(): boolean {
    const rows = this.rowsInView;
    return rows.length > 0 && rows.every(p => this.selection.isSelected(p));
  }

  toggleSelection(row: Project): void {
    this.selection.toggle(row);
    this.cdr.markForCheck();
  }

  toggleSelectAll(): void {
    const rows = this.rowsInView;
    if (this.isAllSelected()) {
      rows.forEach(p => this.selection.deselect(p));
    } else {
      this.selection.select(...rows);
    }
    this.cdr.markForCheck();
  }

  restoreProject(project: Project): void {
    this.restoreMany([project]);
  }

  confirmRestoreSelected(): void {
    const selected = this.selection.selected;
    if (selected.length === 0) return;

    this.restoreMany(selected);
  }

  private restoreMany(projects: Project[]): void {
    const ids = projects.map(p => p.id);
    this.projectService.restoreProjects(ids).subscribe({
      next: ({ restoredCount }) => {
        this.deleted.update((list) => list.filter((p) => !ids.includes(p.id)));
        projects.forEach(p => this.selection.deselect(p));
        this.dataSource.triggerUpdate();
        this.cdr.markForCheck();
        this.snackBar.open(
          this.transloco.translate(
            restoredCount === 1 ? 'admin.trashProjects.restoredOne' : 'admin.trashProjects.restoredMany',
            { count: restoredCount },
          ),
          this.transloco.translate('common.actions.close'),
          { duration: 3000 }
        );
      },
      error: () => this.snackBar.open(
        this.transloco.translate('admin.trashProjects.restoreFailed'),
        this.transloco.translate('common.actions.close'),
        { duration: 5000 },
      )
    });
  }

  confirmPurge(project: Project): void {
    this.purge([project], this.transloco.translate('admin.trashProjects.purgeMessageNamed', { name: project.name }));
  }

  confirmPurgeSelected(): void {
    const purgeable = this.purgeableSelected;
    if (purgeable.length === 0) return;

    this.purge(
      purgeable,
      this.transloco.translate(
        purgeable.length === 1 ? 'admin.trashProjects.purgeMessageOne' : 'admin.trashProjects.purgeMessageMany',
        { count: purgeable.length },
      )
    );
  }

  private purge(projects: Project[], message: string): void {
    this.dialog.open(ConfirmDialogComponent, {
      width: '420px',
      data: {
        title: this.transloco.translate(
          projects.length > 1 ? 'admin.trashProjects.purgeTitleMany' : 'admin.trashProjects.purgeTitleOne',
        ),
        message,
        confirmLabel: this.transloco.translate('common.actions.purge'),
        warn: true
      }
    })
      .afterClosed()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((confirmed: boolean | undefined) => {
        if (!confirmed) return;

        const ids = projects.map(p => p.id);
        this.projectService.purgeProjects(ids).subscribe({
          next: ({ purgedCount }) => {
            this.deleted.update((list) => list.filter((p) => !ids.includes(p.id)));
            projects.forEach(p => this.selection.deselect(p));
            this.dataSource.triggerUpdate();
            this.cdr.markForCheck();
            this.snackBar.open(
              this.transloco.translate(
                purgedCount === 1 ? 'admin.trashProjects.purgedOne' : 'admin.trashProjects.purgedMany',
                { count: purgedCount },
              ),
              this.transloco.translate('common.actions.close'),
              { duration: 3000 }
            );
          },
          error: () => this.snackBar.open(
            this.transloco.translate('admin.trashProjects.purgeFailed'),
            this.transloco.translate('common.actions.close'),
            { duration: 5000 },
          )
        });
      });
  }
}
