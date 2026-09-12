import { Component, OnInit, ViewEncapsulation, Input, ViewChild, Optional, Inject } from '@angular/core';
import { Router, ActivatedRoute, RouterModule } from '@angular/router';
import { DropDownListModule } from "@syncfusion/ej2-angular-dropdowns";
import { TabModule } from "@syncfusion/ej2-angular-navigations";
import { GridModule, PageService, SortService, FilterService, ExcelExportService, ToolbarService, ColumnChooserService } from "@syncfusion/ej2-angular-grids";
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Injectable, InjectionToken } from '@angular/core';
import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
import { Observable, firstValueFrom } from 'rxjs';
import { OnDestroy } from '@angular/core';
import { API_BASE_URL } from '../../app';

// --- SERVICE INTERFACES & TOKENS (keeping existing ones) ---
export interface TicketCommunicationDto {
  id: number;
  ticketId: number;
  commentText: string;
  channel: string;
  postedByUserId: number;
  postedByName: string;
  postedByUserRole: string;
  postedOn: string;
  attachments?: any[];
}

export interface TicketService {
  getAllTickets(customerId: number, filters?: FilterCriteria): Promise<TicketResponse>;
  getTicketsByStatus(status: string): Promise<TicketResponse>;
  updateTicket(ticketId: number, updateData: UpdateTicketRequest): Promise<void>;
  exportTickets(format: string, customerId: number): Promise<Blob>;
  getTicketAttachment(ticketId: number, attachmentId: string): Promise<Blob>;
  createTicket(ticketData: FormData): Promise<Ticket>;
  getTicketHistory(ticketId: number): Promise<TicketHistory[]>;
  updateTicketByCustomer(ticketId: number, customerId: number, updateData: CustomerUpdateTicketRequest): Promise<void>;
  getCommunications(ticketId: number): Observable<any>;
  addCommunication(ticketId: number, formData: FormData): Observable<any>;
}

export interface TicketHistory {
  eventDescription: string;
  changedByUserName: string;
  changedByUserRole: string;
  timestamp: string;
}

export interface NotificationService {
  show(message: string, type: 'success' | 'error' | 'info'): void;
}

// Updated FilterCriteria interface
export interface FilterCriteria {
  status?: string;        // Single status selection
  priority?: string;      // Single priority selection
  product?: string[];     // Keep as array for future use
  subProduct?: string[];  // Keep as array for future use
  dateFrom?: string;
  dateTo?: string;
  assignedTo?: string[];  // Keep as array for future use
  ticketNumber?: string;  // New: ticket number search
}

export interface CustomerUpdateTicketRequest {
  status?: string;
  priority?: string;
}

export interface UpdateTicketRequest {
  status?: string;
  priority?: string;
  assignedToId?: number;
}

export interface TicketAttachment {
  id: string;
  fileName: string;
  filePath: string;
  fileSize: number;
  contentType: string;
  uploadedOn: string;
  uploadedBy: string;
}

export interface Ticket {
  id: number;
  ticketNumber?: string;  // Make sure this exists
  subject: string;
  description: string;
  status: string;
  priority: string;
  product?: string;
  subProduct?: string;
  assignedTo?: string;
  assignedToName?: string;
  createdOn: string;
  lastRepliedOn?: string;
  customerId: number;
  customerName?: string;
  createdByUserId?: number;
  createdByName?: string;
  createdByRole?: string;
  attachments?: TicketAttachment[];
  hasAttachment: boolean;
  attachmentCount: number;
  closingRemark?: string;
  closeNote?: string;
  resolutionNote?: string;
  closingNote?: string;
  remark?: string;
}

export interface TicketResponse {
  tickets: Ticket[];
  totalCount: number;
}

export const TICKET_SERVICE_TOKEN = new InjectionToken<TicketService>('TicketService');
export const NOTIFICATION_SERVICE_TOKEN = new InjectionToken<NotificationService>('NotificationService');
export const API_BASE_URL_TOKEN = new InjectionToken<string>('ApiBaseUrl');

// --- DEFAULT SERVICE IMPLEMENTATIONS (keeping existing ones) ---
@Injectable()
export class DefaultTicketService implements TicketService {
  constructor(
    @Inject(API_BASE_URL_TOKEN) private apiBaseUrl: string,
    private http: HttpClient
  ) { }

  getCommunications(ticketId: number): Observable<any> {
    // Matches the implementation in Core/services/ticket.service.ts
    // Assumes the backend route is available.
    const url = `${this.apiBaseUrl}/manager/tickets/${ticketId}/communications`;
    return this.http.get<any>(url);
  }

  addCommunication(ticketId: number, formData: FormData): Observable<any> {
    // Matches the implementation in Core/services/ticket.service.ts
    const url = `${this.apiBaseUrl}/manager/tickets/${ticketId}/communications`;
    return this.http.post<any>(url, formData);
  }

  async getAllTickets(customerId: number, filters?: FilterCriteria): Promise<TicketResponse> {
    if (!customerId) {
      console.warn('Customer ID is missing, cannot fetch tickets.');
      return { tickets: [], totalCount: 0 };
    }

    try {
      let params = new HttpParams();

      if (filters?.status) {
        params = params.set('status', filters.status);
      }
      if (filters?.priority) {
        params = params.set('priority', filters.priority);
      }
      if (filters?.product && filters.product.length > 0) {
        params = params.set('product', filters.product.join(','));
      }
      if (filters?.subProduct && filters.subProduct.length > 0) {
        params = params.set('subProduct', filters.subProduct.join(','));
      }
      if (filters?.dateFrom) {
        params = params.set('dateFrom', filters.dateFrom);
      }
      if (filters?.dateTo) {
        params = params.set('dateTo', filters.dateTo);
      }
      if (filters?.ticketNumber) {
        params = params.set('ticketNumber', filters.ticketNumber);
      }

      const response = await firstValueFrom(
        this.http.get<TicketResponse>(`${this.apiBaseUrl}/Tickets/customer/${customerId}`, { params })
      );

      return response;
    } catch (error) {
      console.error('Failed to fetch tickets:', error);
      throw error;
    }
  }

  async updateTicketByCustomer(ticketId: number, customerId: number, updateData: CustomerUpdateTicketRequest): Promise<void> {
    try {
      await firstValueFrom(
        this.http.put(`${this.apiBaseUrl}/Manager/${ticketId}/customer/${customerId}/update`, updateData)
      );
    } catch (error) {
      console.error('Failed to update ticket by customer:', error);
      throw error;
    }
  }

  async updateTicket(ticketId: number, updateData: UpdateTicketRequest): Promise<void> {
    try {
      await firstValueFrom(
        this.http.put(`${this.apiBaseUrl}/Tickets/${ticketId}`, updateData)
      );
    } catch (error) {
      console.error('Failed to update ticket:', error);
      throw error;
    }
  }

  async getTicketHistory(ticketId: number): Promise<TicketHistory[]> {
    try {
      const response = await firstValueFrom(
        this.http.get<TicketHistory[]>(`${this.apiBaseUrl}/Tickets/${ticketId}/history`)
      );
      return response;
    } catch (error) {
      console.error('Failed to fetch ticket history:', error);
      throw error;
    }
  }

  async createTicket(ticketData: FormData): Promise<Ticket> {
    try {
      const response = await firstValueFrom(
        this.http.post<Ticket>(`${this.apiBaseUrl}/Tickets`, ticketData)
      );
      return response;
    } catch (error) {
      console.error('Failed to create ticket:', error);
      throw error;
    }
  }

  async getTicketsByStatus(status: string): Promise<TicketResponse> {
    try {
      const response = await firstValueFrom(
        this.http.get<TicketResponse>(`${this.apiBaseUrl}/Tickets/status/${status}`)
      );
      return response;
    } catch (error) {
      console.error('Failed to fetch tickets by status:', error);
      throw error;
    }
  }

  async exportTickets(format: string, customerId: number): Promise<Blob> {
    try {
      const response = await firstValueFrom(
        this.http.get(`${this.apiBaseUrl}/Tickets/export/${format}/customer/${customerId}`,
          { responseType: 'blob' })
      );
      return response;
    } catch (error) {
      console.error(`Failed to export tickets as ${format}:`, error);
      throw error;
    }
  }

  async getTicketAttachment(ticketId: number, attachmentId: string): Promise<Blob> {
    try {
      const response = await firstValueFrom(
        this.http.get(`${this.apiBaseUrl}/Tickets/${ticketId}/attachments/${attachmentId}`,
          { responseType: 'blob' })
      );
      return response;
    } catch (error) {
      console.error('Failed to fetch attachment:', error);
      throw error;
    }
  }
}

@Injectable()
export class DefaultNotificationService implements NotificationService {
  show(message: string, type: string): void {
    console.log(`[${type.toUpperCase()}] ${message}`);

    const toast = document.createElement('div');
    toast.className = `toast toast-${type}`;
    toast.textContent = message;
    toast.style.cssText = `
      position: fixed;
      top: 20px;
      right: 20px;
      background: ${type === 'error' ? '#dc2626' : type === 'success' ? '#16a34a' : '#3b82f6'};
      color: white;
      padding: 12px 24px;
      border-radius: 6px;
      z-index: 10000;
      max-width: 400px;
      box-shadow: 0 4px 6px rgba(0, 0, 0, 0.1);
      animation: slideIn 0.3s ease-out;
    `;

    document.body.appendChild(toast);

    setTimeout(() => {
      toast.style.animation = 'slideOut 0.3s ease-in';
      setTimeout(() => document.body.removeChild(toast), 300);
    }, 5000);
  }
}

// --- THE MAIN COMPONENT ---
@Component({
  selector: 'app-home',
  standalone: true,
  imports: [RouterModule, DropDownListModule, TabModule, CommonModule, GridModule, FormsModule,],
  templateUrl: './home.html',
  styleUrls: ['./home.scss'],
  encapsulation: ViewEncapsulation.None,
  providers: [
    PageService,
    SortService,
    FilterService,
    ExcelExportService,
    ToolbarService,
    ColumnChooserService,
    { provide: API_BASE_URL_TOKEN, useValue: API_BASE_URL },
    { provide: TICKET_SERVICE_TOKEN, useClass: DefaultTicketService },
    { provide: NOTIFICATION_SERVICE_TOKEN, useClass: DefaultNotificationService },
  ]
})
export class HomeComponent implements OnInit, OnDestroy {
  @Input() allowExport: boolean = true;
  @Input() allowColumnManagement: boolean = true;
  @Input() allowNewTicketCreation: boolean = true;
  @Input() defaultHeight: number = 400;

  private clickOutsideListener?: (e: Event) => void;

  // --- Data Properties ---
  public allTickets: Ticket[] = [];
  public filteredTickets: Ticket[] = [];
  public loading: boolean = true;
  public error: string | null = null;
  public statusData: string[] = [];
  public priorityData: string[] = [];
  public productData: string[] = [];
  public subProductData: string[] = [];
  public assignedToData: string[] = [];
  public toolbarOptions: (string | object)[] = [];
  private customerId: number | undefined;

  // --- Tab Management ---
  public activeTab: 'all' | 'waiting' | 'open' | 'closed' = 'all';

  // --- Dropdown States ---
  public dropdownStates: { [key: string]: boolean } = {};
  public dropdownPositions: { [key: string]: { top: number, left: number } } = {};

  // --- Modal States ---
  public showFilterModal: boolean = false;
  public showFileModal: boolean = false;
  public showHistoryModal: boolean = false;
  public showCloseConfirmModal: boolean = false;
  public selectedFileAttachment: any = null;
  public selectedTicketHistory: { ticket: Ticket, history: TicketHistory[] } | null = null;
  public ticketToClose: { id: number, currentStatus: string } | null = null;

  // --- Status Options ---
  public statusOptions: string[] = ['Open', 'Closed', 'Work Done'];
  public priorityOptions: string[] = ['Low', 'Medium', 'High', 'Urgent'];
  public customerStatusOptions: string[] = ['Open', 'Closed'];

  // --- Inline Editing Properties ---
  public editingField: string | null = null;
  public editingValue: string = '';

  // --- Active Filters (Updated structure) ---
  public activeFilters: FilterCriteria = {};

  // --- Grid Settings ---
  public pageSettings = { pageSize: 20, pageSizes: true };
  public currentPage: number = 1;
  public pageSize: number = 20;
  public pageSizeOptions: number[] = [10, 20, 50, 100];
  public sortColumn: string = 'id';
  public sortDirection: 'asc' | 'desc' = 'desc';

  // --- Configuration ---
  public config = {
    ui: {
      emptyMessage: 'No tickets were found for this account.',
      loadingMessage: 'Loading your tickets...'
    }
  };

  stats: any;

  constructor(
    private router: Router,
    private route: ActivatedRoute,
    @Optional() @Inject(TICKET_SERVICE_TOKEN) private ticketService?: TicketService,
    @Optional() @Inject(NOTIFICATION_SERVICE_TOKEN) private notificationService?: NotificationService
  ) {
    this.toolbarOptions = [];
  }

  ngOnInit(): void {
    const session = sessionStorage.getItem('auth_user');
    if (session) {
      try {
        const user = JSON.parse(session);
        this.customerId = Number(user.customerId || user.userId);
        this.loadInitialData(this.customerId);
      } catch (e) {
        this.handleError("Failed to parse user session.", null);
      }
    } else {
      this.handleError("No Customer ID found in the session.", null);
    }
  }

  ngOnDestroy(): void {
    this.closeAllDropdowns();
  }

  // --- Tab Management Methods ---
  public switchTab(tab: 'all' | 'open' | 'closed' | 'waiting'): void { // Add 'waiting'
    this.activeTab = tab;
    this.activeFilters = {};

    switch (tab) {
      case 'open':
        this.activeFilters.status = 'Open';
        break;
      case 'closed':
        this.activeFilters.status = 'Closed';
        break;
      case 'waiting': // <-- ADD THIS CASE
        this.activeFilters.status = 'Work Done';
        break;
      case 'all':
        break;
    }

    this.applyClientSideFilters();
    this.showNotification(`Switched to ${tab} tickets view`, 'info');
  }

  // --- Filter Management Methods ---
  public onFilterChange(): void {
    // Apply filters whenever any filter value changes
    this.applyClientSideFilters();
  }

  public hasActiveFilters(): boolean {
    return !!(
      this.activeFilters.status ||
      this.activeFilters.priority ||
      this.activeFilters.ticketNumber ||
      this.activeFilters.dateFrom ||
      this.activeFilters.dateTo ||
      (this.activeFilters.product && this.activeFilters.product.length > 0) ||
      (this.activeFilters.subProduct && this.activeFilters.subProduct.length > 0)
    );
  }

  public clearFilter(filterKey: keyof FilterCriteria): void {
    if (filterKey === 'dateFrom' || filterKey === 'dateTo') {
      this.activeFilters.dateFrom = undefined;
      this.activeFilters.dateTo = undefined;
    } else {
      this.activeFilters[filterKey] = undefined;
    }
    this.applyClientSideFilters();
  }

  public clearDateFilter(): void {
    this.activeFilters.dateFrom = undefined;
    this.activeFilters.dateTo = undefined;
    this.applyClientSideFilters();
  }

  public clearAllFilters(): void {
    const tabStatusFilter = this.activeTab === 'open' ? 'Open' :
      this.activeTab === 'closed' ? 'Closed' :
        this.activeTab === 'waiting' ? 'Work Done' : undefined; // <-- ADD THIS

    this.activeFilters = {};

    if (tabStatusFilter) {
      this.activeFilters.status = tabStatusFilter;
    }

    this.applyClientSideFilters();
    this.showNotification('Filters cleared.', 'info');
  }

  public formatDateRange(): string {
    const from = this.activeFilters.dateFrom;
    const to = this.activeFilters.dateTo;
    if (from && to) {
      return `${this.formatDate(from)} - ${this.formatDate(to)}`;
    }
    if (from) {
      return `From ${this.formatDate(from)}`;
    }
    if (to) {
      return `Until ${this.formatDate(to)}`;
    }
    return '';
  }

  // --- Empty State Methods ---
  public getEmptyStateMessage(): string {
    if (this.hasActiveFilters()) {
      return 'No tickets match your filters';
    }

    switch (this.activeTab) {
      case 'open':
        return 'No open tickets found';
      case 'closed':
        return 'No closed tickets found';
      case 'waiting': // <-- ADD THIS CASE
        return 'No tickets are waiting for your confirmation';
      default:
        return this.config.ui.emptyMessage;
    }
  }

  public getEmptyStateDescription(): string {
    if (this.hasActiveFilters()) {
      return 'Try adjusting your filter criteria or clearing filters to see more results.';
    }

    switch (this.activeTab) {
      case 'open':
        return 'All tickets have been closed or there are no tickets yet.';
      case 'closed':
        return 'No tickets have been closed yet.';
      case 'waiting': // <-- ADD THIS CASE
        return 'Tickets with completed work will appear here for you to review and close.';

      default:
        return 'No tickets found. Create your first ticket to get started.';
    }
  }



  public getClosedTicketCount(): number {
    return this.allTickets.filter(ticket => ticket.status === 'Closed').length;
  }

  public getHighPriorityCount(): number {
    return this.allTickets.filter(ticket =>
      ticket.priority === 'High' || ticket.priority === 'Urgent'
    ).length;
  }
  public getOpenTicketCount(): number {
    // Exclude 'Closed' and 'Work Done' from the 'Open' count
    return this.allTickets.filter(ticket => ticket.status !== 'Closed' && ticket.status !== 'Work Done').length;
  }

  public getWorkDoneTicketCount(): number {
    return this.allTickets.filter(ticket => ticket.status === 'Work Done').length;
  }

  // --- Status Display Methods ---
  public getDisplayStatus(ticket: Ticket): string {
    if (ticket.status === 'Closed') return 'Closed';
    if (ticket.status === 'Work Done') return 'Work Done';
    // If ticket is assigned to someone and not closed → show 'Assigned'
    if (ticket.assignedToName || ticket.assignedTo) return 'Assigned';
    return 'Open'; // All other non-assigned statuses show as 'Open'
  }

  public getCustomerStatusOptions(ticket: Ticket): string[] {
    // If work is done, customer can either re-open or close it.
    if (ticket.status === 'Work Done') {
      return ['Open', 'Closed'];
    }
    // If it's open, the only action for a customer is to close it.
    if (ticket.status !== 'Closed') {
      return ['Closed'];
    }
    return []; // No options for already closed tickets.
  }

  public canEditTicketStatus(ticket: Ticket): boolean {
    return ticket.status !== 'Closed';
  }

  // --- Data Loading ---
  private async loadInitialData(customerId: number): Promise<void> {
    this.loading = true;
    this.error = null;
    this.allTickets = [];

    try {
      if (this.ticketService) {
        const response = await this.ticketService.getAllTickets(customerId, this.activeFilters);

        // Sort tickets by creation date in descending order (newest first)
        const sortedTickets = response.tickets.sort((a, b) =>
          new Date(b.createdOn).getTime() - new Date(a.createdOn).getTime()
        );

        this.allTickets = sortedTickets;
        this.applyClientSideFilters(); // Apply current tab and filters

        if (this.allTickets.length > 0) {
          this.showNotification('Tickets loaded successfully.', 'success');
        }
        this.loadFilterOptions();
      } else {
        throw new Error("Ticket service is not available.");
      }
    } catch (error: any) {
      this.handleError('Failed to load ticket data from the server.', error);
    } finally {
      this.loading = false;
    }
  }

  private loadFilterOptions(): void {
    const statuses = new Set<string>();
    const priorities = new Set<string>();
    const products = new Set<string>();
    const subProducts = new Set<string>();
    const assignedTo = new Set<string>();

    this.allTickets.forEach(ticket => {
      if (ticket.status) statuses.add(ticket.status);
      if (ticket.priority) priorities.add(ticket.priority);
      if (ticket.product) products.add(ticket.product);
      if (ticket.subProduct) subProducts.add(ticket.subProduct);
      if (ticket.assignedTo) assignedTo.add(ticket.assignedTo);
    });

    this.statusData = Array.from(statuses);
    this.priorityData = Array.from(priorities);
    this.productData = Array.from(products);
    this.subProductData = Array.from(subProducts);
    this.assignedToData = Array.from(assignedTo);
  }

  private applyClientSideFilters(): void {
    let filtered = [...this.allTickets];

    // Apply tab-based filtering first
    switch (this.activeTab) {
      case 'open':
        // An 'Open' ticket is one that is NOT Closed and NOT Work Done
        filtered = filtered.filter(ticket => ticket.status !== 'Closed' && ticket.status !== 'Work Done');
        break;
      case 'waiting':
        // The 'waiting' tab should only show tickets with 'Work Done' status
        filtered = filtered.filter(ticket => ticket.status === 'Work Done');
        break;
      case 'closed':
        filtered = filtered.filter(ticket => ticket.status === 'Closed');
        break;
      case 'all':
        // No primary filtering is done for the 'all' tab; it respects the individual filters below.
        break;
    }

    // Apply individual filters from the dropdowns/inputs.
    // This section applies secondary filters on top of the tab selection.

    // The status filter dropdown should only apply when on the 'all' tab, 
    // as other tabs have a fixed status.
    if (this.activeTab === 'all' && this.activeFilters.status) {
      if (this.activeFilters.status === 'Open') {
        filtered = filtered.filter(ticket => ticket.status !== 'Closed' && ticket.status !== 'Work Done');
      } else {
        // This handles 'Closed', 'Work Done', or any other specific status from the dropdown
        filtered = filtered.filter(ticket => ticket.status === this.activeFilters.status);
      }
    }

    if (this.activeFilters.priority) {
      filtered = filtered.filter(ticket => ticket.priority === this.activeFilters.priority);
    }

    if (this.activeFilters.ticketNumber) {
      const searchTerm = this.activeFilters.ticketNumber.toLowerCase();
      filtered = filtered.filter(ticket =>
        ticket.ticketNumber?.toLowerCase().includes(searchTerm) ||
        ticket.id.toString().includes(searchTerm)
      );
    }

    if (this.activeFilters.product && this.activeFilters.product.length > 0) {
      filtered = filtered.filter(ticket =>
        this.activeFilters.product!.includes(ticket.product || '')
      );
    }

    if (this.activeFilters.subProduct && this.activeFilters.subProduct.length > 0) {
      filtered = filtered.filter(ticket =>
        this.activeFilters.subProduct!.includes(ticket.subProduct || '')
      );
    }

    if (this.activeFilters.assignedTo && this.activeFilters.assignedTo.length > 0) {
      filtered = filtered.filter(ticket =>
        this.activeFilters.assignedTo!.includes(ticket.assignedTo || '')
      );
    }

    if (this.activeFilters.dateFrom) {
      const fromDate = new Date(this.activeFilters.dateFrom);
      fromDate.setHours(0, 0, 0, 0);
      filtered = filtered.filter(ticket => {
        const ticketDate = new Date(ticket.createdOn);
        return ticketDate >= fromDate;
      });
    }

    if (this.activeFilters.dateTo) {
      const toDate = new Date(this.activeFilters.dateTo);
      toDate.setHours(23, 59, 59, 999);
      filtered = filtered.filter(ticket => {
        const ticketDate = new Date(ticket.createdOn);
        return ticketDate <= toDate;
      });
    }

    this.filteredTickets = filtered;
  }

  public get paginatedTickets(): Ticket[] {
    let sorted = [...this.filteredTickets];
    const col = this.sortColumn;
    const dir = this.sortDirection;

    sorted.sort((a: any, b: any) => {
      let valA = a[col];
      let valB = b[col];

      if (col === 'createdOn') {
        valA = new Date(a.createdOn).getTime();
        valB = new Date(b.createdOn).getTime();
      } else if (col === 'id') {
        valA = a.id;
        valB = b.id;
      } else {
        valA = (valA || '').toString().toLowerCase();
        valB = (valB || '').toString().toLowerCase();
      }

      if (valA < valB) return dir === 'asc' ? -1 : 1;
      if (valA > valB) return dir === 'asc' ? 1 : -1;
      return 0;
    });

    const start = (this.currentPage - 1) * this.pageSize;
    return sorted.slice(start, start + this.pageSize);
  }

  public get totalRecords(): number {
    return this.filteredTickets.length;
  }

  public get totalPages(): number {
    return Math.ceil(this.totalRecords / this.pageSize) || 1;
  }

  public onPageChange(page: number): void {
    this.currentPage = page;
  }

  public onPageSizeChange(): void {
    this.currentPage = 1;
  }

  public getStartRecord(): number {
    if (this.totalRecords === 0) return 0;
    return (this.currentPage - 1) * this.pageSize + 1;
  }

  public getEndRecord(): number {
    const end = this.currentPage * this.pageSize;
    return end > this.totalRecords ? this.totalRecords : end;
  }

  public getPageNumbers(): number[] {
    const pages = [];
    const maxPagesToShow = 5;
    let startPage = Math.max(1, this.currentPage - 2);
    let endPage = Math.min(this.totalPages, startPage + maxPagesToShow - 1);

    if (endPage - startPage + 1 < maxPagesToShow) {
      startPage = Math.max(1, endPage - maxPagesToShow + 1);
    }

    for (let i = startPage; i <= endPage; i++) {
      pages.push(i);
    }
    return pages;
  }

  public onSort(column: string): void {
    if (this.sortColumn === column) {
      this.sortDirection = this.sortDirection === 'asc' ? 'desc' : 'asc';
    } else {
      this.sortColumn = column;
      this.sortDirection = 'desc';
    }
    this.currentPage = 1;
  }

  // --- Editing Methods ---
  public startEditing(field: 'status' | 'priority', ticketId: number, currentValue: string, event: MouseEvent): void {
    event.stopPropagation();

    const ticket = this.allTickets.find(t => t.id === ticketId);
    if (ticket && !this.canEditTicketStatus(ticket)) {
      this.showNotification('Cannot edit - ticket is already closed', 'error');
      return;
    }

    this.closeAllDropdowns();

    const dropdownKey = `${field}_${ticketId}`;
    this.editingField = dropdownKey;
    this.editingValue = currentValue;
    this.dropdownStates[dropdownKey] = true;

    // Calculate position for dropdown
    const target = event.currentTarget as HTMLElement;
    const rect = target.getBoundingClientRect();

    const dropdownWidth = 200;
    const dropdownHeight = 250;

    let top = rect.bottom + 5;
    let left = rect.left + (rect.width / 2) - (dropdownWidth / 2);

    const isNearTop = rect.top < 100;

    if (isNearTop && (top + dropdownHeight > window.innerHeight)) {
      top = rect.top - dropdownHeight - 5;
      if (top < 0) {
        top = rect.bottom + 5;
      }
    }

    if (left < 10) {
      left = 10;
    } else if (left + dropdownWidth > window.innerWidth - 10) {
      left = window.innerWidth - dropdownWidth - 10;
    }

    if (top < 10) {
      top = 10;
    } else if (top + dropdownHeight > window.innerHeight - 10) {
      top = window.innerHeight - dropdownHeight - 10;
    }

    this.dropdownPositions[dropdownKey] = { top, left };

    setTimeout(() => {
      this.clickOutsideListener = (e: Event) => {
        const clickedElement = e.target as HTMLElement;
        if (!clickedElement.closest('.dropdown-container') && !clickedElement.closest('.editable-badge')) {
          this.closeAllDropdowns();
        }
      };
      document.addEventListener('click', this.clickOutsideListener);
    }, 0);
  }

  public closeAllDropdowns(): void {
    this.dropdownStates = {};
    this.dropdownPositions = {};
    this.editingField = null;
    this.editingValue = '';

    if (this.clickOutsideListener) {
      document.removeEventListener('click', this.clickOutsideListener);
      this.clickOutsideListener = undefined;
    }
  }

  public selectOption(field: 'status' | 'priority', ticketId: number, value: string): void {
    this.editingValue = value;
    this.saveEdit(field, ticketId);
  }

  public selectStatusOption(ticketId: number, newDisplayStatus: string, ticket: Ticket): void {
    // If user wants to close the ticket (from any state)
    if (newDisplayStatus === 'Closed') {
      this.ticketToClose = { id: ticketId, currentStatus: this.getDisplayStatus(ticket) };
      this.showCloseConfirmModal = true;
      this.closeAllDropdowns();
      return;
    }

    // If user wants to re-open a ticket that was marked as 'Work Done'
    if (newDisplayStatus === 'Open' && ticket.status === 'Work Done') {
      this.saveEdit('status', ticketId, 'Open'); // New status is 'Open'
      this.closeAllDropdowns();
      return;
    }

    // Fallback for any other scenario
    this.closeAllDropdowns();
  }

  // --- Confirmation Dialog Methods ---
  public cancelCloseTicket(): void {
    this.showCloseConfirmModal = false;
    this.ticketToClose = null;
  }

  public async confirmCloseTicket(): Promise<void> {
    if (!this.ticketToClose || !this.ticketService || !this.customerId) {
      this.cancelCloseTicket();
      return;
    }

    try {
      const ticketId = this.ticketToClose.id;
      const originalTicket = this.allTickets.find(t => t.id === ticketId);

      if (!originalTicket) {
        this.handleError(`Could not find ticket #${ticketId} to close.`, null);
        this.cancelCloseTicket();
        return;
      }

      const updateData: CustomerUpdateTicketRequest = {
        status: 'Closed',
        priority: originalTicket.priority,
      };

      await this.ticketService.updateTicketByCustomer(ticketId, this.customerId, updateData);

      // Update local ticket data
      const ticketInAll = this.allTickets.find(t => t.id === ticketId);
      if (ticketInAll) {
        ticketInAll.status = 'Closed';
      }

      this.applyClientSideFilters();
      this.showNotification(`Ticket #${ticketId} has been closed successfully.`, 'success');

    } catch (error) {
      this.handleError(`Failed to close ticket #${this.ticketToClose.id}.`, error);
    } finally {
      this.cancelCloseTicket();
    }
  }

  public async saveEdit(field: 'status' | 'priority', ticketId: number, newValue?: string): Promise<void> {
    const valueToSave = newValue !== undefined ? newValue : this.editingValue;

    if (!this.ticketService || !valueToSave || !this.customerId) {
      this.closeAllDropdowns();
      return;
    }

    // --- FIX START: Define originalTicket and handle if not found ---
    const originalTicket = this.allTickets.find(t => t.id === ticketId);
    if (!originalTicket) {
      this.handleError(`Could not find ticket #${ticketId} to edit.`, null);
      this.closeAllDropdowns();
      return;
    }
    // --- FIX END ---

    const isStatusChange = field === 'status';
    const isPriorityChange = field === 'priority';

    // Prevent saving if there's no actual change
    if ((isPriorityChange && originalTicket.priority === valueToSave) ||
      (isStatusChange && originalTicket.status === valueToSave)) {
      this.closeAllDropdowns();
      return;
    }

    try {
      const updateData: CustomerUpdateTicketRequest = {
        priority: isPriorityChange ? valueToSave : originalTicket.priority,
        status: isStatusChange ? valueToSave : originalTicket.status
      };

      await this.ticketService.updateTicketByCustomer(ticketId, this.customerId, updateData);

      // Update local data
      const ticketInAll = this.allTickets.find(t => t.id === ticketId);
      if (ticketInAll) {
        if (isPriorityChange) ticketInAll.priority = valueToSave;
        if (isStatusChange) ticketInAll.status = valueToSave;
      }

      this.applyClientSideFilters();
      this.showNotification(`Ticket #${ticketId} ${field} updated to "${valueToSave}".`, 'success');

    } catch (error) {
      this.handleError(`Failed to update ticket #${ticketId}.`, error);
    } finally {
      this.closeAllDropdowns();
    }
  }

  public isDropdownOpen(field: string, ticketId: number): boolean {
    return this.dropdownStates[`${field}_${ticketId}`] || false;
  }

  public getDropdownPosition(field: string, ticketId: number): any {
    const key = `${field}_${ticketId}`;
    return this.dropdownPositions[key] || { top: 0, left: 0 };
  }
  public async viewTicketHistory(ticket: Ticket): Promise<void> {
    if (!this.ticketService) {
      this.handleError('Cannot load history: Ticket service is not available.', null);
      return;
    }

    try {
      this.loading = true;
      const history = await this.ticketService.getTicketHistory(ticket.id);
      console.log('HISTORY RAW:', history);

      // Filter history to show only customer-relevant events
      const filteredHistory = this.filterCustomerRelevantHistory(history);

      this.selectedTicketHistory = {
        ticket: ticket,
        history: filteredHistory
      };
      this.showHistoryModal = true;

      this.showNotification(`Loaded ${filteredHistory.length} history entries`, 'info');
    } catch (error) {
      this.handleError(`Failed to load history for ticket #${ticket.id}`, error);
    } finally {
      this.loading = false;
    }
  }

  private filterCustomerRelevantHistory(history: TicketHistory[]): TicketHistory[] {
    return history
      .filter(entry => {
        const description = entry.eventDescription.toLowerCase();

        if (description.includes('ticket was created')) return true;
        if (description.includes('ticket assigned') && description.includes("from 'unassigned'")) return true;
        if (description.includes("to 'work done'")) return true;
        if (description.includes("from 'work done'") && description.includes("to 'closed'")) return true;
        if (description.includes("to 'closed'") && !description.includes("from 'work done'")) return true;
        if (description.includes("from 'closed'") && description.includes("to 'open'")) return true;
        if (description.includes('priority changed') && entry.changedByUserRole?.toLowerCase() === 'customer') return true;

        return false;
      })
      .map(entry => {
        const normalized: any = { ...entry };

        // Normalize timestamp
        if ((entry as any).changeDate) {
          normalized.timestamp = (entry as any).changeDate;
        }

        // Generic assignment message (no name)
        const desc = normalized.eventDescription.toLowerCase();
        if (desc.includes('ticket assigned') && desc.includes("from 'unassigned'")) {
          normalized.eventDescription = 'Ticket was assigned to support team.';
        }

        return normalized as TicketHistory;
      });
  }
  // --- History Methods ---
  //  public async viewTicketHistory(ticket: Ticket): Promise<void> {
  //     if (!this.ticketService) {
  //       this.handleError('Cannot load history: Ticket service is not available.', null);
  //       return;
  //     }

  //     try {
  //       this.loading = true;
  //       const history = await this.ticketService.getTicketHistory(ticket.id);

  //       // Filter history to show only customer-relevant events
  //       const filteredHistory = this.filterCustomerRelevantHistory(history);

  //       this.selectedTicketHistory = {
  //         ticket: ticket,
  //         history: filteredHistory
  //       };
  //       this.showHistoryModal = true;

  //       this.showNotification(`Loaded ${filteredHistory.length} history entries`, 'info');
  //     } catch (error) {
  //       this.handleError(`Failed to load history for ticket #${ticket.id}`, error);
  //     } finally {
  //       this.loading = false;
  //     }
  //   }

  //   private filterCustomerRelevantHistory(history: TicketHistory[]): TicketHistory[] {
  //     const filtered = history.filter(entry => {
  //       const description = entry.eventDescription.toLowerCase();

  //       // Show: Ticket creation
  //       if (description.includes('ticket was created')) {
  //         return true;
  //       }

  //       // Show: When ticket is assigned (first assignment only, hide reassignments)
  //       // We'll show "Ticket assigned" only if it's from 'Unassigned' to someone
  //       if (description.includes('ticket assigned') && 
  //           description.includes("from 'unassigned'")) {
  //         return true;
  //       }

  //       // Show: Status changed to 'Work Done'
  //       if (description.includes('status changed') && 
  //           description.includes("to 'work done'")) {
  //         return true;
  //       }

  //       // Show: Status changed from 'Work Done' to 'Closed'
  //       if (description.includes('status changed') && 
  //           description.includes("from 'work done'") && 
  //           description.includes("to 'closed'")) {
  //         return true;
  //       }

  //       // Show: Status changed to 'Closed' (from any status except 'Work Done' - already covered above)
  //       if (description.includes('status changed') && 
  //           description.includes("to 'closed'") &&
  //           !description.includes("from 'work done'")) {
  //         return true;
  //       }

  //       // Show: Status changed from 'Closed' to 'Open' (ticket reopened)
  //       if (description.includes('status changed') && 
  //           description.includes("from 'closed'") && 
  //           description.includes("to 'open'")) {
  //         return true;
  //       }

  //       // Show: Priority changes made by customer
  //       if (description.includes('priority changed') && 
  //           entry.changedByUserRole?.toLowerCase() === 'customer') {
  //         return true;
  //       }

  //       // Hide all other events (internal status changes, reassignments, etc.)
  //       return false;
  //     });

  //     // Simplify assignment messages for customers
  //     return filtered.map(entry => {
  //       const description = entry.eventDescription.toLowerCase();
  //       if (description.includes('ticket assigned') && description.includes("from 'unassigned'")) {
  //         return {
  //           ...entry,
  //           eventDescription: 'Ticket was assigned to support team.'
  //         };
  //       }
  //       return entry;
  //     });
  //   }

  public getHistoryEventIcon(eventDescription: string): string {
    const description = eventDescription.toLowerCase();
    if (description.includes('created') || description.includes('submitted')) return '📝';
    if (description.includes('status') && description.includes('closed')) return '🔒';
    if (description.includes('status') && description.includes('open')) return '🔓';
    if (description.includes('priority')) return '⚡';
    if (description.includes('assigned')) return '👤';
    if (description.includes('comment') || description.includes('reply')) return '💬';
    if (description.includes('attachment')) return '📎';
    return '📋';
  }

  public getRoleDisplayName(role: string): string {
    switch (role?.toLowerCase()) {
      case 'customer': return 'Customer';
      case 'agent': return 'Support Agent';
      case 'admin': return 'Administrator';
      case 'pm': return 'Project Manager';
      default: return role || 'System';
    }
  }

  public closeHistoryModal(): void {
    this.showHistoryModal = false;
    this.selectedTicketHistory = null;
  }

  public formatHistoryTimestamp(timestamp: string): string {
    if (!timestamp) return 'N/A';
    return new Intl.DateTimeFormat('en-US', {
      year: 'numeric',
      month: 'short',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
      hour12: true
    }).format(new Date(timestamp));
  }

  // --- File Attachment Methods ---
  public viewAttachment(ticket: Ticket): void {
    if (ticket.attachments && ticket.attachments.length > 0) {
      this.selectedFileAttachment = {
        ticket: ticket,
        attachments: ticket.attachments
      };
      this.showFileModal = true;
    }
  }

  public closeFileModal(): void {
    this.showFileModal = false;
    this.selectedFileAttachment = null;
  }

  public async downloadAttachment(ticketId: number, attachment: TicketAttachment): Promise<void> {
    if (!this.ticketService) return;

    try {
      const blob = await this.ticketService.getTicketAttachment(ticketId, attachment.id);
      this.downloadFile(blob, attachment.fileName);
      this.showNotification(`Downloaded ${attachment.fileName}`, 'success');
    } catch (error) {
      this.handleError(`Failed to download ${attachment.fileName}`, error);
    }
  }

  public async previewAttachment(attachment: TicketAttachment): Promise<void> {
    if (!this.ticketService || !this.selectedFileAttachment?.ticket?.id) {
      this.handleError('Cannot preview attachment: required data is missing.', null);
      return;
    }

    try {
      const ticketId = this.selectedFileAttachment.ticket.id;
      const blob = await this.ticketService.getTicketAttachment(ticketId, attachment.id);

      const fileURL = URL.createObjectURL(blob);
      window.open(fileURL, '_blank');

      setTimeout(() => URL.revokeObjectURL(fileURL), 100);
      this.showNotification(`Opening preview for ${attachment.fileName}`, 'info');
    } catch (error) {
      this.handleError(`Failed to load preview for ${attachment.fileName}`, error);
    }
  }

  public formatFileSize(bytes: number): string {
    if (bytes === 0) return '0 Bytes';
    const k = 1024;
    const sizes = ['Bytes', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
  }

  // --- Ticket Detail Popup ---
  public selectedTicket: Ticket | null = null;
  public showTicketPopup = false;

  public viewTicketDetails(ticket: Ticket): void {
    if (ticket && ticket.id) {
      this.selectedTicket = ticket;
      this.showTicketPopup = true;
    }
  }

  public closeTicketPopup(): void {
    this.showTicketPopup = false;
    this.selectedTicket = null;
  }

  public openFullTicketDetail(): void {
    if (this.selectedTicket) {
      this.router.navigate(['/customer/ticket', this.selectedTicket.id]);
    }
  }

  public createNewTicket(): void {
    this.router.navigate(['/customer/ticket-create']);
  }

  public async refreshData(): Promise<void> {
    const customerId = this.route.snapshot.params['customerId'];
    if (customerId) {
      await this.loadInitialData(parseInt(customerId, 10));
    }
  }

  public async exportData(format: 'csv' | 'json'): Promise<void> {
    const customerId = this.route.snapshot.params['customerId'];
    if (!customerId) {
      this.handleError('Cannot export: Customer ID is missing.', null);
      return;
    }
    if (!this.ticketService) {
      this.handleError('Cannot export: Ticket service is not available.', null);
      return;
    }

    try {
      this.loading = true;
      const blob = await this.ticketService.exportTickets(format, parseInt(customerId, 10));
      if (blob.size > 0) {
        this.downloadFile(blob, `tickets_export.${format}`);
        this.showNotification('Export completed successfully.', 'success');
      } else {
        this.showNotification('There was no data to export.', 'info');
      }
    } catch (error) {
      this.handleError('Export failed', error);
    } finally {
      this.loading = false;
    }
  }

  public onGridEvent(eventName: string, data?: any): void {
    console.log(`Grid event: ${eventName}`, data);
    if (eventName === 'rowDoubleClick') {
      this.viewTicketDetails(data);
    }
  }

  // --- Helper Methods ---
  private downloadFile(blob: Blob, filename: string): void {
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = filename;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    URL.revokeObjectURL(url);
  }

  public getTicketCountByStatus(statuses: string[]): number {
    if (!this.filteredTickets || this.filteredTickets.length === 0) return 0;
    if (statuses.includes('All')) return this.filteredTickets.length;
    return this.filteredTickets.filter(t => statuses.includes(t.status)).length;
  }

  public getCssClass(type: string, value: string): string {
    return `${type}-${value?.toLowerCase().replace(/\s+/g, '-')}`;
  }

  public formatDate(date: any): string {
    if (!date) return 'N/A';
    return new Intl.DateTimeFormat('en-GB', {
      day: '2-digit', month: 'short', year: 'numeric'
    }).format(new Date(date)).replace(/ /g, '-');
  }

  public getClosingRemark(ticket: any): string | null {
    if (!ticket) return null;
    
    const direct = ticket.lastUpdateNote ||
                   ticket.LastUpdateNote ||
                   ticket.closingRemark ||
                   ticket.ClosingRemark ||
                   ticket.lastUpdatedNote ||
                   ticket.LastUpdatedNote ||
                   ticket.closeNote ||
                   ticket.resolutionNote ||
                   ticket.closingNote ||
                   ticket.resolution ||
                   ticket.remark ||
                   ticket.Remark;

    if (direct && typeof direct === 'string' && direct.trim().length > 0) {
      return direct.trim();
    }

    const historyList = (this.selectedTicketHistory?.ticket?.id === ticket.id) ? this.selectedTicketHistory?.history : null;

    if (ticket.closedOn && historyList && historyList.length > 0) {
      const closedTime = new Date(ticket.closedOn).getTime();
      if (!isNaN(closedTime)) {
        let closestEvent: any = null;
        let minDiff = 6000; // 6 seconds window

        for (const h of historyList) {
          const rawDate = h.timestamp || (h as any).changeDate;
          if (rawDate) {
            const hTime = new Date(rawDate).getTime();
            if (!isNaN(hTime)) {
              const diff = Math.abs(hTime - closedTime);
              if (diff <= minDiff) {
                minDiff = diff;
                closestEvent = h;
              }
            }
          }
        }

        if (closestEvent) {
          const desc = closestEvent.eventDescription || (closestEvent as any).remark || '';
          if (desc.includes('Note:')) {
            return desc.split('Note:')[1].trim();
          }
          if (desc.includes('Remark:')) {
            return desc.split('Remark:')[1].trim();
          }
          if (desc.length > 0 && !desc.toLowerCase().startsWith('status changed to')) {
            return desc;
          }
        }
      }
    }

    return ticket.remark || null;
  }

  public truncateText(text: string | null | undefined, limit: number = 70): string {
    if (!text) return '';
    const trimmed = text.trim();
    return trimmed.length > limit ? trimmed.substring(0, limit) + '...' : trimmed;
  }

  public isFeatureEnabled(feature: string): boolean {
    switch (feature) {
      case 'export': return this.allowExport;
      case 'columns': return this.allowColumnManagement;
      case 'newTicket': return this.allowNewTicketCreation;
      default: return true;
    }
  }

  private showNotification(message: string, type: 'success' | 'error' | 'info' = 'info'): void {
    if (this.notificationService) {
      this.notificationService.show(message, type);
    }
  }

  private handleError(message: string, error: any): void {
    console.error(message, error);
    this.error = `${message} Please check the API connection and try again.`;
    this.showNotification(this.error, 'error');
  }
  // --- Chat Properties ---
  public showChatModal: boolean = false;
  public currentChatTicket: Ticket | null = null;
  public chatMessages: TicketCommunicationDto[] = [];
  public newMessageText: string = '';
  public isSendingMessage: boolean = false;

  // --- Chat Methods ---
  public openChat(ticket: Ticket): void {
    this.currentChatTicket = ticket;
    this.showChatModal = true;
    this.loadChatHistory(ticket.id);
  }

  public closeChatModal(): void {
    this.showChatModal = false;
    this.currentChatTicket = null;
    this.chatMessages = [];
    this.newMessageText = '';
  }

  public async loadChatHistory(ticketId: number): Promise<void> {
    try {
      // Cast to any to access getCommunications
      const response: any = await firstValueFrom((this.ticketService as any).getCommunications(ticketId));

      // Merge customerChannel (chat) and updateNotesCustomer (PM notes)
      const chatMessages = response.customerChannel || [];
      const managerNotes = response.updateNotesCustomer || [];

      this.chatMessages = [...chatMessages, ...managerNotes];

      // Fix timezones (Strip 'Z' to treat as local server time)
      this.chatMessages.forEach(msg => {
        if (msg.postedOn && typeof msg.postedOn === 'string') {
          msg.postedOn = msg.postedOn.replace(/Z$/, '');
        }
      });

      // Sort by date asc
      this.chatMessages.sort((a, b) => new Date(a.postedOn).getTime() - new Date(b.postedOn).getTime());

    } catch (error) {
      this.handleError('Failed to load chat history.', error);
    }
  }

  public async sendMessage(): Promise<void> {
    if (!this.newMessageText.trim() || !this.currentChatTicket || !this.customerId) return;

    this.isSendingMessage = true;
    const text = this.newMessageText;
    const ticketId = this.currentChatTicket.id;

    try {
      const formData = new FormData();
      formData.append('CommentText', text);
      formData.append('Channel', 'Customer'); // Always Customer channel here
      formData.append('PostedByUserId', this.customerId.toString());
      formData.append('PostedByName', 'Customer');
      formData.append('PostedByUserRole', 'Customer');

      await firstValueFrom((this.ticketService as any).addCommunication(ticketId, formData));

      this.newMessageText = ''; // Clear input
      await this.loadChatHistory(ticketId); // Refresh to see new message (and any others)

    } catch (error) {
      this.handleError('Failed to send message.', error);
    } finally {
      this.isSendingMessage = false;
    }
  }
}