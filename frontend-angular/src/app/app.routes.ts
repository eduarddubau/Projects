import { Routes } from '@angular/router';
import { authGuard } from '@core/guards/auth.guard';
import { adminGuard } from '@core/guards/admin.guard';
import { guestGuard } from '@core/guards/guest.guard';
import { workspaceGuard } from '@core/guards/workspace.guard';
import { workspaceHomeGuard } from '@core/guards/workspace-home.guard';
import { standardUserGuard } from '@core/guards/standard-user.guard';

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
    path: 'workspaces',
    canActivate: [authGuard, standardUserGuard],
    loadComponent: () =>
      import('./features/workspaces/list/workspaces.component').then((m) => m.WorkspacesComponent),
  },
  {
    path: 'workspaces/trash',
    canActivate: [authGuard, standardUserGuard],
    loadComponent: () =>
      import('./features/workspaces/trash/workspace-trash.component').then(
        (m) => m.WorkspaceTrashComponent,
      ),
  },
  {
    // authGuard first: guards run in sequence and stop at the first refusal, so
    // reversed, a signed-out user would fire a 401 before being sent to /login.
    path: 'w/:workspaceId',
    canActivate: [authGuard, standardUserGuard, workspaceGuard],
    children: [
      {
        // The workspace home: greeting, the workspace's numbers, and its projects.
        path: '',
        loadComponent: () =>
          import('./features/workspace-home/workspace-home.component').then(
            (m) => m.WorkspaceHomeComponent,
          ),
      },
      {
        // The projects list is the home page now. pathMatch 'full' so only the bare
        // /projects folds in — /projects/trash and /projects/:id still route below.
        path: 'projects',
        pathMatch: 'full',
        redirectTo: '',
      },
      {
        path: 'projects/trash',
        loadComponent: () =>
          import('./features/projects/trash/trash.component').then((m) => m.TrashComponent),
      },
      {
        path: 'projects/:id',
        loadComponent: () =>
          import('./features/projects/detail/project-detail.component').then(
            (m) => m.ProjectDetailComponent,
          ),
      },
      {
        path: 'members',
        loadComponent: () =>
          import('./features/workspaces/members/members.component').then((m) => m.MembersComponent),
      },
      {
        path: 'settings',
        loadComponent: () =>
          import('./features/workspaces/settings/workspace-settings.component').then(
            (m) => m.WorkspaceSettingsComponent,
          ),
      },
    ],
  },
  {
    // The API requires a signed-in caller, so authGuard bounces anonymous
    // recipients through /login and returnUrl brings them back with the token.
    path: 'invitations/accept',
    canActivate: [authGuard, standardUserGuard],
    loadComponent: () =>
      import('./features/invitations/accept/accept-invitation.component').then(
        (m) => m.AcceptInvitationComponent,
      ),
  },
  {
    // The stable "home" URL, kept so bookmarks and every redirect that means "home"
    // have one target. workspaceHomeGuard always forwards it to /w/{best}, so this
    // route never activates and needs no component — the empty children array is only
    // the shape Angular requires of a route that renders nothing itself.
    path: 'dashboard',
    canActivate: [authGuard, standardUserGuard, workspaceHomeGuard],
    children: [],
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
