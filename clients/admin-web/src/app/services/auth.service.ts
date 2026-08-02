import { Injectable, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';

export interface LoginRequest {
  email: string;
  password: string;
}

export interface LoginResult {
  token: string;
  expiresAt: string;
  userId: string;
  fullName: string;
  role: string;
}

// sessionStorage, not localStorage: an XSS payload can still read it, but the token
// dies with the tab instead of persisting indefinitely on a shared/farm computer.
// A httpOnly cookie would be stronger but needs backend session support this API
// doesn't have yet.
const TOKEN_KEY = 'hato_token';
const USER_KEY = 'hato_user';

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  private http = inject(HttpClient);
  private baseUrl = '/api/v1/people';

  currentUser = signal<LoginResult | null>(this.readStoredUser());

  login(request: LoginRequest): Observable<LoginResult> {
    return this.http.post<LoginResult>(`${this.baseUrl}/auth/login`, request).pipe(
      tap((result) => {
        sessionStorage.setItem(TOKEN_KEY, result.token);
        sessionStorage.setItem(USER_KEY, JSON.stringify(result));
        this.currentUser.set(result);
      })
    );
  }

  logout(): void {
    sessionStorage.removeItem(TOKEN_KEY);
    sessionStorage.removeItem(USER_KEY);
    this.currentUser.set(null);
  }

  getToken(): string | null {
    return sessionStorage.getItem(TOKEN_KEY);
  }

  isAuthenticated(): boolean {
    return !!this.getToken();
  }

  private readStoredUser(): LoginResult | null {
    const raw = sessionStorage.getItem(USER_KEY);
    return raw ? (JSON.parse(raw) as LoginResult) : null;
  }
}
