import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { firstValueFrom } from 'rxjs';
import { authInterceptor } from './auth.interceptor';
import { retryInterceptor } from './retry.interceptor';
import { errorInterceptor } from './error.interceptor';

// Exercises the exact interceptor order registered in app.config.ts, end to end,
// against real HttpTestingController requests -- not each interceptor in isolation.
describe('interceptor chain, as registered in app.config.ts', () => {
  let http: HttpClient;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        // Must mirror app.config.ts's registered order exactly -- that's the point of this test.
        provideHttpClient(withInterceptors([authInterceptor, errorInterceptor, retryInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('retries an idempotent GET that fails with a transient 503, then succeeds', async () => {
    const promise = firstValueFrom(http.get('http://test/quotes'));

    httpMock.expectOne('http://test/quotes').flush('Service Unavailable', { status: 503, statusText: 'Service Unavailable' });

    // Real backoff timer (300ms base delay) -- give it room to fire.
    await new Promise((resolve) => setTimeout(resolve, 500));

    httpMock.expectOne('http://test/quotes').flush({ ok: true });

    await expect(promise).resolves.toEqual({ ok: true });
  });

  it('does not retry a real client error (404) on a GET', async () => {
    let captured: unknown;
    http.get('http://test/quotes/999').subscribe({ error: (err) => (captured = err) });

    httpMock.expectOne('http://test/quotes/999').flush('Not Found', { status: 404, statusText: 'Not Found' });

    await new Promise((resolve) => setTimeout(resolve, 500));
    httpMock.verify(); // throws if a retried second request was made

    expect(captured).toMatchObject({ status: 404 });
  });

  it('does not retry a POST, even on a transient 503', async () => {
    let captured: unknown;
    http.post('http://test/quotes', { author: 'A', text: 'B' }).subscribe({ error: (err) => (captured = err) });

    httpMock.expectOne('http://test/quotes').flush('Service Unavailable', { status: 503, statusText: 'Service Unavailable' });

    await new Promise((resolve) => setTimeout(resolve, 500));
    httpMock.verify(); // throws if a retried second request was made

    expect(captured).toMatchObject({ status: 503 });
  });
});