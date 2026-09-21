import { Routes } from '@angular/router';
import { HomeComponent } from './home/home.component';
import { authGuard } from './auth/auth.guard';

export const routes: Routes = [
  { path: '', component: HomeComponent },
  {
    path: 'quotes/:id',
    loadComponent: () => import('./quote-feed/quote-detail-page.component').then((m) => m.QuoteDetailPageComponent),
  },
  {
    path: 'create',
    canActivate: [authGuard],
    loadComponent: () => import('./create-quote/create-quote-page.component').then((m) => m.CreateQuotePageComponent),
  },
  {
    path: 'login',
    loadComponent: () => import('./auth/login-page.component').then((m) => m.LoginPageComponent),
  },
  { path: '**', redirectTo: '' },
];