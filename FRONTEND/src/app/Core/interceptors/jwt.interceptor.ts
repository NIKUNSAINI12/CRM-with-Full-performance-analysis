import { HttpInterceptorFn, HttpErrorResponse, HttpRequest, HttpHandlerFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, from, switchMap, throwError } from 'rxjs';
import { AuthService } from '../services/auth';

/**
 * Production JWT HTTP Interceptor
 *
 * 1. Attaches "Authorization: Bearer <token>" to every outgoing request (except auth endpoints).
 * 2. On 401 → attempts silent token refresh via httpOnly cookie.
 * 3. On refresh success → retries the original request with the new token.
 * 4. On refresh failure → logs out and redirects to login.
 */
export const jwtInterceptor: HttpInterceptorFn = (req, next) => {
  const auth   = inject(AuthService);
  const router = inject(Router);

  // ── Skip adding token to auth endpoints (they're public) ─────────────
  if (isAuthEndpoint(req.url)) {
    return next(req);
  }

  // ── Attach access token if available ─────────────────────────────────
  const token      = auth.getToken();
  const authorised = token ? addToken(req, token) : req;

  return next(authorised).pipe(
    catchError((error: HttpErrorResponse) => {
      // ── On 401: try silent refresh ──────────────────────────────────
      if (error.status === 401 && !isRefreshEndpoint(req.url)) {
        return from(auth.refreshToken()).pipe(
          switchMap(newToken => {
            if (newToken) {
              // Retry original request with fresh token
              return next(addToken(req, newToken));
            }
            // Refresh failed → force logout
            auth.logout();
            return throwError(() => error);
          }),
          catchError(refreshError => {
            auth.logout();
            return throwError(() => refreshError);
          })
        );
      }

      // ── On 403: redirect to a forbidden page (or just notify) ───────
      if (error.status === 403) {
        console.error('[JWT] 403 Forbidden — insufficient role for this resource.');
      }

      return throwError(() => error);
    })
  );
};

// ── Helpers ───────────────────────────────────────────────────────────────

function addToken(req: HttpRequest<unknown>, token: string): HttpRequest<unknown> {
  return req.clone({
    setHeaders: { Authorization: `Bearer ${token}` }
  });
}

function isAuthEndpoint(url: string): boolean {
  return url.includes('/api/auth/login') ||
         url.includes('/api/auth/pm-login') ||
         url.includes('/api/auth/assignee-login') ||
         url.includes('/api/auth/team-login');
}

function isRefreshEndpoint(url: string): boolean {
  return url.includes('/api/auth/refresh');
}
