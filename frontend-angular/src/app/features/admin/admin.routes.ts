import { Routes } from '@angular/router';

export const adminRoutes: Routes = [
  {
    path: '',
    loadComponent: () => import('./dashboard/admin-dashboard.component').then(m => m.AdminDashboardComponent)
  },
  {
    path: 'projects',
    loadComponent: () => import('./projects/admin-projects.component').then(m => m.AdminProjectsComponent)
  },
  {
    path: 'users',
    loadComponent: () => import('./users/admin-users.component').then(m => m.AdminUsersComponent)
  },
  {
    path: 'trash',
    children: [
      {
        path: 'projects',
        loadComponent: () => import('./trash/projects/trash-projects.component').then(m => m.TrashProjectsComponent)
      },
      {
        path: 'users',
        loadComponent: () => import('./trash/users/trash-users.component').then(m => m.TrashUsersComponent)
      }
    ]
  }
];