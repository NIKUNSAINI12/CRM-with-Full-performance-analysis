import { InjectionToken } from '@angular/core';
import { Ticket } from './models/ticket.model';
import { ProductData } from '../customer/ticket-create/ticket-create';

// --- Service Interfaces ---
export interface TicketService {
  // UPDATED: The method now accepts a customerId
  getAllTickets(customerId: number): Promise<Ticket[]>;
  getTicketsByStatus(status: string): Promise<any[]>;
  exportTickets(format: string): Promise<Blob>;
  getProductHierarchy(): Promise<ProductData[]>;
  getStatusWorkflows(): Promise<any[]>;
  getAssignmentWorkflows(): Promise<any[]>;
  getUserPreferences(userId: number, userRole: string): Promise<any>;
  saveUserPreferences(payload: any): Promise<any>;
}

export interface NotificationService {
  show(message: string, type: string): void;
}


// --- Injection Tokens ---
export const TICKET_SERVICE_TOKEN = new InjectionToken<TicketService>('TicketService');
export const NOTIFICATION_SERVICE_TOKEN = new InjectionToken<NotificationService>('NotificationService');
export const API_BASE_URL_TOKEN = new InjectionToken<string>('ApiBaseUrl');

