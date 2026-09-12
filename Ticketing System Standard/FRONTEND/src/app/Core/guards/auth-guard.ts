import { inject } from '@angular/core';
import { CanActivateFn, Router, ActivatedRouteSnapshot } from '@angular/router';
import { AuthService } from '../services/auth';

/**
 * Production Auth Guard — role-aware.
 *
 * Usage in routes:
 *   { path: 'pm/dashboard', canActivate: [authGuard], data: { roles: ['PM'] } }
 *   { path: 'customer/home', canActivate: [authGuard], data: { roles: ['Customer'] } }
 *   { path: 'developer/...',  canActivate: [authGuard], data: { roles: ['Assignee'] } }
 *   { path: 'some/page',      canActivate: [authGuard] }  // any authenticated user
 */
export const authGuard: CanActivateFn = (route: ActivatedRouteSnapshot) => {
  const auth   = inject(AuthService);
  const router = inject(Router);

  // ── 1. Must be authenticated ──────────────────────────────────────────
  if (!auth.isAuthenticated()) {
    const user = auth.getCurrentUser();
    if (user?.role === 'PM' || user?.role === 'Assignee' || user?.role === 'Developer' || user?.role === 'Manager' || user?.role === 'SuperManager' || user?.role === 'Super Admin' || user?.role === 'HOD') {
      router.navigate(['/login/team']);
    } else {
      router.navigate(['/login']);
    }
    return false;
  }

  // ── 2. Role check (if route specifies required roles) ─────────────────
  const requiredRoles = route.data?.['roles'] as string[] | undefined;
  if (requiredRoles && requiredRoles.length > 0) {
    const currentRole = auth.getCurrentUser()?.role;
    if (!currentRole || !requiredRoles.includes(currentRole)) {
      // Authenticated but wrong role — redirect to their own home
      redirectToHome(auth, router);
      return false;
    }
  }

  return true;
};

/** Redirect user to their own home page based on their role. */
function redirectToHome(auth: AuthService, router: Router): void {
  const user = auth.getCurrentUser();
  if (!user) { router.navigate(['/login']); return; }

  switch (user.role) {
    case 'Customer':
      router.navigate(['/customer/home', user.customerId ?? user.userId]);
      break;
    case 'PM':
    case 'Manager':
    case 'SuperManager':
    case 'Super Admin':
    case 'HOD':
      router.navigate(['/pm/dashboard', user.userId]);
      break;
    case 'Assignee':
    case 'Developer':
      router.navigate(['/developer/dashboard']);
      break;
    default:
      router.navigate(['/login']);
  }
}

// ── Legacy class-based guard for backwards compatibility ──────────────────
// (The routes already use [AuthGuard] as a class — keep this alias working)
export { authGuard as AuthGuard };