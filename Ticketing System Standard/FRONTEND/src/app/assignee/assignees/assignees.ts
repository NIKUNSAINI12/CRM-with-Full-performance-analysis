import { Component, OnInit, OnDestroy, Inject, HostListener, ViewEncapsulation } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { Observable, Subscription, from, BehaviorSubject } from 'rxjs';
import { switchMap, map, catchError, finalize, tap } from 'rxjs/operators';
import { TICKET_SERVICE_TOKEN } from '../../Core/injection-tokens';

import { Ticket, User } from '../../Core/models/ticket.model'; 
import { DashboardStats, TicketService, TicketResponse, Sprint } from '../../Core/services/ticket.service';
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

  // Sidebar state (mirror PM layout structure)
  public sidebarCollapsed = false;
  public sidebarOpen = false;
  public currentUser: User | null = null;

  // Tickets
  private ticketsSubject = new BehaviorSubject<Ticket[]>([]);
  public tickets$: Observable<Ticket[]> = this.ticketsSubject.asObservable();
  public allTickets: Ticket[] = [];
  public paginatedTickets: Ticket[] = [];
  
  // Pagination
  public currentPage: number = 1;
  public pageSize: number = 15;
  public totalPages: number = 0;
  public totalRecords: number = 0;
  public pageSizeOptions: number[] = [10, 15, 25, 50];
  
  // Loading and stats
  public stats: DashboardStats | null = null;
  public displayStats = {
    totalPending: 0,
    forReview: 0,
    urgentTickets: 0,
    delayedTickets: 0,
    newToday: 0
  };
  public activeStatFilter: string | null = null;
  public isLoading = false;

  // Filters
  public statusOptions: string[] = ['Open', 'In Progress', 'Work Done', 'Pending', 'Resolved', 'Closed', 'On Hold'];
  public priorityOptions: string[] = ['Low', 'Medium', 'High', 'Urgent'];
  public selectedStatus: string | null = null;
  public selectedPriority: string | null = null;
  public searchTicketNumber = '';

  public allCustomers: User[] = [];
  public selectedCustomer: number | null = null;
  public sprints: Sprint[] = [];
  public selectedSprintId: string | number | null = null;

  // Sorting
  public sortField: string = '';
  public sortDirection: 'asc' | 'desc' = 'asc';

  private subscription = new Subscription();
  private developerId: number = 0;
  private developerCode: string = '';
  private managerId: number = 0;

  // Customer dropdown filter properties
  public customerSearchInput = 'All Customers';
  public showCustomerDropdown = false;

  // Detect screen size
  public isMobile = false;
  
  // Customer list modal properties
  public showCustomerSelect = false;
  public filteredCustomers: User[] = [];
  public customerSearchTerm: string = '';
  public customerListIsLoading = false;
  public showFilterSection = false;
  public sprintTickets: Ticket[] = [];

  // Prevent duplicate API calls
  private customersLoaded = false;

  constructor(
    @Inject(TICKET_SERVICE_TOKEN) private ticketService: TicketService,
    private router: Router,
    private route: ActivatedRoute,
    private authService: AuthService
  ) {
    this.checkScreenSize();
  }

  @HostListener('window:resize', ['$event'])
  onResize(event?: Event) {
    this.checkScreenSize();
  }

  private checkScreenSize() {
    this.isMobile = window.innerWidth <= 768;
    if (this.isMobile) {
      this.sidebarOpen = false;
    }
  }

  async ngOnInit(): Promise<void> {
    const savedState = localStorage.getItem('sidebarCollapsed');
    if (savedState !== null && !this.isMobile) {
      this.sidebarCollapsed = JSON.parse(savedState);
    }
    
    const user = this.authService.getCurrentUser();
    if (user?.userId) {
      this.developerId = Number(user.userId);
      this.loadCurrentUser(this.developerId);
      
      // Resolve developer code and then load tickets
      try {
        const assignee = await this.ticketService.getAssigneeById(this.developerId);
        this.developerCode = assignee?.assigneeNumber || `E${this.developerId}`;
        this.managerId = assignee?.managerId || 0;
      } catch (err) {
        console.error('Failed to fetch assignee details, falling back to legacy E prefix:', err);
        this.developerCode = `E${this.developerId}`;
      }

      await this.loadDeveloperStats();
      await this.loadStatuses();
      this.loadTickets();
      this.loadAllCustomers();
      this.loadSprints();
    }


  }

  ngOnDestroy(): void {
    this.subscription.unsubscribe();
  }

  private loadCurrentUser(id: number): void {
    try {
      const user = this.authService.getCurrentUser();
      if (user && (Number(user.userId) === id || (user as any).id === id)) {
        this.currentUser = {
          id: Number(user.userId) || id,
          fullName: user.name || 'Executive Member',
          userNumber: user.userNumber || 'DEV',
          role: user.role,
          email: ''
        } as User;
      } else {
        throw new Error('User ID mismatch or not logged in');
      }
    } catch (error) {
      console.error('Failed to load current user', error);
      this.currentUser = { id: id, fullName: 'Executive Member', userNumber: 'DEV', role: 'Assignee' } as User;
    }
  }

  // Unified customer loading method
  private async loadAllCustomers(): Promise<void> {
    if (this.customersLoaded) {
      this.filteredCustomers = [...this.allCustomers];
      return;
    }

    try {
      const customers = await this.ticketService.getAllCustomers();
      this.allCustomers = customers;
      this.filteredCustomers = [...customers];
      this.customersLoaded = true;
      this.updateSearchInputs();
    } catch (error) {
      console.error('Failed to load all customers via new endpoint, falling back:', error);
      try {
        const fallback = await this.ticketService.getMyCustomers();
        this.allCustomers = fallback;
        this.filteredCustomers = [...fallback];
        this.customersLoaded = true;
        this.updateSearchInputs();
      } catch (fbErr) {
        console.error('Fallback customer loading failed:', fbErr);
        this.allCustomers = [];
        this.filteredCustomers = [];
      }
    }
  }

  private async loadSprints(): Promise<void> {
    try {
      this.sprints = await this.ticketService.getSprints();
    } catch (error) {
      console.error('Failed to load sprints:', error);
    }
  }

  public toggleSidebar(): void {
    if (this.isMobile) {
      this.sidebarOpen = !this.sidebarOpen;
    } else {
      this.sidebarCollapsed = !this.sidebarCollapsed;
      localStorage.setItem('sidebarCollapsed', JSON.stringify(this.sidebarCollapsed));
    }
  }

  async loadDeveloperStats(): Promise<void> {
    if (!this.developerId) return;
    try {
      this.stats = await this.ticketService.getDeveloperDashboardStats(this.developerId);
    } catch (error) {
      console.error('Failed to load developer stats:', error);
    }
  }

  private async loadStatuses(): Promise<void> {
    try {
      const statuses = await this.ticketService.getCustomDeveloperStatuses();
      if (statuses && statuses.length > 0) {
        this.statusOptions = statuses.filter((s: any) => s.isActive).map((s: any) => s.statusName);
      }
    } catch (error) {
      console.error('Failed to load developer statuses:', error);
      this.statusOptions = ['Open', 'In Progress', 'Pending PM Review', 'Pending for PM Review', 'Work Done', 'Pending', 'Resolved', 'Closed', 'On Hold'];
    }
  }

  private loadTickets(): void {
    this.isLoading = true;

    from(this.ticketService.getTicketsForDeveloper(this.developerCode || `E${this.developerId}`)).pipe(
        finalize(() => { this.isLoading = false; })
    ).subscribe({
        next: async (response) => {
            this.allTickets = response.tickets || [];
            this.updateStatusOptionsWithTicketStatuses();
            await this.loadTicketDeadlines();
            this.applyFiltersAndPagination();
        },
        error: (error) => {
            console.error('Failed to load developer tickets:', error);
            this.allTickets = [];
            this.totalRecords = 0;
            this.applyFiltersAndPagination();
        }
    });
  }

  private async loadTicketDeadlines(): Promise<void> {
    if (this.allTickets.length === 0) return;

    try {
      // Single batch API call instead of N individual calls
      const ticketIds = this.allTickets.map(t => t.id);
      const deadlines = await this.ticketService.getTicketDeadlinesBatch(ticketIds);

      // Map results back onto tickets
      const deadlineMap = new Map<number, string | null>();
      deadlines.forEach(d => deadlineMap.set(d.ticketId, d.deadlineDate || null));

      this.allTickets.forEach(ticket => {
        (ticket as any).deadline = deadlineMap.get(ticket.id) || null;
      });
    } catch (error) {
      console.error('Error loading ticket deadlines:', error);
    }
  }


  private closeMobileSidebar(): void {
    if (this.isMobile) {
      this.sidebarOpen = false;
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
    let ownershipTickets = [...this.allTickets];

    // Compute dynamic stats on the developer's tickets subset
    const today = new Date();
    today.setHours(0, 0, 0, 0);
    const now = new Date();

    const totalPendingCount = ownershipTickets.filter(ticket => {
      const statusLower = ticket.status ? ticket.status.toLowerCase() : '';
      return !['closed', 'resolved'].includes(statusLower);
    }).length;

    const forReviewCount = ownershipTickets.filter(ticket => {
      const statusLower = ticket.status ? ticket.status.toLowerCase() : '';
      return statusLower.includes('review') || statusLower.includes('work done') || statusLower.includes('pm review');
    }).length;

    const urgentCount = ownershipTickets.filter(ticket => 
      ticket.priority === 'Urgent'
    ).length;

    const delayedCount = ownershipTickets.filter(ticket => {
      const statusLower = ticket.status ? ticket.status.toLowerCase() : '';
      if (['closed', 'resolved', 'on hold'].includes(statusLower)) {
        return false;
      }
      const deadlineStr = (ticket as any).deadline || ticket.deadline;
      if (!deadlineStr) return false;
      const deadlineDate = new Date(deadlineStr);
      return deadlineDate < now;
    }).length;

    const newTodayCount = ownershipTickets.filter(ticket => {
      if (!ticket.createdOn) return false;
      const ticketDate = new Date(ticket.createdOn);
      ticketDate.setHours(0, 0, 0, 0);
      return ticketDate.getTime() === today.getTime();
    }).length;

    this.displayStats = {
      totalPending: totalPendingCount,
      forReview: forReviewCount,
      urgentTickets: urgentCount,
      delayedTickets: delayedCount,
      newToday: newTodayCount
    };

    // Now apply additional presets and filters
    let processingTickets = [...ownershipTickets];

    if (this.searchTicketNumber && this.searchTicketNumber.trim()) {
      const q = this.searchTicketNumber.toLowerCase().trim();
      processingTickets = processingTickets.filter(ticket => 
        (ticket.ticketNumber && ticket.ticketNumber.toLowerCase().includes(q)) ||
        (ticket.subject && ticket.subject.toLowerCase().includes(q))
      );
    }

    if (this.selectedStatus) {
      processingTickets = processingTickets.filter(ticket => 
        ticket.status === this.selectedStatus
      );
    }

    if (this.selectedPriority) {
      processingTickets = processingTickets.filter(ticket => 
        ticket.priority === this.selectedPriority
      );
    }

    if (this.selectedCustomer !== null) {
      processingTickets = processingTickets.filter(ticket => 
        ticket.customerId === this.selectedCustomer
      );
    }

    if (this.selectedSprintId) {
      if (this.selectedSprintId === 'backlog') {
        processingTickets = processingTickets.filter(ticket => ticket.sprintId == null);
      } else {
        processingTickets = processingTickets.filter(ticket => ticket.sprintId === Number(this.selectedSprintId));
      }
    }

    if (this.activeStatFilter) {
        processingTickets = processingTickets.filter(ticket => {
            const ticketStatusLower = ticket.status ? ticket.status.toLowerCase() : '';
            switch (this.activeStatFilter) {
                case 'pending':
                    return !['closed', 'resolved'].includes(ticketStatusLower);
                case 'review':
                    return ticketStatusLower.includes('review') || ticketStatusLower.includes('work done') || ticketStatusLower.includes('pm review');
                case 'closed':
                    return ['closed', 'resolved'].includes(ticketStatusLower);
                case 'urgent':
                    return ticket.priority === 'Urgent';
                case 'new':
                    if (!ticket.createdOn) return false;
                    const ticketDate = new Date(ticket.createdOn);
                    ticketDate.setHours(0, 0, 0, 0);
                    return ticketDate.getTime() === today.getTime();
                case 'delayed':
                    if (['closed', 'resolved', 'on hold'].includes(ticketStatusLower)) {
                      return false;
                    }
                    const deadlineStr = (ticket as any).deadline || ticket.deadline;
                    if (!deadlineStr) return false;
                    const deadlineDate = new Date(deadlineStr);
                    return deadlineDate < now;
                default:
                    return true;
            }
        });
    }

    if (this.sortField) {
      processingTickets.sort((a, b) => {
        const aVal = this.getFieldValue(a, this.sortField);
        const bVal = this.getFieldValue(b, this.sortField);
        let comparison = 0;
        if (aVal < bVal) comparison = -1;
        else if (aVal > bVal) comparison = 1;
        return this.sortDirection === 'desc' ? comparison * -1 : comparison;
      });
    }

    const sprintList = processingTickets.filter(t => t.sprintId != null || t.sprintName);
    const priorityMap: { [key: string]: number } = { 'Urgent': 4, 'High': 3, 'Medium': 2, 'Low': 1 };
    this.sprintTickets = sprintList.sort((a, b) => {
      const isClosedA = a.status?.toLowerCase() === 'closed' || a.status?.toLowerCase() === 'close';
      const isClosedB = b.status?.toLowerCase() === 'closed' || b.status?.toLowerCase() === 'close';
      if (isClosedA !== isClosedB) {
        return isClosedA ? 1 : -1;
      }
      if (a.sprintOrder !== null && a.sprintOrder !== undefined && b.sprintOrder !== null && b.sprintOrder !== undefined) {
        return a.sprintOrder - b.sprintOrder;
      }
      const priorityA = priorityMap[a.priority || ''] || 0;
      const priorityB = priorityMap[b.priority || ''] || 0;
      return priorityB - priorityA;
    });

    this.totalRecords = processingTickets.length;
    this.totalPages = Math.ceil(this.totalRecords / this.pageSize);
    
    const startIndex = (this.currentPage - 1) * this.pageSize;
    const endIndex = startIndex + this.pageSize;
    this.paginatedTickets = processingTickets.slice(startIndex, endIndex);
    
    this.ticketsSubject.next(this.paginatedTickets);
  }

  private getFieldValue(obj: any, field: string): any {
    return field.split('.').reduce((o, key) => o && o[key], obj) || '';
  }

  public onTicketClick(ticket: Ticket): void {
    this.closeMobileSidebar();
    if (ticket.id) {
      this.router.navigate(['/developer/ticket', ticket.id]);
    }
  }

  public onFilterChange(): void {
    this.activeStatFilter = null;
    this.currentPage = 1;
    this.loadTickets();
  }

  public onStatCardClick(statType: 'pending' | 'urgent' | 'new' | 'delayed'): void {
    if (this.activeStatFilter === statType) {
        this.activeStatFilter = null;
    } else {
        this.activeStatFilter = statType;
    }
    this.selectedStatus = null;
    this.selectedPriority = null;
    this.selectedCustomer = null;
    this.searchTicketNumber = '';
    this.currentPage = 1;
    this.loadTickets();
  }

  public onSearchInputChange(): void {
    this.currentPage = 1;
    this.applyFiltersAndPagination();
  }

  public clearAllFilters(): void {
    this.selectedStatus = null;
    this.selectedPriority = null;
    this.selectedCustomer = null;
    this.selectedSprintId = null;
    this.searchTicketNumber = '';
    this.activeStatFilter = null;
    this.currentPage = 1;
    this.loadTickets();
    this.updateSearchInputs();
  }

  public toggleFilterSection(): void {
    this.showFilterSection = !this.showFilterSection;
  }

  public getActiveFiltersCount(): number {
    let count = 0;
    if (this.selectedStatus) count++;
    if (this.selectedPriority) count++;
    if (this.selectedCustomer !== null) count++;
    if (this.selectedSprintId !== null) count++;
    if (this.searchTicketNumber && this.searchTicketNumber.trim()) count++;
    return count;
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
    if (this.sortField !== field) return 'up-down';
    return this.sortDirection === 'asc' ? 'up' : 'down';
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
    this.loadTickets();
    this.loadDeveloperStats();
  }

  public refreshDashboard(): void {
    window.location.reload();
  }

  public getPageNumbers(): number[] {
    const pages = [];
    const maxVisible = this.isMobile ? 3 : 5;
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
    const end = this.getStartRecord() + this.paginatedTickets.length - 1;
    return this.totalRecords === 0 ? 0 : end;
  }

  public getStartRecord(): number {
    return this.totalRecords === 0 ? 0 : (this.currentPage - 1) * this.pageSize + 1;
  }
  
  public formatDate(dateString?: string | Date | null): string {
    if (!dateString) return '';
    const dateObj = new Date(dateString);
    if (isNaN(dateObj.getTime())) return '';
    return dateObj.toLocaleDateString('en-IN', {
      timeZone: 'Asia/Kolkata',
      day: '2-digit',
      month: 'short',
      year: 'numeric'
    });
  }

  public getStatusClass(status: string): string {
    const statusMap: { [key: string]: string } = {
      'New': 'status-new', 'In Progress': 'status-progress',
      'Pending': 'status-pending', 'Resolved': 'status-resolved', 'Closed': 'status-closed',
      'On Hold': 'status-hold', 'Assigned': 'status-assigned', 'Work Done': 'status-resolved'
    };
    return statusMap[status] || 'status-default';
  }

  public getPriorityClass(priority: string): string {
    const priorityMap: { [key: string]: string } = {
      'Low': 'priority-low', 'Medium': 'priority-medium',
      'High': 'priority-high', 'Urgent': 'priority-urgent'
    };
    return priorityMap[priority] || 'priority-default';
  }

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
      } else {
        classes.push('age-new');
      }
    }
    return classes.join(' ');
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

  public formatDeadline(deadlineDate: string | null | undefined): string {
    if (!deadlineDate) return 'No deadline';
    
    const deadline = new Date(deadlineDate);
    const now = new Date();
    now.setHours(0, 0, 0, 0);
    deadline.setHours(0, 0, 0, 0);
    
    const diffTime = deadline.getTime() - now.getTime();
    const diffDays = Math.ceil(diffTime / (1000 * 60 * 60 * 24));
    
    if (diffDays < 0) {
      return `Overdue by ${Math.abs(diffDays)}d`;
    } else if (diffDays === 0) {
      return 'Due today';
    } else if (diffDays === 1) {
      return 'Due tomorrow';
    } else {
      return `${diffDays}d remaining`;
    }
  }

  public getDeadlineClass(deadlineDate: string | null | undefined): string {
    if (!deadlineDate) return 'deadline-none';
    
    const deadline = new Date(deadlineDate);
    const now = new Date();
    now.setHours(0, 0, 0, 0);
    deadline.setHours(0, 0, 0, 0);
    
    const diffTime = deadline.getTime() - now.getTime();
    const diffDays = Math.ceil(diffTime / (1000 * 60 * 60 * 24));
    
    if (diffDays < 0) return 'deadline-overdue';
    if (diffDays <= 1) return 'deadline-urgent';
    if (diffDays <= 3) return 'deadline-soon';
    return 'deadline-normal';
  }

  public getFilterSummary(): string {
    const filters = [];
    if (this.selectedStatus) {
      filters.push(`Status: ${this.selectedStatus}`);
    }
    if (this.selectedPriority) {
      filters.push(`Priority: ${this.selectedPriority}`);
    }
    if (this.selectedCustomer !== null) {
      const customer = this.allCustomers.find(c => c.id === this.selectedCustomer);
      if (customer) {
        filters.push(`Customer: ${customer.fullName}`);
      }
    }
    if (this.selectedSprintId !== null) {
      if (this.selectedSprintId === 'backlog') {
        filters.push('Sprint: Backlog');
      } else {
        const sprint = this.sprints.find(s => s.sprintId === Number(this.selectedSprintId));
        filters.push(`Sprint: ${sprint ? sprint.sprintName : this.selectedSprintId}`);
      }
    }
    if (this.activeStatFilter) {
      const filterName = this.activeStatFilter.charAt(0).toUpperCase() + this.activeStatFilter.slice(1);
      filters.push(`Preset: ${filterName}`);
    }
    if (this.searchTicketNumber && this.searchTicketNumber.trim()) {
      filters.push(`Search: ${this.searchTicketNumber}`);
    }
    return filters.length > 0 ? filters.join(' | ') : 'All Tickets';
  }

  public navigateToSelectCustomer(): void {
    this.closeMobileSidebar();
    this.showCustomerSelect = true;
    this.customerListIsLoading = true;

    this.loadAllCustomers().then(() => {
      this.customerListIsLoading = false;
    }).catch(() => {
      this.customerListIsLoading = false;
    });
  }

  public closeCustomerSelect(): void {
    this.showCustomerSelect = false;
    this.customerSearchTerm = '';
    this.filteredCustomers = [...this.allCustomers];
    
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { openCreateDrawer: null },
      queryParamsHandling: 'merge'
    });
  }

  public filterCustomers(): void {
    if (!this.customerSearchTerm) {
      this.filteredCustomers = [...this.allCustomers];
      return;
    }
    const lowerTerm = this.customerSearchTerm.toLowerCase();
    
    this.filteredCustomers = this.allCustomers.filter(cust =>
      (cust.fullName?.toLowerCase().includes(lowerTerm)) ||
      (cust.userNumber?.toLowerCase().includes(lowerTerm))
    );
  }

  public selectCustomer(customer: User): void { 
    this.showCustomerSelect = false; 
    this.customerSearchTerm = '';     
    
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { openCreateDrawer: null },
      queryParamsHandling: 'merge'
    }).then(() => {
      this.router.navigate(
        ['/customer/ticket-create'], 
        { queryParams: { pmId: this.managerId, customerId: customer.id } }
      );
    });
  }

  // --- PLANE KANBAN HELPER PROPERTIES & METHODS ---
  public viewMode: 'list' | 'board' = 'board';

  public setViewMode(mode: 'list' | 'board'): void {
    this.viewMode = mode;
  }

  public getTicketsByStatus(status: string): Ticket[] {
    if (this.selectedStatus && this.selectedStatus !== status) {
      return [];
    }
    return this.paginatedTickets.filter(t => t.status === status);
  }

  public getStatusDotClass(status: string): string {
    const map: { [k: string]: string } = { 
      'Open': 'bg-blue-500', 
      'In Progress': 'bg-amber-500', 
      'Work Done': 'bg-emerald-500', 
      'Rework': 'bg-red-500', 
      'Closed': 'bg-gray-500',
      'On Hold': 'bg-amber-700',
      'Assigned': 'bg-indigo-500'
    };
    return map[status] || 'bg-gray-500';
  }

  public getOrderedStatusOptions(): string[] {
    const list = [...this.statusOptions];
    const openIndex = list.findIndex(s => s.toLowerCase() === 'open');
    let openValue = 'Open';
    if (openIndex !== -1) {
      openValue = list[openIndex];
      list.splice(openIndex, 1);
    }
    const counts: { [status: string]: number } = {};
    list.forEach(status => {
      counts[status] = this.allTickets.filter(t => t.status === status).length;
    });
    list.sort((a, b) => (counts[b] || 0) - (counts[a] || 0));
    return [openValue, ...list];
  }

  @HostListener('document:click', ['$event'])
  public onDocumentClick(event: MouseEvent): void {
    const target = event.target as HTMLElement;
    if (!target.closest('.customer-dropdown-container')) {
      if (this.showCustomerDropdown) {
        this.showCustomerDropdown = false;
        this.updateSearchInputs();
      }
    }
  }

  public updateSearchInputs(): void {
    if (this.selectedCustomer !== null) {
      const cust = this.allCustomers.find(c => c.id === this.selectedCustomer);
      this.customerSearchInput = cust ? cust.fullName : '';
    } else {
      this.customerSearchInput = 'All Customers';
    }
  }

  public selectCustomerFilter(customer: any | null): void {
    if (customer) {
      this.selectedCustomer = customer.id;
      this.customerSearchInput = customer.fullName;
    } else {
      this.selectedCustomer = null;
      this.customerSearchInput = 'All Customers';
    }
    this.showCustomerDropdown = false;
    this.onFilterChange();
  }

  public get filteredCustomerOptions(): any[] {
    const term = this.customerSearchInput.toLowerCase().trim();
    if (!term || term === 'all customers') return this.allCustomers;
    return this.allCustomers.filter(c => 
      c.fullName.toLowerCase().includes(term) || 
      (c.userNumber && c.userNumber.toLowerCase().includes(term))
    );
  }

  public getRemainingSprintTasksCount(): number {
    return this.sprintTickets.filter(t => {
      const statusLower = t.status ? t.status.toLowerCase() : '';
      return !['closed', 'resolved', 'close'].includes(statusLower);
    }).length;
  }
}