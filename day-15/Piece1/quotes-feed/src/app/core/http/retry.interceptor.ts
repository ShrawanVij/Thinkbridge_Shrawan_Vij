import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { retry, timer } from 'rxjs';

const MAX_RETRIES = 2;
const BASE_DELAY_MS = 300;

/** Retries idempotent GETs with exponential backoff on transient failures (network error or 5xx). Real client errors (4xx) are never retried. */
export const retryInterceptor: HttpInterceptorFn = (req, next) => {
  if (req.method !== 'GET') {
    return next(req);
  }

  return next(req).pipe(
    retry({
      count: MAX_RETRIES,
      delay: (error: unknown, retryCount: number) => {
        const isTransient = error instanceof HttpErrorResponse && (error.status === 0 || error.status >= 500);
        if (!isTransient) {
          throw error;
        }
        return timer(BASE_DELAY_MS * 2 ** (retryCount - 1));
      },
    }),
  );
};