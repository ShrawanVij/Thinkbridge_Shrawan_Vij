import { HttpErrorResponse } from '@angular/common/http';
import { mapToAppError } from './error.interceptor';

describe('mapToAppError', () => {
  it('maps a real ValidationProblemDetails 400 (e.g. POST /api/quotes) to the first field message', () => {
    const err = new HttpErrorResponse({
      status: 400,
      error: {
        title: 'One or more validation errors occurred.',
        status: 400,
        errors: { author: ['Author is required.'], text: ['Text is required.'] },
      },
    });

    const appError = mapToAppError(err);

    expect(appError.friendlyMessage).toBe('Author is required.');
    expect(appError.status).toBe(400);
    expect(appError.fieldErrors).toEqual({ author: ['Author is required.'], text: ['Text is required.'] });
  });

  it('falls back to a generic message for a plain-string 400 (the real GET /api/quotes?page=0 shape)', () => {
    const err = new HttpErrorResponse({
      status: 400,
      error: 'Page must be >= 1 and size must be between 1 and 100.',
    });

    const appError = mapToAppError(err);

    expect(appError.friendlyMessage).toBe('Something went wrong. Please try again.');
    expect(appError.status).toBe(400);
    expect(appError.fieldErrors).toBeUndefined();
  });

  it('maps a network failure (status 0) to a connectivity message', () => {
    const err = new HttpErrorResponse({ status: 0 });

    const appError = mapToAppError(err);

    expect(appError.friendlyMessage).toBe('Could not reach the server. Please check your connection.');
  });

  it('maps a 401/403 to a login message', () => {
    expect(mapToAppError(new HttpErrorResponse({ status: 401 })).friendlyMessage).toBe('You must be logged in to do that.');
    expect(mapToAppError(new HttpErrorResponse({ status: 403 })).friendlyMessage).toBe('You must be logged in to do that.');
  });

  it('maps a 500 to a generic message', () => {
    const appError = mapToAppError(new HttpErrorResponse({ status: 500 }));
    expect(appError.friendlyMessage).toBe('Something went wrong. Please try again.');
  });
});