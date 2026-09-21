import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import { LoginRequest, LoginResponse } from './auth.model';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = 'http://127.0.0.1:5220';

  private readonly accessToken = signal<string | null>(null);
  private readonly loggedInEmail = signal<string | null>(null);
  readonly isAuthenticated = computed(() => this.accessToken() !== null);
  readonly email = computed(() => this.loggedInEmail());

  login(request: LoginRequest): Observable<LoginResponse> {
    return this.http.post<LoginResponse>(`${this.baseUrl}/api/auth/login`, request).pipe(
      tap((response) => {
        this.accessToken.set(response.access_token);
        this.loggedInEmail.set(request.email);
      }),
    );
  }

  authHeader(): Record<string, string> {
    const token = this.accessToken();
    return token ? { Authorization: `Bearer ${token}` } : {};
  }
}