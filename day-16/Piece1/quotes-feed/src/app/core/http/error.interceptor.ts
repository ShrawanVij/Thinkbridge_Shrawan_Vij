import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { catchError, throwError } from 'rxjs';
import { AppError } from './app-error.model';

export function mapToAppError(err: HttpErrorResponse): AppError {
  const body = err.error;

  if (err.status === 0) {
    return new AppError('Could not reach the server. Please check your connection.', 0);
  }

  if (err.status >= 400 && err.status < 500 && body && typeof body === 'object') {
    if ('errors' in body && body.errors && typeof body.errors === 'object') {
      const errors = body.errors as Record<string, string[]>;
      const firstMessage = Object.values(errors)[0]?.[0];
      return new AppError(firstMessage ?? body.title ?? 'Please check your input and try again.', err.status, errors);
    }
    if ('title' in body && typeof body.title === 'string') {
      return new AppError(body.title, err.status);
    }
  }

  if (err.status === 401 || err.status === 403) {
    return new AppError('You must be logged in to do that.', err.status);
  }

  return new AppError('Something went wrong. Please try again.', err.status);
}

/** Maps a real ProblemDetails/ValidationProblemDetails (or any other) HTTP error to a typed AppError with a friendly message. */
export const errorInterceptor: HttpInterceptorFn = (req, next) =>
  next(req).pipe(
    catchError((err: unknown) => {
      if (err instanceof HttpErrorResponse) {
        return throwError(() => mapToAppError(err));
      }
      return throwError(() => err);
    }),
  );