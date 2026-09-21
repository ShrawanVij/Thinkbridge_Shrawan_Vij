import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { LoginFormComponent } from './login-form.component';
import { AuthService } from './auth.service';

describe('LoginFormComponent', () => {
  let httpMock: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [LoginFormComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();

    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  function create() {
    const fixture = TestBed.createComponent(LoginFormComponent);
    fixture.detectChanges();
    return fixture;
  }

  function fill(fixture: ReturnType<typeof create>, email: string, password: string) {
    const emailInput = fixture.nativeElement.querySelector('#login-email');
    const passwordInput = fixture.nativeElement.querySelector('#login-password');
    emailInput.value = email;
    emailInput.dispatchEvent(new Event('input'));
    passwordInput.value = password;
    passwordInput.dispatchEvent(new Event('input'));
    fixture.detectChanges();
  }

  it('does not show errors before any interaction (pristine state)', () => {
    const fixture = create();
    expect(fixture.nativeElement.textContent).not.toContain('required');
  });

  it('shows both required errors and focuses email when submitted empty', () => {
    const fixture = create();

    fixture.nativeElement.querySelector('button[type=submit]').click();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Email is required.');
    expect(fixture.nativeElement.textContent).toContain('Password is required.');
    expect(document.activeElement?.id).toBe('login-email');
  });

  it('logs in with the real /api/auth/login contract and sets the auth token', async () => {
    const fixture = create();
    const authService = TestBed.inject(AuthService);
    fill(fixture, 'test@example.com', 'Test123!');

    fixture.nativeElement.querySelector('button[type=submit]').click();

    const req = httpMock.expectOne((r) => r.url.endsWith('/api/auth/login'));
    expect(req.request.body).toEqual({ email: 'test@example.com', password: 'Test123!' });
    req.flush({ access_token: 'fake-token', refresh_token: 'fake-refresh', expires_in: 900 });
    await fixture.whenStable();

    expect(authService.isAuthenticated()).toBe(true);
  });

  it('shows a server error on invalid credentials (401)', async () => {
    const fixture = create();
    const component = fixture.componentInstance;
    fill(fixture, 'wrong@example.com', 'wrongpass');

    fixture.nativeElement.querySelector('button[type=submit]').click();

    const req = httpMock.expectOne((r) => r.url.endsWith('/api/auth/login'));
    req.flush({}, { status: 401, statusText: 'Unauthorized' });
    await fixture.whenStable();

    fixture.detectChanges();
    expect(component.serverError()).toBe('Invalid email or password.');
  });
});