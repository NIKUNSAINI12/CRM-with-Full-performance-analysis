import { Component, OnInit, Inject } from '@angular/core';
import { RouterOutlet, Router, NavigationEnd, RouterModule } from '@angular/router';
import { CommonModule } from '@angular/common';
import { filter } from 'rxjs/operators';
import { AuthService } from './Core/services/auth'; // Adjust path if needed
import { TICKET_SERVICE_TOKEN } from './Core/injection-tokens';
import { TicketService } from './Core/services/ticket.service';
import { RbacService } from './Core/services/rbac.service';
import { NotificationHubService } from './Core/services/notification-hub.service';

export const API_BASE_URL = 'http://localhost:5058/api';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [
    RouterOutlet,
    CommonModule,
    RouterModule
  ],
  templateUrl: './app.html',
  styleUrls: ['./app.scss'],
})
export class App implements OnInit {
  title = 'ticketing-system';
  showHeader = true;
  sidebarCollapsed = false;
  isLightTheme = false;

  public userName: string = '';
  public userInitialsStr: string = '';
  public loginId: string = '';
  private lastUserId: string | null = null;
  public showCreateUserMenu = false;

  public showNotificationsDropdown = false;
  public dropdownNotifications: any[] = [];
  public dropdownUnreadCount = 0;

  constructor(
    private router: Router,
    private authService: AuthService, // Inject the AuthService
    public rbacService: RbacService, // Inject the RbacService for UI template usage
    @Inject(TICKET_SERVICE_TOKEN) private ticketService: TicketService,
    public notificationService: NotificationHubService
  ) { }

  ngOnInit() {
    // Theme Toggling Setup
    const savedTheme = localStorage.getItem('theme');
    if (savedTheme === 'light') {
      this.isLightTheme = true;
      document.documentElement.classList.add('light');
    } else {
      this.isLightTheme = false;
      document.documentElement.classList.remove('light');
    }

    const savedState = localStorage.getItem('sidebarCollapsed');
    if (savedState !== null) {
      this.sidebarCollapsed = JSON.parse(savedState);
    }
    this.checkRoute(this.router.url);
    this.router.events.pipe(
      filter(event => event instanceof NavigationEnd)
    ).subscribe((event: NavigationEnd) => {
      this.showNotificationsDropdown = false; // close dropdown on route changes
      this.checkRoute(event.url);
    });

    // Reactive notification subscriptions
    this.notificationService.notifications$.subscribe(notes => {
      this.dropdownNotifications = notes.slice(0, 5);
    });
    this.notificationService.unreadCount$.subscribe(count => {
      this.dropdownUnreadCount = count;
    });
  }

  // Getter to check if the user is logged in
  public get isLoggedIn(): boolean {
    return this.authService.isAuthenticated();
  }

  // Getter to fetch the user role
  public get userRole(): string | null {
    const user = this.authService.getCurrentUser();
    return user ? user.role : null;
  }

  // Getter to fetch the user identifier
  public get userId(): string | null {
    const user = this.authService.getCurrentUser();
    return user ? user.userId.toString() : null;
  }

  // Getter to create initials for the profile button
  public get userInitials(): string {
    if (this.userInitialsStr) {
      return this.userInitialsStr;
    }
    const user = this.authService.getCurrentUser();
    return user ? user.role.charAt(0).toUpperCase() : 'U';
  }

  toggleSidebar(): void {
    this.sidebarCollapsed = !this.sidebarCollapsed;
    localStorage.setItem('sidebarCollapsed', JSON.stringify(this.sidebarCollapsed));
    if (this.sidebarCollapsed) {
      this.showCreateUserMenu = false;
    }
  }

  toggleCreateUserMenu(): void {
    if (this.sidebarCollapsed) {
      this.toggleSidebar(); // Expand sidebar if trying to open menu
    }
    this.showCreateUserMenu = !this.showCreateUserMenu;
  }

  toggleTheme(): void {
    this.isLightTheme = !this.isLightTheme;
    if (this.isLightTheme) {
      document.documentElement.classList.add('light');
      localStorage.setItem('theme', 'light');
    } else {
      document.documentElement.classList.remove('light');
      localStorage.setItem('theme', 'dark');
    }
  }

  logout(): void {
    if (!confirm('Are you sure you want to log out?')) return;
    this.authService.logout();
  }

  toggleNotificationsDropdown(event: MouseEvent): void {
    event.stopPropagation();
    this.showNotificationsDropdown = !this.showNotificationsDropdown;
    if (this.showNotificationsDropdown) {
      this.notificationService.loadInitialNotifications();
    }
  }

  markNotificationAsRead(notification: any, event: MouseEvent): void {
    event.stopPropagation();
    this.notificationService.markAsRead(notification.id).subscribe();
  }

  markAllNotificationsAsRead(event: MouseEvent): void {
    event.stopPropagation();
    this.notificationService.markAllAsRead().subscribe();
  }

  handleNotificationClick(notification: any): void {
    this.showNotificationsDropdown = false;
    if (!notification.isRead) {
      this.notificationService.markAsRead(notification.id).subscribe();
    }

    let url = notification.redirectUrl;
    if (url.startsWith('/tickets/')) {
      const ticketId = url.replace('/tickets/', '');
      const user = this.authService.getCurrentUser();
      const role = user?.role;
      if (role === 'PM') {
        this.router.navigate([`/pm/ticket/${ticketId}`]);
      } else if (role === 'Assignee') {
        this.router.navigate([`/developer/ticket/${ticketId}`]);
      } else {
        this.router.navigate([`/customer/ticket/${ticketId}`]);
      }
    } else {
      this.router.navigate([url]);
    }
  }

  openQuickTicket(): void {
    if (this.rbacService.hasPermission('view_pm_workspace')) {
      this.router.navigate(['/pm/dashboard'], { queryParams: { openCreateDrawer: 'true' } });
    } else if (this.authService.getCurrentUser()?.role === 'Customer') {
      this.router.navigate(['/customer/ticket-create']);
    }
  }

  private checkRoute(url: string) {
    this.showHeader = !(url === '/login' || url.startsWith('/login/'));
    
    // Check and load user details
    const currentUser = this.authService.getCurrentUser();
    if (currentUser) {
      const uid = currentUser.userId.toString();
      if (uid !== this.lastUserId) {
        this.lastUserId = uid;
        this.loadUserDetails(currentUser);
        this.notificationService.initConnection();
      }
    } else {
      if (this.lastUserId !== null) {
        this.notificationService.stopConnection();
      }
      this.lastUserId = null;
      this.userName = '';
      this.userInitialsStr = '';
      this.loginId = '';
    }
  }

  private async loadUserDetails(user: { userId: number | string; role: string; userNumber?: string }) {
    const uid = Number(user.userId);
    this.loginId = user.userNumber || '';

    if (!uid) {
      this.userName = '';
      this.userInitialsStr = '';
      return;
    }

    try {
      if (user.role === 'PM') {
        const pm = await this.ticketService.getUserById(uid);
        if (pm) {
          if (pm.fullName) {
            this.userName = pm.fullName;
            this.userInitialsStr = this.calculateInitials(pm.fullName);
          }
          if (pm.userNumber) {
            this.loginId = pm.userNumber;
          }
        }
      } else if (user.role === 'Assignee') {
        const assignee = await this.ticketService.getAssigneeById(uid);
        if (assignee) {
          if (assignee.fullName) {
            this.userName = assignee.fullName;
            this.userInitialsStr = this.calculateInitials(assignee.fullName);
          }
          if (assignee.assigneeNumber) {
            this.loginId = assignee.assigneeNumber;
          }
        }
      } else if (user.role === 'Customer') {
        const customer = await this.ticketService.getCustomerById(uid);
        if (customer) {
          if (customer.name) {
            this.userName = customer.name;
            this.userInitialsStr = this.calculateInitials(customer.name);
          }
        }
      }
    } catch (error) {
      console.error('Failed to load user details in App:', error);
      // fallback
      const currentUser = this.authService.getCurrentUser();
      this.userName = currentUser?.name || user.role;
      this.userInitialsStr = this.userName ? this.calculateInitials(this.userName) : user.role.charAt(0).toUpperCase();
    }
  }

  private calculateInitials(name: string): string {
    if (!name) return 'U';
    const parts = name.trim().split(/\s+/);
    if (parts.length === 0) return 'U';
    let initials = parts[0].charAt(0).toUpperCase();
    if (parts.length > 1) {
      initials += parts[parts.length - 1].charAt(0).toUpperCase();
    }
    return initials.substring(0, 2);
  }
}
