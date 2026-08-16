import { Routes } from '@angular/router';

export const adminRoutes: Routes = [
  {
    path: '',
    loadComponent: () => import('./admin-layout.component').then((m) => m.AdminLayoutComponent),
    children: [
      {
        path: '',
        loadComponent: () =>
          import('./dashboard/admin-dashboard.component').then((m) => m.AdminDashboardComponent),
      },
      {
        path: 'users',
        loadComponent: () =>
          import('./users/admin-users.component').then((m) => m.AdminUsersComponent),
      },
      {
        path: 'trash',
        children: [
          {
            path: '',
            pathMatch: 'full',
            redirectTo: 'projects',
          },
          {
            path: 'projects',
            loadComponent: () =>
              import('./trash/projects/trash-projects.component').then(
                (m) => m.TrashProjectsComponent,
              ),
          },
          {
            path: 'users',
            loadComponent: () =>
              import('./trash/users/trash-users.component').then((m) => m.TrashUsersComponent),
          },
          {
            path: 'workspaces',
            loadComponent: () =>
              import('./trash/workspaces/trash-workspaces.component').then(
                (m) => m.TrashWorkspacesComponent,
              ),
          },
        ],
      },
    ],
  },
];
