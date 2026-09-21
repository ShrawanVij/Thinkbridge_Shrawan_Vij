import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { Router, provideRouter, withComponentInputBinding } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { routes } from './app.routes';
import { AuthService } from './auth/auth.service';

// Exercises the REAL route config end to end, the same way the browser does --
// not each route's component in isolation.
describe('app.routes integration', () => {
  let harness: RouterTestingHarness;
  let httpMock: HttpTestingController;

  beforeEach(async () => {
    TestBed.configureTestingModule({
      // Must mirror app.config.ts's provideRouter() config exactly -- that's the point of this test.
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter(routes, withComponentInputBinding())],
    });
    httpMock = TestBed.inject(HttpTestingController);
    harness = await RouterTestingHarness.create();
  });

  afterEach(() => httpMock.verify());

  it('lazy-loads the detail route and passes the real :id route param to GET /api/quotes/{id}', async () => {
    await harness.navigateByUrl('/quotes/42');

    const req = httpMock.expectOne((r) => r.url.endsWith('/api/quotes/42'));
    req.flush({ id: 42, author: 'Ada Lovelace', text: 'A real quote.', userId: 1, createdAt: '2026-01-01T00:00:00Z', tags: [] });
    harness.detectChanges();

    expect(harness.routeNativeElement?.textContent).toContain('A real quote.');
  });

  it('redirects /create to /login when not authenticated (guard)', async () => {
    await harness.navigateByUrl('/create');

    const router = TestBed.inject(Router);
    expect(router.url).toContain('/login');
    expect(router.url).toContain('redirectTo=%2Fcreate');
  });

  it('allows /create once logged in (guard passes)', async () => {
    const authService = TestBed.inject(AuthService);
    authService.login({ email: 'test@example.com', password: 'Test123!' }).subscribe();
    httpMock.expectOne('http://127.0.0.1:5220/api/auth/login').flush({
      access_token: 'fake-jwt-token',
      expires_in: 900,
    });

    await harness.navigateByUrl('/create');

    const router = TestBed.inject(Router);
    expect(router.url).toBe('/create');
  });
});