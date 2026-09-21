import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { AuthService } from './auth.service';

const STORAGE_KEY = 'quotes-feed.auth';

describe('AuthService session persistence', () => {
  let httpMock: HttpTestingController;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
    localStorage.clear();
  });

  it('persists the real login response to localStorage on login', () => {
    const authService = TestBed.inject(AuthService);

    authService.login({ email: 'test@example.com', password: 'Test123!' }).subscribe();
    const req = httpMock.expectOne('http://127.0.0.1:5220/api/auth/login');
    expect(req.request.withCredentials).toBe(true); // required to receive the HttpOnly refreshToken cookie
    req.flush({
      access_token: 'fake-jwt-token',
      expires_in: 900,
    });

    const stored = JSON.parse(localStorage.getItem(STORAGE_KEY)!);
    expect(stored.accessToken).toBe('fake-jwt-token');
    expect(stored.email).toBe('test@example.com');
    expect(stored.expiresAt).toBeGreaterThan(Date.now());
    // The refresh token must never be written to localStorage -- it lives
    // only in the HttpOnly cookie the backend sets.
    expect(stored.refreshToken).toBeUndefined();
  });

  it('restores a still-valid session on a fresh instance (simulating a tab refresh)', () => {
    localStorage.setItem(
      STORAGE_KEY,
      JSON.stringify({
        accessToken: 'restored-token',
        email: 'restored@example.com',
        expiresAt: Date.now() + 60_000,
      }),
    );

    // A fresh AuthService instance is what a real page reload produces.
    const authService = TestBed.inject(AuthService);

    expect(authService.isAuthenticated()).toBe(true);
    expect(authService.email()).toBe('restored@example.com');
    expect(authService.authHeader()).toEqual({ Authorization: 'Bearer restored-token' });
  });

  it('drops an expired stored session and clears it, rather than restoring a dead token', () => {
    localStorage.setItem(
      STORAGE_KEY,
      JSON.stringify({
        accessToken: 'expired-token',
        email: 'expired@example.com',
        expiresAt: Date.now() - 1_000,
      }),
    );

    const authService = TestBed.inject(AuthService);

    expect(authService.isAuthenticated()).toBe(false);
    expect(localStorage.getItem(STORAGE_KEY)).toBeNull();
  });

  it('drops corrupted storage content without throwing', () => {
    localStorage.setItem(STORAGE_KEY, 'not valid json{{{');

    expect(() => TestBed.inject(AuthService)).not.toThrow();
    const authService = TestBed.inject(AuthService);
    expect(authService.isAuthenticated()).toBe(false);
    expect(localStorage.getItem(STORAGE_KEY)).toBeNull();
  });

  it('logout() clears both the in-memory state and localStorage', () => {
    localStorage.setItem(
      STORAGE_KEY,
      JSON.stringify({
        accessToken: 'token',
        email: 'test@example.com',
        expiresAt: Date.now() + 60_000,
      }),
    );

    const authService = TestBed.inject(AuthService);
    expect(authService.isAuthenticated()).toBe(true);

    authService.logout();

    expect(authService.isAuthenticated()).toBe(false);
    expect(localStorage.getItem(STORAGE_KEY)).toBeNull();
  });
});