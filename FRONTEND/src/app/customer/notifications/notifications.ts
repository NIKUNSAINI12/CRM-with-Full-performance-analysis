import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { NotificationHubService, Notification } from '../../Core/services/notification-hub.service';
import { AuthService } from '../../Core/services/auth';

@Component({
  selector: 'app-notifications',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './notifications.html',
  styleUrls: ['./notifications.scss']
})
export class NotificationsComponent implements OnInit {
  public notifications: Notification[] = [];
  public currentFilter: 'all' | 'unread' | 'read' = 'all';
  public loading = false;
  public totalCount = 0;
  public unreadCount = 0;

  constructor(
    private notificationService: NotificationHubService,
    private authService: AuthService,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.loadNotifications();
  }

  public loadNotifications(): void {
    this.loading = true;
    let isRead: boolean | undefined = undefined;
    if (this.currentFilter === 'unread') isRead = false;
    else if (this.currentFilter === 'read') isRead = true;

    this.notificationService.getNotifications(isRead, 1, 100).subscribe({
      next: (res) => {
        this.notifications = res.notifications;
        this.totalCount = res.totalCount;
        this.unreadCount = res.unreadCount;
        this.loading = false;
      },
      error: (err) => {
        console.error('Failed to load notifications:', err);
        this.loading = false;
      }
    });
  }

  public filterNotifications(filterType: 'all' | 'unread' | 'read'): void {
    this.currentFilter = filterType;
    this.loadNotifications();
  }

  public markAsRead(notification: Notification, event: MouseEvent): void {
    event.stopPropagation(); // Avoid triggering navigation
    if (notification.isRead) return;

    this.notificationService.markAsRead(notification.id).subscribe({
      next: () => {
        notification.isRead = true;
        this.unreadCount = Math.max(0, this.unreadCount - 1);
        if (this.currentFilter === 'unread') {
          this.notifications = this.notifications.filter(n => n.id !== notification.id);
        }
      }
    });
  }

  public markAllAsRead(): void {
    this.notificationService.markAllAsRead().subscribe({
      next: () => {
        this.notifications = this.notifications.map(n => ({ ...n, isRead: true }));
        this.unreadCount = 0;
        if (this.currentFilter === 'unread') {
          this.notifications = [];
        }
      }
    });
  }

  public deleteNotification(id: number, event: MouseEvent): void {
    event.stopPropagation();
    this.notificationService.deleteNotification(id).subscribe({
      next: () => {
        this.notifications = this.notifications.filter(n => n.id !== id);
        this.loadNotifications();
      }
    });
  }

  public handleNotificationClick(notification: Notification): void {
    // 1. Mark as read first if unread
    if (!notification.isRead) {
      this.notificationService.markAsRead(notification.id).subscribe();
    }

    // 2. Navigate based on redirect URL and User role
    let url = notification.redirectUrl;
    if (url.startsWith('/tickets/')) {
      const ticketId = url.replace('/tickets/', '');
      const user = this.authService.getCurrentUser();
      const role = user?.role;
      this.router.navigate([this.authService.getTicketDetailRoute(Number(ticketId))]);
    } else {
      this.router.navigate([url]);
    }
  }

  public getIconClass(entityType: string): string {
    switch (entityType?.toLowerCase()) {
      case 'ticket':
        return 'confirmation_number';
      case 'customer':
        return 'business';
      default:
        return 'notifications';
    }
  }

  public formatTime(dateStr: string): string {
    try {
      const date = new Date(dateStr);
      const now = new Date();
      const diffMs = now.getTime() - date.getTime();
      const diffMins = Math.floor(diffMs / 60000);
      const diffHrs = Math.floor(diffMins / 60);

      if (diffMins < 1) return 'Just now';
      if (diffMins < 60) return `${diffMins}m ago`;
      if (diffHrs < 24) return `${diffHrs}h ago`;
      
      return date.toLocaleDateString(undefined, { month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' });
    } catch {
      return dateStr;
    }
  }
}
