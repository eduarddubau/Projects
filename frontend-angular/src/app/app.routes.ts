import { Routes } from '@angular/router';
import { authGuard } from '@core/guards/auth.guard';
import { adminGuard } from '@core/guards/admin.guard';
import { guestGuard } from '@core/guards/guest.guard';

export const routes: Routes = [
  {
    path: '',
    loadComponent: () => import('./features/home/home.component').then(m => m.HomeComponent)
  },
  {
    path: 'login',
    loadComponent: () => import('./features/login/login.component').then(m => m.LoginComponent),
    canActivate: [guestGuard]
  },
  {
    path: 'register',
    loadComponent: () => import('./features/register/register.component').then(m => m.RegisterComponent),
    canActivate: [guestGuard]
  },
  {
    path: 'projects',
    canActivate: [authGuard],
    data: { breadcrumb: 'Projects' },
    children: [
      {
        path: '',
        loadComponent: () => import('./features/projects/projects.component').then(m => m.ProjectsComponent)
      },
      {
        path: 'trash',
        loadComponent: () => import('./features/projects/trash/trash.component').then(m => m.TrashComponent),
        data: { breadcrumb: 'Trash' }
      },
      {
        path: ':id',
        loadComponent: () => import('./features/projects/detail/project-detail.component').then(m => m.ProjectDetailComponent),
        data: { breadcrumb: 'Details' }
      }
    ]
  },
  {
    path: 'admin',
    canActivate: [adminGuard],
    loadChildren: () => import('./features/admin/admin.routes').then(m => m.adminRoutes)
  },
  {
    path: '**',
    redirectTo: ''
  }
];