import { Routes } from '@angular/router';
import { authGuard } from '@core/guards/auth.guard';
import { adminGuard } from '@core/guards/admin.guard';
import { guestGuard } from '@core/guards/guest.guard';
import { workspaceGuard } from '@core/guards/workspace.guard';

export const routes: Routes = [
  {
    path: '',
    loadComponent: () => import('./features/home/home.component').then((m) => m.HomeComponent),
  },
  {
    path: 'login',
    loadComponent: () => import('./features/login/login.component').then((m) => m.LoginComponent),
    canActivate: [guestGuard],
  },
  {
    path: 'register',
    loadComponent: () =>
      import('./features/register/register.component').then((m) => m.RegisterComponent),
    canActivate: [guestGuard],
  },
  {
    path: 'projects',
    canActivate: [authGuard],
    children: [
      {
        path: '',
        loadComponent: () =>
          import('./features/projects/projects.component').then((m) => m.ProjectsComponent),
      },
      {
        path: 'trash',
        loadComponent: () =>
          import('./features/projects/trash/trash.component').then((m) => m.TrashComponent),
      },
      {
        path: ':id',
        loadComponent: () =>
          import('./features/projects/detail/project-detail.component').then(
            (m) => m.ProjectDetailComponent,
          ),
      },
    ],
  },
  {
    path: 'workspaces',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/workspaces/list/workspaces.component').then((m) => m.WorkspacesComponent),
  },
  {
    // authGuard first: guards run in sequence and stop at the first refusal, so
    // reversed, a signed-out user would fire a 401 before being sent to /login.
    path: 'w/:workspaceId',
    canActivate: [authGuard, workspaceGuard],
    children: [
      {
        path: 'members',
        loadComponent: () =>
          import('./features/workspaces/members/members.component').then((m) => m.MembersComponent),
      },
    ],
  },
  {
    // The API requires a signed-in caller, so authGuard bounces anonymous
    // recipients through /login and returnUrl brings them back with the token.
    path: 'invitations/accept',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/invitations/accept/accept-invitation.component').then(
        (m) => m.AcceptInvitationComponent,
      ),
  },
  {
    path: 'dashboard',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/user-dashboard/user-dashboard.component').then(
        (m) => m.UserDashboardComponent,
      ),
  },
  {
    path: 'profile',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/profile/profile.component').then((m) => m.ProfileComponent),
  },
  {
    path: 'admin',
    canActivate: [adminGuard],
    loadChildren: () => import('./features/admin/admin.routes').then((m) => m.adminRoutes),
  },
  {
    path: '**',
    redirectTo: '',
  },
];
