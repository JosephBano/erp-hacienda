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
        localStorage.setItem(TOKEN_KEY, result.token);
        localStorage.setItem(USER_KEY, JSON.stringify(result));
        this.currentUser.set(result);
      })
    );
  }

  logout(): void {
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(USER_KEY);
    this.currentUser.set(null);
  }

  getToken(): string | null {
    return localStorage.getItem(TOKEN_KEY);
  }

  isAuthenticated(): boolean {
    return !!this.getToken();
  }

  private readStoredUser(): LoginResult | null {
    const raw = localStorage.getItem(USER_KEY);
    return raw ? (JSON.parse(raw) as LoginResult) : null;
  }
}
