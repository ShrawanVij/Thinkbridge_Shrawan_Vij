import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import { LoginRequest, LoginResponse } from './auth.model';
import { environment } from '../../environments/environment';

const STORAGE_KEY = 'quotes-feed.auth';

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

  login(request: LoginRequest): Observable<LoginResponse> {
    // withCredentials so the browser stores the HttpOnly refreshToken cookie
    // the backend sets on this response -- it's a cross-origin request
    // (localhost:4200 -> 127.0.0.1:5220), so cookies aren't sent/kept by
    // default without this.
    return this.http.post<LoginResponse>(`${this.baseUrl}/api/auth/login`, request, { withCredentials: true }).pipe(
      tap((response) => {
        this.accessToken.set(response.access_token);
        this.loggedInEmail.set(request.email);
        this.persistSession({
          accessToken: response.access_token,
          email: request.email,
          // Real Week-1 contract: expires_in is seconds (900 = the configured
          // 15-minute AccessTokenLifetime). Store an absolute deadline so a
          // later page load can tell a stale token from a fresh one.
          expiresAt: Date.now() + response.expires_in * 1000,
        });
      }),
    );
  }

  logout(): void {
    this.accessToken.set(null);
    this.loggedInEmail.set(null);
    this.clearSession();
  }

  authHeader(): Record<string, string> {
    const token = this.accessToken();
    return token ? { Authorization: `Bearer ${token}` } : {};
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
      // Storage unavailable (quota, private browsing, etc.) -- the session
      // just won't survive a refresh; the in-memory login still works.
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