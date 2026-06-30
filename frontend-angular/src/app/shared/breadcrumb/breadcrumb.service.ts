import { Injectable, signal } from '@angular/core';

/**
 * Overrides the last breadcrumb's label with a dynamic value (e.g. the name of
 * the record being viewed) instead of the route's static `data.breadcrumb`.
 * The owning component sets the label once its data loads and clears it (null)
 * on destroy so it doesn't leak to the next page.
 */
@Injectable({ providedIn: 'root' })
export class BreadcrumbService {
  readonly leafLabel = signal<string | null>(null);

  setLeafLabel(label: string | null): void {
    this.leafLabel.set(label);
  }
}
