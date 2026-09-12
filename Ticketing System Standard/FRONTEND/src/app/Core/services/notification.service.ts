import { Injectable } from '@angular/core';

// Service interface
export interface NotificationService {
  show(message: string, type: 'success' | 'error' | 'info' | 'warn'): void;
}

// Default implementation
@Injectable()
export class DefaultNotificationService implements NotificationService {
  show(message: string, type: 'success' | 'error' | 'info' | 'warn'): void {
    // This can be enhanced later with a real toast notification library
    console.log(`[${type.toUpperCase()}] ${message}`);
    if (type === 'error' && typeof window !== 'undefined' && window.alert) {
      window.alert(`Error: ${message}`);
    }
  }
}

