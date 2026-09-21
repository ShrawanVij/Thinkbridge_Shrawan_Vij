import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import { AuthResponse, LoginRequest, RegisterRequest } from './auth.model';
import { environment } from '../../environments/environment';

const STORAGE_KEY = 'quotes-app.auth';

interface StoredSession {
  accessToken: string;
  email: string;
  expiresAt: number;
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiBaseUrl;

  private readonly accessToken = signal<string | null>(null);
  private readonly loggedInEmail = signal<string | null>(null);
  readonly isAuthenticated = computed(() => this.accessToken() !== null);
  readonly email = computed(() => this.loggedInEmail());

  constructor() {
    this.restoreSession();
  }

  login(request: LoginRequest): Observable<AuthResponse> {
    // withCredentials so the browser stores the HttpOnly refreshToken cookie
    // the backend sets on this response.
    return this.http
      .post<AuthResponse>(`${this.baseUrl}/api/auth/login`, request, { withCredentials: true })
      .pipe(tap((response) => this.applySession(response, request.email)));
  }

  register(request: RegisterRequest): Observable<AuthResponse> {
    return this.http
      .post<AuthResponse>(`${this.baseUrl}/api/auth/register`, request, { withCredentials: true })
      .pipe(tap((response) => this.applySession(response, request.email)));
  }

  logout(): void {
    // Best-effort: revoke the refresh token cookie server-side, but local
    // logout must never hang on the network.
    this.http
      .post(`${this.baseUrl}/api/auth/logout`, {}, { withCredentials: true })
      .subscribe({ error: () => {} });

    this.accessToken.set(null);
    this.loggedInEmail.set(null);
    this.clearSession();
  }

  authHeader(): Record<string, string> {
    const token = this.accessToken();
    return token ? { Authorization: `Bearer ${token}` } : {};
  }

  private applySession(response: AuthResponse, email: string): void {
    this.accessToken.set(response.access_token);
    this.loggedInEmail.set(email);
    this.persistSession({
      accessToken: response.access_token,
      email,
      expiresAt: Date.now() + response.expires_in * 1000,
    });
  }

  private restoreSession(): void {
    const raw = this.readStorage();
    if (!raw) return;

    let session: StoredSession;
    try {
      session = JSON.parse(raw);
    } catch {
      this.clearSession();
      return;
    }

    if (!session.accessToken || Date.now() >= session.expiresAt) {
      this.clearSession();
      return;
    }

    this.accessToken.set(session.accessToken);
    this.loggedInEmail.set(session.email);
  }

  private persistSession(session: StoredSession): void {
    try {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(session));
    } catch {
      // Storage unavailable -- the session just won't survive a refresh.
    }
  }

  private readStorage(): string | null {
    try {
      return localStorage.getItem(STORAGE_KEY);
    } catch {
      return null;
    }
  }

  private clearSession(): void {
    try {
      localStorage.removeItem(STORAGE_KEY);
    } catch {
      // Storage unavailable -- nothing to clear.
    }
  }
}
