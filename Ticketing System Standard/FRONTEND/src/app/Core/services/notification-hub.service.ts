import { Injectable, Inject, PLATFORM_ID } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { BehaviorSubject, Observable, tap } from 'rxjs';
import * as signalR from '@microsoft/signalr';
import { AuthService } from './auth';
import { API_BASE_URL } from '../../app';

export interface Notification {
  id: number;
  userId: number;
  title: string;
  message: string;
  entityType: string;
  entityId: number;
  redirectUrl: string;
  isRead: boolean;
  createdAt: string;
}

export interface NotificationResponse {
  notifications: Notification[];
  totalCount: number;
  unreadCount: number;
}

@Injectable({
  providedIn: 'root'
})
export class NotificationHubService {
  private hubConnection!: signalR.HubConnection;
  private hubUrl = API_BASE_URL.replace('/api', '') + '/notificationHub';
  private apiUrl = `${API_BASE_URL}/Notification`;

  private notificationsSubject = new BehaviorSubject<Notification[]>([]);
  public notifications$ = this.notificationsSubject.asObservable();

  private unreadCountSubject = new BehaviorSubject<number>(0);
  public unreadCount$ = this.unreadCountSubject.asObservable();

  private isConnectedSubject = new BehaviorSubject<boolean>(false);
  public isConnected$ = this.isConnectedSubject.asObservable();

  constructor(
    private authService: AuthService,
    private http: HttpClient,
    @Inject(PLATFORM_ID) private platformId: Object
  ) {
    if (this.authService.isAuthenticated()) {
      this.initConnection();
    }
  }

  public initConnection(): void {
    if (!isPlatformBrowser(this.platformId)) return;
    if (this.hubConnection && this.hubConnection.state !== signalR.HubConnectionState.Disconnected) {
      return;
    }

    this.hubConnection = new signalR.HubConnectionBuilder()
      .withUrl(this.hubUrl, {
        accessTokenFactory: () => this.authService.getValidToken().then(t => t || ''),
        skipNegotiation: true,
        transport: signalR.HttpTransportType.WebSockets
      })
      .withAutomaticReconnect()
      .build();

    this.hubConnection.on('ReceiveNotification', (notification: Notification) => {
      console.log('Received notification: ', notification);
      
      const current = this.notificationsSubject.value;
      // Prepend the new notification
      this.notificationsSubject.next([notification, ...current]);
      this.unreadCountSubject.next(this.unreadCountSubject.value + 1);
    });

    this.hubConnection.start()
      .then(() => {
        console.log('SignalR connected to NotificationHub.');
        this.isConnectedSubject.next(true);
        this.loadInitialNotifications();
      })
      .catch(err => {
        console.error('Error connecting to NotificationHub:', err);
        this.isConnectedSubject.next(false);
      });

    this.hubConnection.onclose(() => {
      this.isConnectedSubject.next(false);
    });
  }

  public stopConnection(): void {
    if (this.hubConnection && this.hubConnection.state !== signalR.HubConnectionState.Disconnected) {
      this.hubConnection.stop().then(() => {
        console.log('SignalR connection stopped.');
        this.isConnectedSubject.next(false);
      });
    }
  }

  public loadInitialNotifications(): void {
    this.getNotifications(undefined, 1, 20).subscribe({
      next: (res) => {
        this.notificationsSubject.next(res.notifications);
        this.unreadCountSubject.next(res.unreadCount);
      },
      error: (err) => {
        console.error('Failed to load initial notifications:', err);
      }
    });
  }

  public getNotifications(isRead?: boolean, pageNumber: number = 1, pageSize: number = 20): Observable<NotificationResponse> {
    let params: any = { pageNumber, pageSize };
    if (isRead !== undefined) {
      params.isRead = isRead;
    }
    
    const token = this.authService.getToken();
    const headers = { Authorization: `Bearer ${token}` };

    return this.http.get<NotificationResponse>(this.apiUrl, { params, headers });
  }

  public markAsRead(id: number): Observable<void> {
    const token = this.authService.getToken();
    const headers = { Authorization: `Bearer ${token}` };
    
    return this.http.put<void>(`${this.apiUrl}/${id}/read`, {}, { headers }).pipe(
      tap(() => {
        const current = this.notificationsSubject.value.map(n => {
          if (n.id === id) {
            return { ...n, isRead: true };
          }
          return n;
        });
        this.notificationsSubject.next(current);
        
        const newUnread = Math.max(0, this.unreadCountSubject.value - 1);
        this.unreadCountSubject.next(newUnread);
      })
    );
  }

  public markAllAsRead(): Observable<void> {
    const token = this.authService.getToken();
    const headers = { Authorization: `Bearer ${token}` };
    
    return this.http.put<void>(`${this.apiUrl}/read-all`, {}, { headers }).pipe(
      tap(() => {
        const current = this.notificationsSubject.value.map(n => ({ ...n, isRead: true }));
        this.notificationsSubject.next(current);
        this.unreadCountSubject.next(0);
      })
    );
  }

  public deleteNotification(id: number): Observable<void> {
    const token = this.authService.getToken();
    const headers = { Authorization: `Bearer ${token}` };

    return this.http.delete<void>(`${this.apiUrl}/${id}`, { headers }).pipe(
      tap(() => {
        const item = this.notificationsSubject.value.find(n => n.id === id);
        const current = this.notificationsSubject.value.filter(n => n.id !== id);
        this.notificationsSubject.next(current);

        if (item && !item.isRead) {
          const newUnread = Math.max(0, this.unreadCountSubject.value - 1);
          this.unreadCountSubject.next(newUnread);
        }
      })
    );
  }
}
