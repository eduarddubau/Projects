import { Injectable, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatPaginatorIntl } from '@angular/material/paginator';
import { TranslocoService } from '@jsverse/transloco';

// mat-paginator labels come from MatPaginatorIntl, not templates, so they
// need their own bridge to the active dictionary. Provided at component
// level by each table page (a root provider would pull the paginator into
// the initial bundle).
@Injectable()
export class TranslocoPaginatorIntl extends MatPaginatorIntl {
  private transloco = inject(TranslocoService);

  constructor() {
    super();

    this.getRangeLabel = (page, pageSize, length) => {
      if (length === 0 || pageSize === 0) {
        return this.transloco.translate('paginator.rangeEmpty', { length });
      }
      const start = page * pageSize;
      const end = Math.min(start + pageSize, length);
      return this.transloco.translate('paginator.rangeOf', { start: start + 1, end, length });
    };

    this.transloco.selectTranslateObject('paginator')
      .pipe(takeUntilDestroyed())
      .subscribe((labels) => {
        this.itemsPerPageLabel = labels['itemsPerPage'];
        this.nextPageLabel = labels['nextPage'];
        this.previousPageLabel = labels['previousPage'];
        this.firstPageLabel = labels['firstPage'];
        this.lastPageLabel = labels['lastPage'];
        this.changes.next();
      });
  }
}
