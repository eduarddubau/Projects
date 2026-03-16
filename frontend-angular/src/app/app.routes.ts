import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', loadComponent: () => import('./home/home').then(m => m.Home) },
  { path: 'login', loadComponent: () => import('./login/login').then(m => m.Login) },
  { path: 'entities', loadComponent: () => import('./entity-list/entity-list.component').then(m => m.EntityListComponent) }
];