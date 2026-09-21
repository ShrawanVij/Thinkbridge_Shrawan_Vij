import { ApplicationConfig, provideBrowserGlobalErrorListeners, provideZonelessChangeDetection } from '@angular/core';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { authInterceptor } from './core/http/auth.interceptor';
import { retryInterceptor } from './core/http/retry.interceptor';
import { errorInterceptor } from './core/http/error.interceptor';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideZonelessChangeDetection(),
    // Order matters: responses flow back through interceptors in reverse of
    // this list, so retryInterceptor must sit closer to the backend than
    // errorInterceptor -- otherwise errorInterceptor maps the raw
    // HttpErrorResponse to an AppError before retryInterceptor ever sees it,
    // and retries silently never happen.
    provideHttpClient(withInterceptors([authInterceptor, errorInterceptor, retryInterceptor])),
  ]
};
