import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { authInterceptor } from './auth.interceptor';
import { AuthService } from '../../auth/auth.service';

describe('authInterceptor', () => {
  let http: HttpClient;
  let httpMock: HttpTestingController;
  let authService: AuthService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(withInterceptors([authInterceptor])), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
    authService = TestBed.inject(AuthService);
  });

  afterEach(() => httpMock.verify());

  it('does not attach an Authorization header when logged out', () => {
    http.get('http://test/quotes').subscribe();
    const req = httpMock.expectOne('http://test/quotes');
    expect(req.request.headers.has('Authorization')).toBe(false);
    req.flush({});
  });

  it('attaches the real bearer token as an Authorization header once logged in', () => {
    authService.login({ email: 'test@example.com', password: 'Test123!' }).subscribe();
    httpMock.expectOne('http://127.0.0.1:5220/api/auth/login').flush({
      access_token: 'fake-jwt-token',
      expires_in: 900,
    });

    http.get('http://test/quotes').subscribe();
    const req = httpMock.expectOne('http://test/quotes');
    expect(req.request.headers.get('Authorization')).toBe('Bearer fake-jwt-token');
    req.flush({});
  });
});