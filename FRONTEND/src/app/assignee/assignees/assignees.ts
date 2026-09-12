import { Component, OnInit, OnDestroy, Inject, ViewEncapsulation } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { Observable, Subscription, from, of } from 'rxjs';
import { switchMap, map, catchError, finalize, tap } from 'rxjs/operators';
import { TICKET_SERVICE_TOKEN } from '../../Core/injection-tokens';
import { Ticket } from '../../Core/models/ticket.model';
import { DashboardStats, TicketService, TicketResponse } from '../../Core/services/ticket.service';
import { AuthService } from '../../Core/services/auth';

@Component({
  selector: 'app-assignees',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './assignees.html',
  styleUrls: ['./assignees.scss'],
  encapsulation: ViewEncapsulation.None
})
export class AssigneesComponent implements OnInit, OnDestroy {
  // Data properties
  public tickets$: Observable<Ticket[]>;
  public allTickets: Ticket[] = [];
  public filteredTickets: Ticket[] = [];
  public paginatedTickets: Ticket[] = [];
  
  // Pagination
  public currentPage: number = 1;
  public pageSize: number = 15;
  public totalPages: number = 0;
  public totalRecords: number = 0;
  public pageSizeOptions: number[] = [10, 15, 25, 50];
  
  // Loading and stats
  public stats: DashboardStats | null = null;
  public activeStatFilter: string | null = null;
  public isLoading = false;
  public isLoadingDelayed = false; // Separate loading state for delayed tickets

  // Filters
  public statusOptions: string[] = ['Open', 'In Progress', 'Work Done', 'Pending', 'Resolved', 'Closed','On Hold'];
  public priorityOptions: string[] = ['Low', 'Medium', 'High', 'Urgent'];
  public selectedStatus: string | null = null;
  public selectedPriority: string | null = null;
  public searchQuery: string = '';

  // Tab filter: 'assigned' (Assigned to me) or 'created' (Created by me)
  public activeTab: 'assigned' | 'created' = 'assigned';

  // Sorting
  public sortField: string = '';
  public sortDirection: 'asc' | 'desc' = 'asc';

  private subscription = new Subscription();
  private developerId: number = 0;
  private developerCode: string = '';


  constructor(
    @Inject(TICKET_SERVICE_TOKEN) private ticketService: TicketService,
    private router: Router,
    private route: ActivatedRoute,
    private authService: AuthService
  ) {
    this.tickets$ = this.route.paramMap.pipe(
      tap(() => {
        this.isLoading = true;
        const currentUser = this.authService.getCurrentUser();
        this.developerId = currentUser ? Number(currentUser.userId) : 0;

        this.loadStats();
      }),
      switchMap(() => {
        return from(this.ticketService.getAssigneeById(this.developerId)).pipe(
          tap(assignee => {
            this.developerCode = assignee?.assigneeNumber || `E${this.developerId}`;
          }),
          switchMap(assignee => {
            if (this.activeTab === 'created') {
              return from(this.ticketService.getTicketsForPM({
                createdByUserId: this.developerId,
                pageNumber: 1,
                pageSize: 500
              }));
            }
            const code = assignee?.assigneeNumber || `E${this.developerId}`;
            return from(this.ticketService.getTicketsForDeveloper(code));
          }),
          catchError(err => {
            console.error('Failed to fetch assignee details, falling back to legacy E prefix:', err);
            this.developerCode = `E${this.developerId}`;
            if (this.activeTab === 'created') {
              return from(this.ticketService.getTicketsForPM({
                createdByUserId: this.developerId,
                pageNumber: 1,
                pageSize: 500
              }));
            }
            return from(this.ticketService.getTicketsForDeveloper(this.developerCode));
          }),
          finalize(() => { this.isLoading = false; })
        );
      }),

      tap((response: TicketResponse) => {
        this.allTickets = response.tickets || [];
        this.totalRecords = response.totalCount || 0;
        this.updateStatusOptionsWithTicketStatuses();
        this.applyFiltersAndPagination();
      }),
      map(() => this.paginatedTickets),
      catchError((error) => {
        console.error('Failed to load tickets for developer:', error);
        this.allTickets = [];
        this.totalRecords = 0;
        this.applyFiltersAndPagination();
        return of([]);
      })
    );
  }

  public async setActiveTab(tab: 'assigned' | 'created'): Promise<void> {
    if (this.activeTab === tab) return;
    this.activeTab = tab;
    this.clearAllFilters(false);
    await this.loadTicketsForDeveloper();
  }

  public async loadTicketsForDeveloper(): Promise<void> {
    if (!this.developerId) return;
    this.isLoading = true;
    try {
      let response: TicketResponse;
      if (this.activeTab === 'created') {
        response = await this.ticketService.getTicketsForPM({
          createdByUserId: this.developerId,
          pageNumber: 1,
          pageSize: 500
        });
      } else {
        const code = this.developerCode || `E${this.developerId}`;
        response = await this.ticketService.getTicketsForDeveloper(code);
      }
      this.allTickets = response.tickets || [];
      this.totalRecords = response.totalCount || 0;
      this.updateStatusOptionsWithTicketStatuses();
      this.applyFiltersAndPagination();
    } catch (error) {
      console.error('Failed to load tickets for developer:', error);
      this.allTickets = [];
      this.totalRecords = 0;
      this.applyFiltersAndPagination();
    } finally {
      this.isLoading = false;
    }
  }

  async ngOnInit(): Promise<void> {
    this.subscription.add(this.tickets$.subscribe());
    try {
      const devStatuses = await this.ticketService.getCustomDeveloperStatuses();
      if (devStatuses && devStatuses.length > 0) {
        this.statusOptions = devStatuses.filter((s: any) => s.isActive).map((s: any) => s.statusName);
      }
    } catch (error) {
      console.error('Failed to load custom developer statuses:', error);
    }
  }

  ngOnDestroy(): void {
    this.subscription.unsubscribe();
  }

  async loadStats(): Promise<void> {
    if (!this.developerId) return;
    try {
      this.stats = await this.ticketService.getDeveloperDashboardStats(this.developerId);
    } catch (error) {
      console.error('Failed to load developer dashboard stats:', error);
    }
  }

  // New method to load delayed tickets
  private async loadDelayedTickets(): Promise<void> {
    if (!this.developerId) return;
    
    this.isLoadingDelayed = true;
    try {
      const response = await this.ticketService.getDelayedTicketsForDeveloper({
        developerId: this.developerId,
        pageNumber: 1,
        pageSize: 500 // Get all delayed tickets
      });
      
      this.allTickets = response.tickets || [];
      this.totalRecords = response.totalCount || 0;
      this.updateStatusOptionsWithTicketStatuses();
      this.applyFiltersAndPagination();
    } catch (error) {
      console.error('Failed to load delayed tickets for developer:', error);
      this.allTickets = [];
      this.totalRecords = 0;
      this.applyFiltersAndPagination();
    } finally {
      this.isLoadingDelayed = false;
    }
  }

  private updateStatusOptionsWithTicketStatuses(): void {
    const existingStatuses = new Set(this.statusOptions);
    this.allTickets.forEach(ticket => {
      if (ticket.status && !existingStatuses.has(ticket.status)) {
        this.statusOptions.push(ticket.status);
        existingStatuses.add(ticket.status);
      }
    });
  }

  private applyFiltersAndPagination(): void {
    const today = new Date();
    today.setHours(0, 0, 0, 0);

    this.filteredTickets = this.allTickets.filter(ticket => {
      let statusMatch = true;
      let priorityMatch = true;

      if (this.selectedStatus) {
        statusMatch = ticket.status === this.selectedStatus;
      }
      if (this.selectedPriority) {
        priorityMatch = ticket.priority === this.selectedPriority;
      }

      if (this.activeStatFilter) {
        switch (this.activeStatFilter) {
          case 'pending':
            statusMatch = !['Closed', 'Resolved'].includes(ticket.status);
            priorityMatch = true;
            break;
          case 'urgent':
            priorityMatch = ticket.priority === 'Urgent';
            statusMatch = true;
            break;
          case 'new':
            const ticketDate = new Date(ticket.createdOn);
            ticketDate.setHours(0, 0, 0, 0);
            statusMatch = ticketDate.getTime() === today.getTime();
            priorityMatch = true;
            break;
          case 'delayed':
            // For delayed tickets, we don't need additional filtering
            // since the tickets are already filtered by the API
            statusMatch = true;
            priorityMatch = true;
            break;
        }
      }
      let searchMatch = true;
      if (this.searchQuery && this.searchQuery.trim()) {
        const query = this.searchQuery.toLowerCase().trim();
        const subject = (ticket.subject || '').toLowerCase();
        const num = (ticket.ticketNumber || '').toLowerCase();
        const customer = (ticket.customerName || '').toLowerCase();
        searchMatch = subject.includes(query) || num.includes(query) || customer.includes(query);
      }

      return statusMatch && priorityMatch && searchMatch;
    });

    if (this.sortField) {
      this.filteredTickets.sort((a, b) => {
          const aVal = this.getFieldValue(a, this.sortField);
          const bVal = this.getFieldValue(b, this.sortField);
          let comparison = 0;
          if (aVal < bVal) comparison = -1;
          else if (aVal > bVal) comparison = 1;
          return this.sortDirection === 'desc' ? comparison * -1 : comparison;
      });
    }

    this.totalPages = Math.ceil(this.filteredTickets.length / this.pageSize);
    
    if (this.currentPage > this.totalPages && this.totalPages > 0) {
      this.currentPage = this.totalPages;
    } else if (this.totalPages === 0) {
      this.currentPage = 1;
    }
    
    const startIndex = (this.currentPage - 1) * this.pageSize;
    this.paginatedTickets = this.filteredTickets.slice(startIndex, startIndex + this.pageSize);
  }

  private getFieldValue(obj: any, field: string): any {
    return field.split('.').reduce((o, key) => o && o[key], obj) || '';
  }

  public onTicketClick(ticket: Ticket): void {
    if (ticket.id) {
      this.router.navigate([this.authService.getTicketDetailRoute(ticket.id)]);
    }
  }

  public onFilterChange(): void {
    this.activeStatFilter = null;
    this.currentPage = 1;
    this.applyFiltersAndPagination();
  }

  // Updated method to handle delayed tickets stat
 public async onStatCardClick(statType: 'pending' | 'urgent' | 'new' | 'delayed'): Promise<void> {
  this.clearAllFilters(false);
  this.activeStatFilter = statType;
  this.currentPage = 1;

  if (statType === 'delayed') {
    await this.loadDelayedTickets();
  } else if (statType === 'new') {
    await this.loadTodayTicketsForDeveloper(); // 👈 new helper
  } else {
    this.applyFiltersAndPagination();
  }
}

private async loadTodayTicketsForDeveloper(): Promise<void> {
  if (!this.developerId) return;

  this.isLoading = true;
  try {
    const today = new Date();
    const dateFrom = new Date(today);
    const dateTo = new Date(today);
    dateFrom.setHours(0, 0, 0, 0);
    dateTo.setHours(23, 59, 59, 999);

    const response = await this.ticketService.getTicketsForDeveloper(this.developerCode || `E${this.developerId}`, {
      dateFrom: dateFrom.toISOString(),
      dateTo: dateTo.toISOString(),
      pageNumber: 1,
      pageSize: 500
    });

    this.allTickets = response.tickets || [];
    this.totalRecords = response.totalCount || 0;
    this.updateStatusOptionsWithTicketStatuses();
    this.applyFiltersAndPagination();
  } catch (error) {
    console.error('Failed to load today’s tickets for developer:', error);
    this.allTickets = [];
    this.totalRecords = 0;
    this.applyFiltersAndPagination();
  } finally {
    this.isLoading = false;
  }
}



  public clearAllFilters(reload: boolean = true): void {
    this.selectedStatus = null;
    this.selectedPriority = null;
    this.activeStatFilter = null;
    this.searchQuery = '';
    this.currentPage = 1;
    if (reload) {
      // Reload all tickets when clearing filters
      this.refresh();
    }
  }
  
  public onSort(field: string): void {
    if (this.sortField === field) {
      this.sortDirection = this.sortDirection === 'asc' ? 'desc' : 'asc';
    } else {
      this.sortField = field;
      this.sortDirection = 'asc';
    }
    this.applyFiltersAndPagination();
  }

  public getSortIcon(field: string): string {
    if (this.sortField !== field) return '↕️';
    return this.sortDirection === 'asc' ? '↑' : '↓';
  }

  public onPageChange(page: number): void {
    if (page >= 1 && page <= this.totalPages) {
      this.currentPage = page;
      this.applyFiltersAndPagination();
    }
  }

  public onPageSizeChange(): void {
    this.currentPage = 1;
    this.applyFiltersAndPagination();
  }
  
  public refresh(): void {
    this.router.navigateByUrl('/', { skipLocationChange: true }).then(() => {
        this.router.navigate(['/developer/dashboard']);
    });
  }

  public getPageNumbers(): number[] {
    const pages = [];
    const maxVisible = 5;
    let start = Math.max(1, this.currentPage - Math.floor(maxVisible / 2));
    let end = Math.min(this.totalPages, start + maxVisible - 1);
    
    if (end - start + 1 < maxVisible) {
      start = Math.max(1, end - maxVisible + 1);
    }
    
    for (let i = start; i <= end; i++) {
      pages.push(i);
    }
    return pages;
  }

  public getEndRecord(): number {
    return Math.min(this.currentPage * this.pageSize, this.filteredTickets.length);
  }

  public getStartRecord(): number {
    return this.filteredTickets.length === 0 ? 0 : (this.currentPage - 1) * this.pageSize + 1;
  }
  
  public getAssigneeInitials(ticket: any): string {
    const name = ticket.assignedToName || (ticket.assignedTo != null ? String(ticket.assignedTo) : '');
    return name ? name.substring(0, 2).toUpperCase() : '?';
  }

  public getAssigneeShortName(ticket: any): string {
    const name = ticket.assignedToName || (ticket.assignedTo != null ? String(ticket.assignedTo) : '');
    if (!name) return '—';
    return name.length > 10 ? name.substring(0, 10) + '…' : name;
  }

  public formatDate(dateString: string): string {
    if (!dateString) return '';
    return new Date(dateString).toLocaleDateString();
  }

  public getStatusClass(status: string): string {
    const statusMap: { [key: string]: string } = {
      'Open': 'status-new', 'New': 'status-new', 'In Progress': 'status-progress',
      'Pending': 'status-pending', 'Resolved': 'status-resolved', 'Closed': 'status-closed', 'Work Done': 'status-resolved'
    };
    return statusMap[status] || 'status-default';
  }

  public getPriorityClass(priority: string): string {
    const priorityMap: { [key: string]: string } = {
      'Low': 'priority-low', 'Medium': 'priority-medium',
      'High': 'priority-high', 'Urgent': 'priority-urgent'
    };
    return priorityMap[priority] ||  'priority-default';
  }
  // Add this new method inside your component class

  // Modify the getTicketAgeClass method in your component class
  public getTicketAgeClass(dateString: string | null, status?: string, lastUpdatedOn?: string | null): string {
    if (!dateString) return '';

    const now = new Date();
    const createdDate = new Date(dateString);
    const differenceInMs = now.getTime() - createdDate.getTime();
    const daysElapsed = Math.floor(differenceInMs / (1000 * 60 * 60 * 24));

    const classes: string[] = [];

    if (status && status.toLowerCase() === 'closed') {
      classes.push('age-closed');
    } else {
      if (daysElapsed >= 15) {
        classes.push('age-critical', 'blink-aggressive');
      } else if (daysElapsed >= 7) {
        classes.push('age-high', 'blink-subtle');
      } else if (daysElapsed >= 3) {
        classes.push('age-medium');
      } else if (daysElapsed >= 1) {
        classes.push('age-low');
      } else {
        classes.push('age-new');
      }
    }

    const result = classes.join(' ');
    console.log(`Ticket age: ${daysElapsed} days | Status: ${status} | Applied classes: ${result}`);
    return result;
  }



  public getTimeElapsed(dateString?: string | Date | null, isClosed: boolean = false): string {
    if (!dateString) return '';

    const now = new Date().getTime();
    const eventDate = new Date(dateString).getTime();
    const seconds = Math.floor((now - eventDate) / 1000);

    let interval = seconds / 31536000;
    if (interval > 1) return Math.floor(interval) + " year(s) ago";
    interval = seconds / 2592000;
    if (interval > 1) return Math.floor(interval) + " month(s) ago";
    interval = seconds / 86400;
    if (interval > 1) return Math.floor(interval) + " day(s) ago";
    interval = seconds / 3600;
    if (interval > 1) return Math.floor(interval) + " hour(s) ago";
    interval = seconds / 60;
    if (interval > 1) return Math.floor(interval) + " minute(s) ago";
    return "just now";
  }


  // Helper method to check if we're currently viewing delayed tickets
  public get isShowingDelayedTickets(): boolean {
    return this.activeStatFilter === 'delayed';
  }

  // Helper method to get appropriate loading state
  public get isCurrentlyLoading(): boolean {
    return this.isLoading || (this.isLoadingDelayed && this.activeStatFilter === 'delayed');
  }

  // --- PLANE KANBAN HELPER PROPERTIES & METHODS ---
  public viewMode: 'list' | 'board' = 'board';

  public setViewMode(mode: 'list' | 'board'): void {
    this.viewMode = mode;
  }

  public getTicketsByStatus(status: string): Ticket[] {
    if (this.selectedStatus && this.selectedStatus.toLowerCase() !== status.toLowerCase()) {
      return [];
    }
    return this.filteredTickets.filter(t => (t.status || '').trim().toLowerCase() === status.trim().toLowerCase());
  }

  public getStatusDotClass(status: string): string {
    const s = (status || '').toLowerCase().trim();
    if (s === 'open' || s === 'new') return 'bg-blue-500';
    if (s === 'in progress' || s === 'progress') return 'bg-amber-500';
    if (s === 'work done' || s === 'resolved') return 'bg-emerald-500';
    if (s === 'rework') return 'bg-red-500';
    if (s === 'closed') return 'bg-gray-500';
    if (s === 'on hold' || s === 'hold') return 'bg-amber-700';
    if (s === 'assigned') return 'bg-indigo-500';
    if (s === 'pending') return 'bg-cyan-500';
    return 'bg-purple-500';
  }

  public getStatusBadgeClass(status: string): string {
    const map: { [k: string]: string } = {
      'Open': 'bg-blue-500/10 text-blue-400 border-blue-500/30',
      'In Progress': 'bg-amber-500/10 text-amber-400 border-amber-500/30',
      'Work Done': 'bg-emerald-500/10 text-emerald-400 border-emerald-500/30',
      'Rework': 'bg-red-500/10 text-red-400 border-red-500/30',
      'Closed': 'bg-gray-500/10 text-gray-400 border-gray-500/30'
    };
    return map[status] || 'bg-brand-primary/10 text-brand-primary border-brand-primary/30';
  }

  public getOrderedStatusOptions(): string[] {
    const statusSet = new Set<string>();
    this.statusOptions.forEach(s => statusSet.add(s));
    this.filteredTickets.forEach(t => {
      if (t.status && t.status.trim()) {
        statusSet.add(t.status.trim());
      }
    });

    let list = Array.from(statusSet);
    if (this.selectedStatus) {
      return list.filter(s => s.toLowerCase() === this.selectedStatus!.toLowerCase());
    }
    const openIndex = list.findIndex(s => s.toLowerCase() === 'open');
    let openValue = 'Open';
    if (openIndex !== -1) {
      openValue = list[openIndex];
      list.splice(openIndex, 1);
    }
    const counts: { [status: string]: number } = {};
    list.forEach(status => {
      counts[status] = this.filteredTickets.filter(t => (t.status || '').trim().toLowerCase() === status.toLowerCase()).length;
    });
    list.sort((a, b) => (counts[b] || 0) - (counts[a] || 0));
    return [openValue, ...list];
  }


  // --- GLOBAL SEARCH ---
  public globalSearchQuery: string = '';
  public globalSearchType: 'ticket' | 'docket' | 'auto' = 'ticket';
  public globalSearchResults: any[] = [];
  public showGlobalSearchModal: boolean = false;
  public isGlobalSearching: boolean = false;
  public lastSearchedQuery: string = '';

  public async performGlobalSearch(): Promise<void> {
    if (!this.globalSearchQuery || !this.globalSearchQuery.trim()) return;
    const query = this.globalSearchQuery.trim();
    this.lastSearchedQuery = query;
    this.isGlobalSearching = true;
    try {
      const results = await this.ticketService.searchGlobalTicket(query, this.globalSearchType);
      this.globalSearchResults = results || [];
      if (this.globalSearchResults.length === 0) {
        alert(`No tickets found matching Ticket/Docket #: "${query}"`);
      } else {
        this.showGlobalSearchModal = true;
      }
    } catch (error) {
      console.error('Failed to perform global ticket search:', error);
      alert(`Search failed for query "${query}". Please check your input and try again.`);
    } finally {
      this.isGlobalSearching = false;
    }
  }

  public navigateToSearchedTicket(ticket: any): void {
    this.showGlobalSearchModal = false;
    const ticketId = ticket.id || ticket.ticketId || ticket.Id || ticket.TicketId;
    if (!ticketId) return;
    const routeUrl = this.authService.getTicketDetailRoute(Number(ticketId));
    this.router.navigate([routeUrl]);
  }
}