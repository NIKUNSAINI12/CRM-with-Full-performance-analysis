import { Injectable } from '@angular/core';

@Injectable({
  providedIn: 'root'
})
export class RbacService {
  private _permissions: Set<string> = new Set<string>();
  private _dashboardRoute: string = '/login';

  constructor() {
    this.loadFromStorage();
  }

  /**
   * Called by AuthService upon successful login to store permissions
   */
  public setPermissions(permissions: string[], dashboardRoute: string): void {
    this._permissions = new Set<string>(permissions || []);
    this._dashboardRoute = dashboardRoute || '/login';
    
    // Save to localStorage for persistence across reloads
    localStorage.setItem('user_permissions', JSON.stringify(Array.from(this._permissions)));
    localStorage.setItem('user_dashboard', this._dashboardRoute);
  }

  /**
   * Check if the current user has a specific permission
   */
  public hasPermission(permissionKey: string): boolean {
    return this._permissions.has(permissionKey);
  }

  /**
   * Check if the current user has ANY of the specified permissions
   */
  public hasAnyPermission(permissionKeys: string[]): boolean {
    return permissionKeys.some(key => this._permissions.has(key));
  }

  /**
   * Check if the current user has ALL of the specified permissions
   */
  public hasAllPermissions(permissionKeys: string[]): boolean {
    return permissionKeys.every(key => this._permissions.has(key));
  }

  /**
   * Get the designated dashboard route for the current user's role
   */
  public getDashboardRoute(): string {
    return this._dashboardRoute;
  }

  /**
   * Clear permissions on logout
   */
  public clearPermissions(): void {
    this._permissions.clear();
    this._dashboardRoute = '/login';
    localStorage.removeItem('user_permissions');
    localStorage.removeItem('user_dashboard');
  }

  private loadFromStorage(): void {
    try {
      const storedPerms = localStorage.getItem('user_permissions');
      if (storedPerms) {
        const permsArray = JSON.parse(storedPerms);
        if (Array.isArray(permsArray)) {
          this._permissions = new Set<string>(permsArray);
        }
      }

      const storedDashboard = localStorage.getItem('user_dashboard');
      if (storedDashboard) {
        this._dashboardRoute = storedDashboard;
      }
    } catch (e) {
      console.warn('Could not load permissions from storage', e);
    }
  }
}
