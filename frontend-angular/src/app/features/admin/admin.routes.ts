import { Routes } from '@angular/router';

export const adminRoutes: Routes = [
  {
    path: '',
    loadComponent: () => import('./admin-layout.component').then(m => m.AdminLayoutComponent),
    data: { breadcrumb: 'Admin' },
    children: [
      {
        path: '',
        loadComponent: () => import('./dashboard/admin-dashboard.component').then(m => m.AdminDashboardComponent),
        data: { breadcrumb: 'Dashboard' }
      },
      {
        path: 'projects',
        loadComponent: () => import('./projects/admin-projects.component').then(m => m.AdminProjectsComponent),
        data: { breadcrumb: 'Projects' }
      },
      {
        path: 'users',
        loadComponent: () => import('./users/admin-users.component').then(m => m.AdminUsersComponent),
        data: { breadcrumb: 'Users' }
      },
      {
        path: 'trash',
        data: { breadcrumb: 'Trash' },
        children: [
          {
            path: '',
            pathMatch: 'full',
            redirectTo: 'projects'
          },
          {
            path: 'projects',
            loadComponent: () => import('./trash/projects/trash-projects.component').then(m => m.TrashProjectsComponent),
            data: { breadcrumb: 'Projects' }
          },
          {
            path: 'users',
            loadComponent: () => import('./trash/users/trash-users.component').then(m => m.TrashUsersComponent),
            data: { breadcrumb: 'Users' }
          }
        ]
      }
    ]
  }
];
