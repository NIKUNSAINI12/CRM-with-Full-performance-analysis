import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { Observable, tap, firstValueFrom } from 'rxjs';
import { API_BASE_URL } from '../../app';
import { RbacService } from './rbac.service';

export interface AuthUser {
  accessToken:  string;
  userId:       number | string;
  role:         'Customer' | 'PM' | 'Assignee' | 'Manager' | 'SuperManager' | 'Super Admin' | 'HOD' | string;
  name:         string;
  expiresAt:    string;   // ISO datetime string
  customerId?:  number;   // only for customers
  dashboard?:   string;   // Dynamic dashboard route
  permissions?: string[]; // Dynamic permissions
  userNumber?:  string;
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly apiUrl = API_BASE_URL;
  private readonly SESSION_KEY = 'auth_user';

  // ── In-memory access token (more XSS-safe than localStorage) ──────────
  // We also write to sessionStorage for page-refresh survival.
  private _accessToken: string | null = null;

  constructor(private http: HttpClient, private router: Router, private rbacService: RbacService) {
    // Restore from localStorage or sessionStorage on page load
    const stored = localStorage.getItem(this.SESSION_KEY) || sessionStorage.getItem(this.SESSION_KEY);
    if (stored) {
      try {
        const user: AuthUser = JSON.parse(stored);
        this._accessToken = user.accessToken;
        // Sync to sessionStorage for any component dependencies
        sessionStorage.setItem(this.SESSION_KEY, stored);
      } catch {
        sessionStorage.removeItem(this.SESSION_KEY);
        localStorage.removeItem(this.SESSION_KEY);
      }
    }
  }

  // ════════════════════════════════════════════════════════════════════
  // LOGIN METHODS
  // ════════════════════════════════════════════════════════════════════

  login(userNumber: string, password: string): Observable<AuthUser> {
    return this.http
      .post<AuthUser>(`${this.apiUrl}/auth/login`, { userNumber, password }, { withCredentials: true })
      .pipe(tap(res => this.storeSession(res)));
  }

  // ════════════════════════════════════════════════════════════════════
  // TOKEN MANAGEMENT
  // ════════════════════════════════════════════════════════════════════

  /** Returns the raw JWT access token (for the HTTP interceptor). */
  getToken(): string | null {
    if (this._accessToken) return this._accessToken;

    // Fallback: try localStorage or sessionStorage
    const stored = localStorage.getItem(this.SESSION_KEY) || sessionStorage.getItem(this.SESSION_KEY);
    if (stored) {
      try {
        const user: AuthUser = JSON.parse(stored);
        this._accessToken = user.accessToken;
        return this._accessToken;
      } catch { /* ignore */ }
    }
    return null;
  }

  /**
   * Calls the /auth/refresh endpoint (httpOnly cookie is sent automatically by browser).
   * On success, stores the new access token. Returns new token or null.
   */
  async refreshToken(): Promise<string | null> {
    try {
      const res = await firstValueFrom(
        this.http.post<AuthUser>(`${this.apiUrl}/auth/refresh`, {}, { withCredentials: true })
      );
      this.storeSession(res);
      return res.accessToken;
    } catch {
      return null;
    }
  }

  // ════════════════════════════════════════════════════════════════════
  // LOGOUT
  // ════════════════════════════════════════════════════════════════════

  logout(): void {
    const stored = localStorage.getItem(this.SESSION_KEY) || sessionStorage.getItem(this.SESSION_KEY);
    if (!stored) {
      this.clearLocalData();
      this.router.navigate(['/login']);
      return;
    }

    this.http.post(`${this.apiUrl}/auth/logout`, {}, { withCredentials: true })
      .subscribe({
        next: ()  => {
          this.clearLocalData();
          this.router.navigate(['/login']);
        },
        error: () => {
          this.clearLocalData();
          this.router.navigate(['/login']);
        }
      });
  }

  private clearLocalData(): void {
    this._accessToken = null;
    sessionStorage.removeItem(this.SESSION_KEY);
    localStorage.removeItem(this.SESSION_KEY);
    localStorage.removeItem('user');
    this.rbacService.clearPermissions();
  }
  // ════════════════════════════════════════════════════════════════════
  // HELPERS
  // ════════════════════════════════════════════════════════════════════

  isAuthenticated(): boolean {
    const stored = localStorage.getItem(this.SESSION_KEY) || sessionStorage.getItem(this.SESSION_KEY);
    if (!stored) return false;
    return true;
  }

  /** Gets the token, silently refreshing it first if it has expired. */
  async getValidToken(): Promise<string | null> {
    const token = this.getToken();
    if (!token) return null;

    const stored = localStorage.getItem(this.SESSION_KEY) || sessionStorage.getItem(this.SESSION_KEY);
    if (stored) {
      try {
        const user: AuthUser = JSON.parse(stored);
        if (this.isTokenExpired(user.expiresAt)) {
          return await this.refreshToken();
        }
      } catch { /* ignore */ }
    }
    return token;
  }

  getCurrentUser(): AuthUser | null {
    const stored = localStorage.getItem(this.SESSION_KEY) || sessionStorage.getItem(this.SESSION_KEY);
    if (!stored) return null;
    try { return JSON.parse(stored) as AuthUser; } catch { return null; }
  }

  hasRole(role: 'Customer' | 'PM' | 'Assignee' | 'Manager' | 'Super Admin' | 'HOD'): boolean {
    return this.getCurrentUser()?.role === role;
  }

  private storeSession(user: AuthUser): void {
    this._accessToken = user.accessToken;

    if (user.permissions && user.dashboard) {
      this.rbacService.setPermissions(user.permissions, user.dashboard);
    }

    // Also keep legacy 'user' key in localStorage for backwards compatibility
    // with any code that reads localStorage.getItem('user')
    localStorage.setItem('user', JSON.stringify({
      id:   user.customerId ?? user.userId,
      role: user.role,
      name: user.name
    }));

    sessionStorage.setItem(this.SESSION_KEY, JSON.stringify(user));
    localStorage.setItem(this.SESSION_KEY, JSON.stringify(user));
  }

  private isTokenExpired(expiresAtIso: string): boolean {
    try {
      return new Date(expiresAtIso).getTime() <= Date.now();
    } catch { return true; }
  }

  getTicketDetailRoute(ticketId: number): string {
    const user = this.getCurrentUser();
    if (!user) return `/customer/ticket/${ticketId}`;
    const isExecutiveDefault = user.role === 'Assignee' || user.role === 'Developer';
    if (isExecutiveDefault) {
      return `/developer/ticket/${ticketId}`;
    } else {
      return `/pm/ticket/${ticketId}`;
    }
  }
}