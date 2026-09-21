import { ApplicationConfig, provideBrowserGlobalErrorListeners, provideZonelessChangeDetection } from '@angular/core';
import { provideRouter, withComponentInputBinding, withViewTransitions } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { routes } from './app.routes';
import { authInterceptor } from './core/http/auth.interceptor';
import { retryInterceptor } from './core/http/retry.interceptor';
import { errorInterceptor } from './core/http/error.interceptor';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideZonelessChangeDetection(),
    // withComponentInputBinding() is required for the :id route param to
    // reach QuoteDetailPageComponent's `id` input() -- without it, Angular
    // silently never sets the input (it's not an error), so the id stays
    // undefined and no request is ever made. Caught by
    // app.routes.integration.spec.ts.
    provideRouter(routes, withComponentInputBinding(), withViewTransitions()),
    // Order matters: responses flow back through interceptors in reverse of
    // this list, so retryInterceptor must sit closer to the backend than
    // errorInterceptor -- otherwise errorInterceptor maps the raw
    // HttpErrorResponse to an AppError before retryInterceptor ever sees it,
    // and retries silently never happen.
    provideHttpClient(withInterceptors([authInterceptor, errorInterceptor, retryInterceptor])),
  ]
};
