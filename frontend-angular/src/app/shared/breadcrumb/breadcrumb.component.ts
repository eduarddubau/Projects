import { Component, inject, signal } from '@angular/core';
import { Router, NavigationEnd, ActivatedRouteSnapshot, RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatIconModule } from '@angular/material/icon';
import { filter } from 'rxjs';

interface Crumb {
  label: string;
  url: string;
}

@Component({
  selector: 'app-breadcrumb',
  imports: [RouterLink, MatIconModule],
  templateUrl: './breadcrumb.component.html',
  styleUrl: './breadcrumb.component.scss',
})
export class BreadcrumbComponent {
  private router = inject(Router);

  crumbs = signal<Crumb[]>([]);

  constructor() {
    this.router.events
      .pipe(
        filter((e) => e instanceof NavigationEnd),
        takeUntilDestroyed()
      )
      .subscribe(() => this.crumbs.set(this.build()));

    // Build once for the initial route (e.g. on a hard refresh).
    this.crumbs.set(this.build());
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
