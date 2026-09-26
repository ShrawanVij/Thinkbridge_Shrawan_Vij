import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, switchMap, throwError } from 'rxjs';
import { AuthService } from '../auth/auth.service';

// The refresh endpoint itself must never go through the "retry after 401"
// path below -- a 401 there means the refresh token is dead too, not that it
// needs refreshing again.
const REFRESH_URL_SUFFIX = '/api/auth/refresh';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const authService = inject(AuthService);
  const authHeader = authService.authHeader();
  const authedReq = authHeader['Authorization'] ? req.clone({ setHeaders: authHeader }) : req;

  if (req.url.endsWith(REFRESH_URL_SUFFIX)) {
    return next(authedReq);
  }

  return next(authedReq).pipe(
    catchError((error: unknown) => {
      // Only worth attempting a refresh if this request actually carried an
      // access token that could have expired -- an anonymous request 401ing
      // means something else (e.g. no session at all), not an expired token.
      if (!(error instanceof HttpErrorResponse) || error.status !== 401 || !authHeader['Authorization']) {
        return throwError(() => error);
      }

      return authService.refresh().pipe(
        switchMap(() => next(req.clone({ setHeaders: authService.authHeader() }))),
        catchError((refreshError: unknown) => {
          authService.logout();
          return throwError(() => refreshError);
        }),
      );
    }),
  );
};
