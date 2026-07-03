import { Component, inject, ChangeDetectionStrategy } from '@angular/core';
import { RouterOutlet, RouterLink, RouterLinkActive } from '@angular/router';
import { MatSidenavModule, MatDrawer } from '@angular/material/sidenav';
import { MatListModule } from '@angular/material/list';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatDividerModule } from '@angular/material/divider';
import { BreakpointObserver, Breakpoints } from '@angular/cdk/layout';
import { toSignal } from '@angular/core/rxjs-interop';
import { map } from 'rxjs';
import { AuroraComponent } from '@shared/aurora/aurora.component';

@Component({
  selector: 'app-admin-layout',
  imports: [
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    MatSidenavModule,
    MatListModule,
    MatIconModule,
    MatButtonModule,
    MatDividerModule,
    AuroraComponent,
  ],
  templateUrl: './admin-layout.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './admin-layout.component.scss',
})
export class AdminLayoutComponent {
  private breakpoints = inject(BreakpointObserver);

  isHandset = toSignal(
    this.breakpoints
      .observe([Breakpoints.Handset, '(max-width: 720px)'])
      .pipe(map((result) => result.matches)),
    { initialValue: false },
  );

  /** On mobile the drawer overlays content, so close it after navigating. */
  closeIfHandset(drawer: MatDrawer): void {
    if (this.isHandset()) {
      drawer.close();
    }
  }
}
