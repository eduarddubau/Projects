import { Component, computed, inject, signal, ChangeDetectionStrategy } from '@angular/core';
import { Router, NavigationEnd, ActivatedRouteSnapshot, RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatIconModule } from '@angular/material/icon';
import { filter } from 'rxjs';
import { BreadcrumbService } from './breadcrumb.service';

interface Crumb {
  label: string;
  url: string;
}

@Component({
  selector: 'app-breadcrumb',
  imports: [RouterLink, MatIconModule],
  templateUrl: './breadcrumb.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrl: './breadcrumb.component.scss',
})
export class BreadcrumbComponent {
  private router = inject(Router);
  private breadcrumbService = inject(BreadcrumbService);

  private routerCrumbs = signal<Crumb[]>([]);

  // Replace the last crumb's label with the dynamic override (e.g. a record
  // name) when one is set; otherwise use the labels from the route.
  readonly crumbs = computed(() => {
    const crumbs = this.routerCrumbs();
    const leafLabel = this.breadcrumbService.leafLabel();
    if (leafLabel && crumbs.length) {
      const lastIndex = crumbs.length - 1;
      return crumbs.map((crumb, i) => (i === lastIndex ? { ...crumb, label: leafLabel } : crumb));
    }
    return crumbs;
  });

  constructor() {
    this.router.events
      .pipe(
        filter((e) => e instanceof NavigationEnd),
        takeUntilDestroyed(),
      )
      .subscribe(() => this.routerCrumbs.set(this.build()));

    // Build once for the initial route (e.g. on a hard refresh).
    this.routerCrumbs.set(this.build());
  }

  private build(): Crumb[] {
    const crumbs: Crumb[] = [];
    let route: ActivatedRouteSnapshot | null = this.router.routerState.snapshot.root;
    let url = '';

    while (route) {
      const segment = route.url.map((s) => s.path).join('/');
      if (segment) {
        url += `/${segment}`;
      }

      const label = route.data['breadcrumb'] as string | undefined;
      if (label) {
        crumbs.push({ label, url });
      }

      route = route.firstChild;
    }

    return crumbs;
  }
}
