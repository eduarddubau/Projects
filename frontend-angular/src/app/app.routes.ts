import { Routes } from '@angular/router';
import { authGuard } from '@core/guards/auth.guard';
import { adminGuard } from '@core/guards/admin.guard';
import { guestGuard } from '@core/guards/guest.guard';
import { workspaceGuard } from '@core/guards/workspace.guard';
import { workspaceHomeGuard } from '@core/guards/workspace-home.guard';
import { workspaceOwnerGuard } from '@core/guards/workspace-owner.guard';
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
    loadComponent: () =>
      import('./features/workspaces/shell/workspace-shell.component').then(
        (m) => m.WorkspaceShellComponent,
      ),
    children: [
      {
        // The workspace home: greeting, the workspace's numbers, and your own work in it.
        path: '',
        loadComponent: () =>
          import('./features/workspace-home/workspace-home.component').then(
            (m) => m.WorkspaceHomeComponent,
          ),
      },
      {
        path: 'projects',
        loadComponent: () =>
          import('./features/projects/list/projects-page.component').then(
            (m) => m.ProjectsPageComponent,
          ),
      },
      {
        path: 'tasks',
        loadComponent: () =>
          import('./features/tasks/workspace/workspace-tasks.component').then(
            (m) => m.WorkspaceTasksComponent,
          ),
      },
      {
        // Its own destination, not projects/trash. As a child of the projects route it made
        // the sidebar unsolvable — no routerLinkActive setting could light Projects on a
        // project's page while leaving it dark here.
        path: 'trash',
        loadComponent: () =>
          import('./features/trash/trash-shell.component').then((m) => m.TrashShellComponent),
        children: [
          // Tasks, because it is the tab both roles have — a role-dependent redirect would be
          // a second copy of the rule below, free to drift from it.
          { path: '', pathMatch: 'full', redirectTo: 'tasks' },
          {
            path: 'tasks',
            loadComponent: () =>
              import('./features/tasks/trash/task-trash.component').then(
                (m) => m.TaskTrashComponent,
              ),
          },
          {
            // The guard moved here from the page: a member now reaches /trash legitimately for
            // the task tab, and only projects are owner-only.
            path: 'projects',
            canActivate: [workspaceOwnerGuard],
            loadComponent: () =>
              import('./features/projects/trash/trash.component').then((m) => m.TrashComponent),
          },
        ],
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
