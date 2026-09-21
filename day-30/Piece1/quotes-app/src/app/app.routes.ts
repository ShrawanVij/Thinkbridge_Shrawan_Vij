import { Routes } from '@angular/router';
import { authGuard } from './auth/auth.guard';

export const routes: Routes = [
  { path: '', redirectTo: 'quotes', pathMatch: 'full' },
  {
    path: 'login',
    loadComponent: () => import('./auth/login-page.component').then((m) => m.LoginPageComponent),
  },
  {
    path: 'register',
    loadComponent: () => import('./auth/register-page.component').then((m) => m.RegisterPageComponent),
  },
  {
    path: 'quotes',
    loadComponent: () => import('./quotes/quote-feed-page.component').then((m) => m.QuoteFeedPageComponent),
  },
  {
    path: 'quotes/new',
    canActivate: [authGuard],
    loadComponent: () => import('./quotes/quote-form-page.component').then((m) => m.QuoteFormPageComponent),
  },
  {
    path: 'quotes/:id/edit',
    canActivate: [authGuard],
    loadComponent: () => import('./quotes/quote-form-page.component').then((m) => m.QuoteFormPageComponent),
  },
  {
    path: 'quotes/:id',
    loadComponent: () => import('./quotes/quote-detail-page.component').then((m) => m.QuoteDetailPageComponent),
  },
  {
    path: 'collections',
    canActivate: [authGuard],
    loadComponent: () => import('./collections/collections-page.component').then((m) => m.CollectionsPageComponent),
  },
  {
    path: 'collections/:id',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./collections/collection-detail-page.component').then((m) => m.CollectionDetailPageComponent),
  },
  { path: '**', redirectTo: 'quotes' },
];
