import { TestBed } from '@angular/core/testing';
import { Router, UrlTree } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { authGuard } from './auth.guard';
import { AuthService } from './auth.service';

describe('authGuard', () => {
  let authService: AuthService;
  let router: Router;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    authService = TestBed.inject(AuthService);
    router = TestBed.inject(Router);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  function runGuard(url: string) {
    return TestBed.runInInjectionContext(() =>
      authGuard({} as never, { url } as never),
    );
  }

  it('redirects to /login with the attempted URL when not authenticated', () => {
    const result = runGuard('/create');

    expect(result).toBeInstanceOf(UrlTree);
    const urlTree = result as UrlTree;
    expect(urlTree.toString()).toContain('/login');
    expect(urlTree.queryParams['redirectTo']).toBe('/create');
  });

  it('allows activation once logged in with the real Week-1 auth contract', () => {
    authService.login({ email: 'test@example.com', password: 'Test123!' }).subscribe();
    httpMock.expectOne('http://127.0.0.1:5220/api/auth/login').flush({
      access_token: 'fake-jwt-token',
      expires_in: 900,
    });

    expect(runGuard('/create')).toBe(true);
  });
});